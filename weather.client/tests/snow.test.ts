import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';
import { addDays, averageSnow, formatInches, isoDate } from '../src/snowAverages.ts';
import {
    addManualRow, clearManualValues, defaultManualRows, emptyManualSnow, hasManualValues, loadManualSnow, parseInches,
    removeManualRow, saveManualSnow, setManualValue,
} from '../src/snowManual.ts';
import { addSnowTown, loadSnowTowns, maxAddedTowns, removeSnowTown, resetSnowTowns, townZips } from '../src/snowTowns.ts';

const saved = new Map<string, string>();

beforeEach(() => {
    saved.clear();
    Object.assign(globalThis, {
        window: {
            localStorage: {
                getItem: (key: string) => saved.get(key) ?? null,
                setItem: (key: string, value: string) => saved.set(key, value),
            },
        },
    });
});

test('averages lows, highs and midpoints, leaving out partial and empty rows', () => {
    const average = averageSnow([
        { low: 1, high: 3 },
        { low: 2, high: 2 },
        { low: 0, high: 10, partial: true },
        { low: null, high: null },
        { low: null, high: 4 },
    ]);

    assert.deepEqual(average, { low: 7 / 3, high: 3, middle: (2 + 2 + 4) / 3, count: 3 });
});

test('an empty town has no average', () => {
    assert.deepEqual(averageSnow([{ low: null, high: null }]), { low: null, high: null, middle: null, count: 0 });
});

test('formats whole inches without a decimal', () => {
    assert.equal(formatInches(3), '3');
    assert.equal(formatInches(2.46), '2.5');
    assert.equal(formatInches(0.04), '0');
    assert.equal(formatInches(null), '');
});

test('dates step across months in local time', () => {
    assert.equal(isoDate(new Date(2026, 10, 7)), '2026-11-07');
    assert.equal(addDays('2026-11-30', 1), '2026-12-01');
    assert.equal(addDays('2026-11-01', -1), '2026-10-31');
});

test('manual values save, survive a reload and clear while keeping the rows', () => {
    let manual = setManualValue(emptyManualSnow(), 'wmtw', '04101', 'low', 1, '2026-11-13', '2026-11-14');
    manual = setManualValue(manual, 'wmtw', '04101', 'high', 3, '2026-11-15', '2026-11-16');
    saveManualSnow(manual);

    const reloaded = loadManualSnow();
    assert.deepEqual(reloaded.values, { wmtw: { '04101': { low: 1, high: 3 } } });
    assert.equal(reloaded.start, '2026-11-13');
    assert.ok(hasManualValues(reloaded));

    const cleared = clearManualValues(addManualRow(reloaded, 'WCSH'));
    assert.deepEqual(cleared.values, {});
    assert.equal(cleared.start, null);
    assert.equal(cleared.rows.length, defaultManualRows.length + 1);
    assert.ok(!hasManualValues(cleared));
});

test('removing a manual row drops its values', () => {
    const manual = setManualValue(emptyManualSnow(), 'wgme', '04011', 'low', 2, '2026-11-13', '2026-11-13');

    const removed = removeManualRow(manual, 'wgme');

    assert.ok(!removed.rows.some(row => row.id === 'wgme'));
    assert.deepEqual(removed.values, {});
});

test('manual input accepts only non-negative numbers', () => {
    assert.equal(parseInches('2.5'), 2.5);
    assert.equal(parseInches(''), null);
    assert.equal(parseInches('-1'), null);
});

test('unreadable manual storage starts with the default rows', () => {
    saved.set('snowManual', '{not json');

    assert.deepEqual(loadManualSnow(), emptyManualSnow());
});

test('towns are the configured ones not removed, then added ZIPs', () => {
    const configured = ['03904', '04101'];
    let choices = addSnowTown(configured, loadSnowTowns(), '99701');
    choices = removeSnowTown(configured, choices, '03904');

    assert.deepEqual(townZips(configured, choices), ['04101', '99701']);
    assert.deepEqual(loadSnowTowns(), choices);

    choices = addSnowTown(configured, choices, '03904');
    assert.deepEqual(townZips(configured, choices), ['03904', '04101', '99701']);

    choices = removeSnowTown(configured, choices, '99701');
    assert.deepEqual(townZips(configured, choices), configured);
    assert.deepEqual(resetSnowTowns(), { added: [], removed: [] });
});

test('a full added list refuses more towns', () => {
    let choices = loadSnowTowns();
    for (let index = 0; index < maxAddedTowns; index++) {
        choices = addSnowTown([], choices, String(10000 + index));
    }

    assert.equal(addSnowTown([], choices, '99999'), choices);
});
