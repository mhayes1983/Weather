import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';
import { applyTheme, loadTheme, saveTheme } from '../src/theme.ts';

const dataset: DOMStringMap = {};
const saved = new Map<string, string>();

function stubStorage(storage: Pick<Storage, 'getItem' | 'setItem'>) {
    Object.assign(globalThis, { window: { localStorage: storage } });
}

function blockedStorage(): never {
    throw new Error('Storage is blocked.');
}

beforeEach(() => {
    saved.clear();
    delete dataset.theme;
    Object.assign(globalThis, { document: { documentElement: { dataset } } });
    stubStorage({
        getItem: key => saved.get(key) ?? null,
        setItem: (key, value) => void saved.set(key, value),
    });
});

test('loads auto when nothing is saved', () => {
    assert.equal(loadTheme(), 'auto');
});

test('loads a saved choice', () => {
    saveTheme('light');
    assert.equal(loadTheme(), 'light');

    saveTheme('dark');
    assert.equal(loadTheme(), 'dark');
});

test('loads auto for a value it does not know', () => {
    saved.set('theme', 'purple');

    assert.equal(loadTheme(), 'auto');
});

test('loads auto when storage is blocked', () => {
    stubStorage({ getItem: blockedStorage, setItem: blockedStorage });

    assert.equal(loadTheme(), 'auto');
});

test('saving survives blocked storage', () => {
    stubStorage({ getItem: blockedStorage, setItem: blockedStorage });

    assert.doesNotThrow(() => saveTheme('dark'));
});

test('light and dark set the attribute', () => {
    applyTheme('light');
    assert.equal(dataset.theme, 'light');

    applyTheme('dark');
    assert.equal(dataset.theme, 'dark');
});

test('auto clears the attribute', () => {
    applyTheme('dark');
    applyTheme('auto');

    assert.equal('theme' in dataset, false);
});
