import assert from 'node:assert/strict';
import { test } from 'node:test';
import { precipSummary, snowAmount } from '../src/precipitation.ts';

test('snow shows one decimal place', () => {
    assert.equal(snowAmount(3), '3.0 in');
    assert.equal(snowAmount(0.26), '0.3 in');
});

test('snow too small to display is left out', () => {
    assert.equal(snowAmount(null), null);
    assert.equal(snowAmount(0), null);
    assert.equal(snowAmount(0.04), null);
});

test('summary lists chance, amount and snow', () => {
    assert.equal(precipSummary(60, 0.3, 3), 'Precip 60% chance, 0.30 in, 3.0 in snow');
});

test('summary leaves out amounts of zero', () => {
    assert.equal(precipSummary(5, 0, 0), 'Precip 5% chance');
});

test('summary is empty without any data', () => {
    assert.equal(precipSummary(null, null, null), null);
});
