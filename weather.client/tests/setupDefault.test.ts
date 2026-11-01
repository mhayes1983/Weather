import assert from 'node:assert/strict';
import { test } from 'node:test';
import { defaultAfterSetup, defaultStationChoices } from '../src/setupDefault.ts';

const brunswick = { id: 'KMEBRUNS32', name: 'Brunswick' };
const lookedUp = { id: 'KLOOKED1', name: 'Looked up' };
const zip = { id: 'om-90210', name: 'Beverly Hills, CA' };

test('a chosen station stays the default', () => {
    const choices = defaultStationChoices([brunswick], [], true);
    assert.equal(defaultAfterSetup('KMEBRUNS32', choices), 'KMEBRUNS32');
});

test('a saved station stays the default', () => {
    const choices = defaultStationChoices([brunswick], [lookedUp], true);
    assert.equal(defaultAfterSetup('KLOOKED1', choices), 'KLOOKED1');
});

test('a saved station stays the default with no stations chosen', () => {
    const choices = defaultStationChoices([], [zip], true);
    assert.equal(defaultAfterSetup('om-90210', choices), 'om-90210');
});

test('the home station stays the default', () => {
    assert.equal(defaultAfterSetup('home', []), 'home');
});

test('a station left out returns to home', () => {
    const choices = defaultStationChoices([], [], true);
    assert.equal(defaultAfterSetup('KMEBRUNS32', choices), 'home');
});

test('removing the key keeps Open-Meteo stations only', () => {
    const choices = defaultStationChoices([], [lookedUp, zip], false);
    assert.deepEqual(choices, [zip]);
    assert.equal(defaultAfterSetup('KLOOKED1', choices), 'home');
    assert.equal(defaultAfterSetup('om-90210', choices), 'om-90210');
});

test('a station in both lists appears once', () => {
    assert.deepEqual(defaultStationChoices([brunswick], [brunswick], true), [brunswick]);
});
