import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';
import { loadUnit, saveUnit } from '../src/temperature.ts';

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
        setItem: (key, value) => void saved.set(key, value),
    });
});

test('loads Fahrenheit when nothing is saved', () => {
    assert.equal(loadUnit(), 'F');
});

test('loads a saved choice', () => {
    saveUnit('C');
    assert.equal(loadUnit(), 'C');

    saveUnit('F');
    assert.equal(loadUnit(), 'F');
});

test('loads Fahrenheit for a value it does not know', () => {
    saved.set('unit', 'K');

    assert.equal(loadUnit(), 'F');
});

test('loads Fahrenheit when storage is blocked', () => {
    stubStorage({ getItem: blockedStorage, setItem: blockedStorage });

    assert.equal(loadUnit(), 'F');
});

test('saving survives blocked storage', () => {
    stubStorage({ getItem: blockedStorage, setItem: blockedStorage });

    assert.doesNotThrow(() => saveUnit('C'));
});
