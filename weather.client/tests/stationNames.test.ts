import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';
import { loadStationNames, maxStationNameLength, renameStation, stationName } from '../src/stationNames.ts';

const saved = new Map<string, string>();

function stubStorage(storage: Pick<Storage, 'getItem' | 'setItem'>) {
    Object.assign(globalThis, { window: { localStorage: storage } });
}

function blockedStorage(): never {
    throw new Error('Storage is blocked.');
}

beforeEach(() => {
    saved.clear();
    stubStorage({
        getItem: key => saved.get(key) ?? null,
        setItem: (key, value) => saved.set(key, value),
    });
});

test('a rename is saved and read back', () => {
    const names = renameStation({}, 'amb-0123456789ab', '  Camp station ');

    assert.deepEqual(names, { 'amb-0123456789ab': 'Camp station' });
    assert.deepEqual(loadStationNames(), names);
});

test('a blank name goes back to the station name', () => {
    const names = renameStation(renameStation({}, 'home', 'House'), 'home', ' ');

    assert.deepEqual(names, {});
    assert.equal(stationName(names, 'home', 'Home station'), 'Home station');
});

test('the browser name wins over the station name', () => {
    assert.equal(stationName({ home: 'House' }, 'home', 'Home station'), 'House');
    assert.equal(stationName({ home: 'House' }, 'KOTHER1', undefined), undefined);
});

test('long names are cut short', () => {
    const names = renameStation({}, 'home', 'x'.repeat(100));

    assert.equal(names.home.length, maxStationNameLength);
});

test('broken or odd storage reads as no names', () => {
    saved.set('stationNames', '{not json');
    assert.deepEqual(loadStationNames(), {});

    saved.set('stationNames', '["home"]');
    assert.deepEqual(loadStationNames(), {});

    saved.set('stationNames', '{"home":"House","bad":3,"__proto__":"x"}');
    assert.deepEqual(loadStationNames(), { home: 'House' });
});

test('blocked storage still renames for this page', () => {
    stubStorage({ getItem: blockedStorage, setItem: blockedStorage });

    assert.deepEqual(loadStationNames(), {});
    assert.deepEqual(renameStation({}, 'home', 'House'), { home: 'House' });
});
