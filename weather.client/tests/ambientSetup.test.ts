import assert from 'node:assert/strict';
import { test } from 'node:test';
import { ambientDefaultChoices, ambientName, ambientSettings, homeChoiceName } from '../src/ambientSetup.ts';

const yard = { id: 'amb-000000000001', name: 'Backyard' };
const camp = { id: 'amb-000000000002', name: 'Camp' };
const unnamed = { id: 'amb-000000000003', name: null };

test('the first device is home until another is chosen', () => {
    const settings = ambientSettings([yard, camp], []);

    assert.deepEqual(settings.map(setting => setting.home), [true, false]);
});

test('saved settings are kept, matching the ID in any case', () => {
    const saved = [{ deviceId: camp.id.toUpperCase(), name: 'Camp station', zip: '04102', hidden: false, home: true }];

    const settings = ambientSettings([yard, camp], saved);

    assert.deepEqual(settings.map(setting => setting.home), [false, true]);
    assert.equal(settings[1].name, 'Camp station');
});

test('names fall back to the Ambient name, and home to Home station', () => {
    const [home, other, third] = ambientSettings([yard, camp, unnamed], []);

    assert.equal(ambientName(home, yard), 'Home station');
    assert.equal(ambientName(other, camp), 'Camp');
    assert.equal(ambientName(third, unnamed), 'Ambient station');
    assert.equal(ambientName({ ...other, name: '  Barn ' }, camp), 'Barn');
});

test('default choices leave out home and hidden devices', () => {
    const settings = ambientSettings([yard, camp, unnamed], []).map(setting =>
        setting.deviceId === unnamed.id ? { ...setting, hidden: true } : setting);

    assert.deepEqual(ambientDefaultChoices([yard, camp, unnamed], settings), [{ id: camp.id, name: 'Camp' }]);
});

test('the home label uses the home device name', () => {
    const settings = ambientSettings([yard, camp], []).map(setting => setting.home ? { ...setting, name: 'House' } : setting);

    assert.equal(homeChoiceName([yard, camp], settings), 'House');
    assert.equal(homeChoiceName(undefined, []), 'Home station');
});
