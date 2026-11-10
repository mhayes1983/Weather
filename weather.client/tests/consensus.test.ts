import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { ForecastCell, ForecastRow } from '../src/api/forecast.ts';
import { consensus, consensusRange, consensusValue } from '../src/consensus.ts';

function cell(lowF: number | null, highF: number | null): ForecastCell {
    return { lowF, highF, lowAnomaly: 'None', highAnomaly: 'None' };
}

const row: ForecastRow = {
    date: '2026-11-10',
    averageHighF: 50,
    averageLowF: 32,
    cells: {
        nws: cell(30, 48),
        gfs: cell(33, 55),
        ecmwf: cell(null, 51),
    },
};

test('the consensus is the mean and range of the sources shown', () => {
    assert.deepEqual(consensus(row, ['nws', 'gfs', 'ecmwf'], 'high'), { meanF: 154 / 3, minF: 48, maxF: 55, count: 3 });
    assert.deepEqual(consensus(row, ['nws', 'gfs', 'ecmwf'], 'low'), { meanF: 31.5, minF: 30, maxF: 33, count: 2 });
});

test('a hidden source is left out', () => {
    assert.deepEqual(consensus(row, ['nws', 'ecmwf'], 'high'), { meanF: 49.5, minF: 48, maxF: 51, count: 2 });
});

test('one source is not a consensus', () => {
    assert.equal(consensus(row, ['nws', 'ecmwf'], 'low'), null);
    assert.equal(consensus(row, ['nws', 'missing'], 'high'), null);
    assert.equal(consensus(row, [], 'high'), null);
});

test('values and ranges follow the unit', () => {
    const high = consensus(row, ['nws', 'gfs', 'ecmwf'], 'high')!;
    assert.equal(consensusValue(high, 'F'), 51);
    assert.equal(consensusRange(high, 'F'), '48–55');
    assert.equal(consensusValue(high, 'C'), 11);
    assert.equal(consensusRange(high, 'C'), '9–13');
});

test('sources that agree show no range', () => {
    const same: ForecastRow = { ...row, cells: { a: cell(30, 50), b: cell(30, 50) } };
    assert.equal(consensusRange(consensus(same, ['a', 'b'], 'high')!, 'F'), null);
});
