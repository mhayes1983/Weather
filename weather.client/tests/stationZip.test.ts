import assert from 'node:assert/strict';
import { test } from 'node:test';
import { stationZip } from '../src/stationZip.ts';

const listed = [
    { id: 'home', name: 'Home station', zip: '04086' },
    { id: 'KCONFIG1', name: 'Configured', zip: '04011' },
    { id: 'KNOZIP1', name: 'No ZIP', zip: null },
];

test('a saved station uses the ZIP it was found by', () => {
    assert.equal(stationZip('KFOUND1', [{ id: 'KFOUND1', name: 'Found', zip: '04101' }], listed, '04086'), '04101');
});

test('a configured station uses its configured ZIP', () => {
    assert.equal(stationZip('KCONFIG1', [], listed, '00000'), '04011');
    assert.equal(stationZip('home', [], listed, '00000'), '04086');
});

test('an Open-Meteo station uses the ZIP in its ID', () => {
    assert.equal(stationZip('om-04101', [{ id: 'om-04101', name: 'ZIP 04101' }], listed, '04086'), '04101');
});

test('a station without a ZIP falls back to the home ZIP', () => {
    assert.equal(stationZip('KNOZIP1', [], listed, '04086'), '04086');
    assert.equal(stationZip('KFOUND1', [{ id: 'KFOUND1', name: 'Found by ID' }], [], '04086'), '04086');
});
