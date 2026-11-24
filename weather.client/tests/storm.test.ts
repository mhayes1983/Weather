import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';
import { outageReasons, snowRatio } from '../src/outageRisk.ts';
import { agreementLimits, sourceSpread } from '../src/snowAverages.ts';
import { emptyManualSnow, loadManualSnow, manualStorageKeys, saveManualSnow } from '../src/snowManual.ts';
import { agreementByHour, gustSegments, intensity, windSummary, windySpans } from '../src/snowTiming.ts';
import { formatAmount, readView, reportTowns } from '../src/stormViews.ts';

const saved = new Map<string, string>();

beforeEach(() => {
    saved.clear();
    Object.assign(globalThis, {
        window: {
            localStorage: {
                getItem: (key: string) => saved.get(key) ?? null,
                setItem: (key: string, value: string) => saved.set(key, value),
            },
        },
    });
});

test('rates are light, moderate or heavy by each view\'s scale', () => {
    assert.equal(intensity(0.5), 'moderate');
    assert.equal(intensity(0.05, 'rain'), 'light');
    assert.equal(intensity(0.15, 'rain'), 'moderate');
    assert.equal(intensity(0.3, 'rain'), 'heavy');
    assert.equal(intensity(25, 'wind'), 'light');
    assert.equal(intensity(30, 'wind'), 'moderate');
    assert.equal(intensity(46, 'wind'), 'heavy');
});

test('only gusts of 30 mph or more make segments and count towards agreement', () => {
    const gusts = [
        { start: '2026-11-12T01:00:00-05:00', end: '2026-11-12T02:00:00-05:00', mph: 25 },
        { start: '2026-11-12T02:00:00-05:00', end: '2026-11-12T04:00:00-05:00', mph: 42 },
    ];

    const segments = gustSegments(gusts, '2026-11-12', '2026-11-12');
    assert.equal(segments.length, 1);
    assert.equal(segments[0].rate, 42);

    const counts = agreementByHour([windySpans(gusts), windySpans([gusts[1]])], '2026-11-12', '2026-11-12');
    assert.deepEqual(counts.slice(0, 5), [0, 0, 2, 2, 0]);
});

test('a wind bar summary gives its span and strongest gust', () => {
    assert.equal(
        windSummary({
            maxMph: 47.6,
            windyStart: '2026-11-12T19:00:00-05:00',
            windyEnd: '2026-11-13T01:00:00-05:00',
            peakStart: '2026-11-12T23:00:00-05:00',
            peakEnd: '2026-11-13T00:00:00-05:00',
        }),
        'Thu 7 PM – Fri 1 AM: gusts to 48 mph, strongest Thu 11 PM');
    assert.equal(windSummary({ maxMph: 20, windyStart: null, windyEnd: null, peakStart: null, peakEnd: null }), '');
});

test('views read from an address fall back to snow', () => {
    assert.equal(readView('rain'), 'rain');
    assert.equal(readView('depth'), 'depth');
    assert.equal(readView(null), 'snow');
    assert.equal(readView('sleet'), 'snow');
});

test('amounts take each view\'s digits', () => {
    assert.equal(formatAmount(1.25, 'snow'), '1.3');
    assert.equal(formatAmount(1.256, 'rain'), '1.26');
    assert.equal(formatAmount(2, 'rain'), '2');
    assert.equal(formatAmount(47.6, 'wind'), '48');
    assert.equal(formatAmount(null, 'wind'), '');
});

test('reports follow the view, and depth has none', () => {
    const reports = { towns: ['snow'], rainTowns: ['rain'], windTowns: null };

    assert.deepEqual(reportTowns(reports, 'snow'), ['snow']);
    assert.deepEqual(reportTowns(reports, 'rain'), ['rain']);
    assert.deepEqual(reportTowns(reports, 'wind'), []);
    assert.deepEqual(reportTowns(reports, 'depth'), []);
    assert.deepEqual(reportTowns(undefined, 'snow'), []);
});

test('sources agree within the limit or a quarter of the mean, leaving out partial rows', () => {
    const snow = (low: number, high = low, partial = false) => ({ low, high, partial });

    assert.deepEqual(sourceSpread([snow(4), snow(5)], agreementLimits.snow), { lowest: 4, highest: 5, agree: true });
    assert.equal(sourceSpread([snow(4), snow(8)], agreementLimits.snow)?.agree, false);
    // 20 and 24 inches differ by 4, but that's under a quarter of their mean.
    assert.equal(sourceSpread([snow(20), snow(24)], agreementLimits.snow)?.agree, true);
    assert.equal(sourceSpread([snow(4), snow(12, 12, true)], agreementLimits.snow), null);
    // A source with no data for the dates is left out rather than counted as 0.
    assert.deepEqual(sourceSpread([snow(4), snow(5), { low: null, high: null }], agreementLimits.snow), { lowest: 4, highest: 5, agree: true });
    assert.equal(sourceSpread([snow(0.5), snow(1)], agreementLimits.rain)?.agree, false);
    assert.equal(sourceSpread([snow(40), snow(48)], agreementLimits.wind)?.agree, true);
});

test('outage risk needs wet snow, ice, wind with snow or ice, or strong wind alone', () => {
    const calm = { snowInches: 0, ratio: null, iceInches: 0, gustMph: 20 };

    assert.deepEqual(outageReasons(calm), []);
    assert.deepEqual(outageReasons({ ...calm, snowInches: 6, ratio: 7 }), ['heavy wet snow (6 in at 7:1)']);
    assert.deepEqual(outageReasons({ ...calm, snowInches: 6, ratio: 12 }), []);
    assert.deepEqual(outageReasons({ ...calm, iceInches: 0.3 }), ['ice of 0.30 in']);
    assert.deepEqual(outageReasons({ ...calm, gustMph: 42, snowInches: 3, ratio: 12 }), ['gusts near 42 mph with snow']);
    assert.deepEqual(outageReasons({ ...calm, gustMph: 42 }), []);
    assert.deepEqual(outageReasons({ ...calm, gustMph: 60 }), ['gusts near 60 mph']);
});

test('the outage ratio ignores a dusting in a rainstorm and weights sources by their snow', () => {
    // From the review: two sources with 6 in at 12:1 and one with 0.3 in of snow in 1.2 in of water (0:1 after rounding).
    const cells = [{ highInches: 6, ratio: 12 }, { highInches: 0.3, ratio: 0 }, { highInches: 6, ratio: 12 }];

    assert.equal(snowRatio(cells), 12);
    assert.deepEqual(outageReasons({ snowInches: 4.1, ratio: snowRatio(cells), iceInches: 0, gustMph: 0 }), []);

    // 8 in at 8:1 and 2 in at 16:1 make 10 in from 1.125 in of water.
    assert.equal(snowRatio([{ highInches: 8, ratio: 8 }, { highInches: 2, ratio: 16 }])?.toFixed(2), '8.89');
    assert.equal(snowRatio([{ highInches: 0.5, ratio: 4 }, { highInches: null, ratio: null }]), null);
});

test('each view keeps its own manual rows, and snow keeps its old key', () => {
    const rain = { ...emptyManualSnow(), values: { ncm: { '04086': { low: 1, high: 1.5 } } } };
    saveManualSnow(rain, 'rain');

    assert.equal(manualStorageKeys.snow, 'snowManual');
    assert.ok(saved.has('rainManual'));
    assert.equal(saved.has('snowManual'), false);
    assert.deepEqual(loadManualSnow('rain').values, rain.values);
    assert.deepEqual(loadManualSnow('snow').values, {});
    assert.deepEqual(loadManualSnow('wind').values, {});
});
