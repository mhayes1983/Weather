import assert from 'node:assert/strict';
import { test } from 'node:test';
import { moveCard, orderCards, stepCard, toggleCollapsed, type StationLayout } from '../src/stationLayout.ts';

const empty: StationLayout = { order: [], collapsed: [] };
const shown = ['Wind', 'Rain', 'Atmosphere', 'Sun'];

test('saved order comes first and unnamed cards keep their default order after it', () => {
    const items = ['Wind', 'Rain', 'Atmosphere', 'Sun', 'Soil'];
    assert.deepEqual(orderCards(items, item => item, ['Sun', 'Wind', 'Gone']), ['Sun', 'Wind', 'Rain', 'Atmosphere', 'Soil']);
    assert.deepEqual(orderCards(items, item => item, []), items);
});

test('a card moves before another or to the end', () => {
    assert.deepEqual(moveCard(empty, shown, 'Sun', 'Wind').order, ['Sun', 'Wind', 'Rain', 'Atmosphere']);
    assert.deepEqual(moveCard(empty, shown, 'Wind', null).order, ['Rain', 'Atmosphere', 'Sun', 'Wind']);
});

test('cards this station lacks keep their place in the saved order', () => {
    const layout = { ...empty, order: ['Soil', 'Wind', 'Rain', 'Atmosphere', 'Sun'] };
    assert.deepEqual(moveCard(layout, shown, 'Rain', 'Wind').order, ['Rain', 'Wind', 'Atmosphere', 'Sun', 'Soil']);
});

test('arrow keys move one place and stop at the ends', () => {
    assert.deepEqual(stepCard(empty, shown, 'Rain', -1).order, ['Rain', 'Wind', 'Atmosphere', 'Sun']);
    assert.deepEqual(stepCard(empty, shown, 'Rain', 1).order, ['Wind', 'Atmosphere', 'Rain', 'Sun']);
    assert.deepEqual(stepCard(empty, shown, 'Atmosphere', 1).order, ['Wind', 'Rain', 'Sun', 'Atmosphere']);
    assert.equal(stepCard(empty, shown, 'Wind', -1), empty);
    assert.equal(stepCard(empty, shown, 'Sun', 1), empty);
});

test('collapsing toggles', () => {
    const collapsed = toggleCollapsed(empty, 'Rain');
    assert.deepEqual(collapsed.collapsed, ['Rain']);
    assert.deepEqual(toggleCollapsed(collapsed, 'Rain').collapsed, []);
});
