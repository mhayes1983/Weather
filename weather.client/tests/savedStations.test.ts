import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';
import { addSavedStation, loadSavedStations, maxSavedStations, removeSavedStation, watchSavedStations } from '../src/savedStations.ts';

const first = { id: 'KFIRST1', name: 'First' };
const second = { id: 'KSECOND2', name: 'Second' };
const saved = new Map<string, string>();

type Listener = (event: { key: string | null }) => void;
let listeners: Listener[] = [];

function stubStorage(storage: Pick<Storage, 'getItem' | 'setItem'>) {
    Object.assign(globalThis, {
        window: {
            localStorage: storage,
            addEventListener: (_: string, listener: Listener) => listeners.push(listener),
            removeEventListener: (_: string, listener: Listener) => {
                listeners = listeners.filter(other => other !== listener);
            },
        },
    });
}

function blockedStorage(): never {
    throw new Error('Storage is blocked.');
}

function fullList() {
    return Array.from({ length: maxSavedStations }, (_, index) => ({ id: `KFULL${index}`, name: `Full ${index}` }));
}

beforeEach(() => {
    saved.clear();
    listeners = [];
    stubStorage({
        getItem: key => saved.get(key) ?? null,
        setItem: (key, value) => void saved.set(key, value),
    });
});

test('loads nothing when nothing is saved', () => {
    assert.deepEqual(loadSavedStations(), []);
});

test('adding a station saves it', () => {
    addSavedStation([], first);

    assert.deepEqual(loadSavedStations(), [first]);
});

test('adding a known station keeps one copy', () => {
    assert.deepEqual(addSavedStation([first, second], { ...first, name: 'Renamed' }), [second, { ...first, name: 'Renamed' }]);
});

test('removing a station saves the rest', () => {
    assert.deepEqual(removeSavedStation([first, second], first.id), [second]);
    assert.deepEqual(loadSavedStations(), [second]);
});

test('a full list refuses a new station and keeps every saved one', () => {
    const stations = fullList();

    assert.equal(addSavedStation(stations, first), stations);
    assert.deepEqual(loadSavedStations(), []);
});

test('a full list still updates a station it already has', () => {
    const stations = fullList();

    assert.equal(addSavedStation(stations, stations[0]).length, maxSavedStations);
});

test('removing from a full list makes room', () => {
    const stations = removeSavedStation(fullList(), 'KFULL0');

    assert.equal(addSavedStation(stations, first).at(-1), first);
});

test('stations build up in memory when storage is blocked', () => {
    stubStorage({ getItem: blockedStorage, setItem: blockedStorage });

    assert.deepEqual(addSavedStation(addSavedStation([], first), second), [first, second]);
});

test('loads nothing when storage is blocked', () => {
    stubStorage({ getItem: blockedStorage, setItem: blockedStorage });

    assert.deepEqual(loadSavedStations(), []);
});

test('picks up changes from another tab until unsubscribed', () => {
    const seen: unknown[] = [];
    const stop = watchSavedStations(stations => seen.push(stations));
    addSavedStation([], first);

    listeners[0]({ key: 'somethingElse' });
    listeners[0]({ key: 'savedStations' });
    stop();

    assert.deepEqual(seen, [[first]]);
    assert.equal(listeners.length, 0);
});
