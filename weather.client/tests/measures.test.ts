import assert from 'node:assert/strict';
import { test } from 'node:test';
import { toggleMeasure } from '../src/measures.ts';

test('hiding a measure leaves the other', () => {
    assert.deepEqual(toggleMeasure(['low', 'high'], 'high'), ['low']);
    assert.deepEqual(toggleMeasure(['low', 'high'], 'low'), ['high']);
});

test('the last measure shown stays shown', () => {
    assert.deepEqual(toggleMeasure(['high'], 'high'), ['high']);
});

test('showing a measure again keeps low before high', () => {
    assert.deepEqual(toggleMeasure(['high'], 'low'), ['low', 'high']);
    assert.deepEqual(toggleMeasure(['low'], 'high'), ['low', 'high']);
});
