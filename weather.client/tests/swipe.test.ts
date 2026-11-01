import assert from 'node:assert/strict';
import { test } from 'node:test';
import { swipeAction } from '../src/swipe.ts';

test('swiping right opens a closed menu', () => {
    assert.equal(swipeAction(80, 5, false), 'open');
});

test('swiping left closes an open menu', () => {
    assert.equal(swipeAction(-80, 5, true), 'close');
});

test('the other direction does nothing', () => {
    assert.equal(swipeAction(-80, 0, false), undefined);
    assert.equal(swipeAction(80, 0, true), undefined);
});

test('short swipes do nothing', () => {
    assert.equal(swipeAction(59, 0, false), undefined);
});

test('scrolling at an angle does nothing', () => {
    assert.equal(swipeAction(80, 60, false), undefined);
});
