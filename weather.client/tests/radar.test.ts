import assert from 'node:assert/strict';
import { test } from 'node:test';
import { arcgisTileUrl, mapModes, precipTypeKey, readLayerTimes, readTemperatureLegend } from '../src/api/radar.ts';

const capabilities = `<?xml version="1.0"?>
<WMS_Capabilities version="1.3.0">
  <Capability>
    <Layer>
      <Name>WMS</Name>
      <Dimension name="time" units="ISO8601">2020-01-01T00:00:00.000Z</Dimension>
      <Layer queryable="1">
        <Name>other_layer</Name>
        <Dimension name="time" units="ISO8601">2026-11-06T10:00:00.000Z,2026-11-06T10:05:00.000Z</Dimension>
      </Layer>
      <Layer queryable="1">
        <Name>goes_longwave_imagery</Name>
        <Dimension name="time" default="2026-11-06T13:03:00Z" units="ISO8601" nearestValue="1">2026-11-06T12:53:00.000Z, 2026-11-06T12:58:00.000Z,2026-11-06T13:03:00.000Z</Dimension>
      </Layer>
      <Layer>
        <Name>no_times</Name>
      </Layer>
    </Layer>
  </Capability>
</WMS_Capabilities>`;

test('reads the times of the named layer, not the first in the document', () => {
    assert.deepEqual(readLayerTimes(capabilities, 'goes_longwave_imagery'), [
        '2026-11-06T12:53:00.000Z',
        '2026-11-06T12:58:00.000Z',
        '2026-11-06T13:03:00.000Z',
    ]);
});

test('returns no times for a missing layer or one without times', () => {
    assert.deepEqual(readLayerTimes(capabilities, 'missing'), []);
    assert.deepEqual(readLayerTimes(capabilities, 'no_times'), []);
});

test('builds the Web Mercator bounds of a tile', () => {
    const url = new URL(arcgisTileUrl('https://example.test/MapServer', '0', 0, 0, 1, 7));
    const edge = 20037508.34;

    assert.equal(url.pathname, '/MapServer/export');
    assert.deepEqual(url.searchParams.get('bbox')!.split(',').map(Number), [-edge, 0, 0, edge]);
    assert.equal(url.searchParams.get('bboxSR'), '3857');
    assert.equal(url.searchParams.get('imageSR'), '3857');
    assert.equal(url.searchParams.get('layers'), 'show:0');
    assert.equal(url.searchParams.get('size'), '256,256');
    assert.equal(url.searchParams.get('v'), '7');
});

test('a tile over Maine stays inside its zoom level grid', () => {
    const url = new URL(arcgisTileUrl('https://example.test/MapServer', '0', 19, 23, 6, 0));
    const [xmin, ymin, xmax, ymax] = url.searchParams.get('bbox')!.split(',').map(Number);

    assert.ok(xmin < -7_800_000 && xmax > -7_800_000, 'tile covers about 70°W');
    assert.ok(ymin < 5_621_000 && ymax > 5_621_000, 'tile covers about 45°N');
    assert.ok(Math.abs(xmax - xmin - (ymax - ymin)) < 0.1, 'tile is square');
});

test('reads the temperature scale in range, keyed by the top of each band', () => {
    const legend = {
        layers: [
            { layerId: 4, legend: [{ label: '0 - 1', imageData: 'WRONG', contentType: 'image/png' }] },
            {
                layerId: 8,
                legend: [
                    { label: '-80.139999 - -30', imageData: 'A', contentType: 'image/png' },
                    { label: '-20.99999999 - -20', imageData: 'B', contentType: 'image/png' },
                    { label: '-19.99999999 - -19', imageData: 'C', contentType: 'image/png' },
                    { label: '31.00000001 - 32', imageData: 'D', contentType: 'image/png' },
                    { label: '109.0000001 - 110', imageData: 'E', contentType: 'image/png' },
                    { label: '110.0000001 - 140', imageData: 'F', contentType: 'image/png' },
                ],
            },
        ],
    };

    assert.deepEqual(readTemperatureLegend(legend), [
        { fahrenheit: -19, image: 'data:image/png;base64,C' },
        { fahrenheit: 32, image: 'data:image/png;base64,D' },
        { fahrenheit: 110, image: 'data:image/png;base64,E' },
    ]);
});

test('reads no temperature scale from an unexpected response', () => {
    assert.deepEqual(readTemperatureLegend({}), []);
});

test('keys the precip type legend blocks in NOAA order', () => {
    // NOAA's image has seven equal blocks labelled with these codes. If the image
    // changes, the hover zones would name the wrong colours.
    assert.deepEqual(precipTypeKey.map(entry => entry.code), ['WS', 'S', 'C', 'H', 'CS', 'ST', 'CT']);
    assert.ok(precipTypeKey.every(entry => entry.label && entry.description));
});

test('gives every mode a unique id and one kind of legend', () => {
    assert.equal(new Set(mapModes.map(mode => mode.id)).size, mapModes.length);

    for (const mode of mapModes) {
        const legends = [mode.kind === 'arcgis', Boolean(mode.legendKey), Boolean(mode.scale)].filter(Boolean);
        assert.ok(legends.length <= 1, `${mode.id} has ${legends.length} legends`);
    }
});

test('explains the clouds scale and links the warning colours', () => {
    const clouds = mapModes.find(mode => mode.id === 'clouds')!;
    const lightning = mapModes.find(mode => mode.id === 'lightning')!;

    assert.ok(clouds.scale && clouds.scale.colours.length >= 2);
    assert.match(clouds.scale.start, /warmer/i);
    assert.match(clouds.scale.end, /colder/i);
    assert.ok(lightning.overlay);
    assert.match(lightning.noteLink?.href ?? '', /^https:\/\/www\.weather\.gov\//);
});

test('animates every mode except temperature', () => {
    assert.deepEqual(mapModes.filter(mode => !mode.animated).map(mode => mode.id), ['temperature']);
});
