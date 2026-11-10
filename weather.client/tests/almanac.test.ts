import assert from 'node:assert/strict';
import { test } from 'node:test';
import { almanacAmount, almanacDate, almanacDeparture, almanacTabLabel, almanacTemperature, climateStationName, selectedAlmanacDate } from '../src/almanac.ts';

test('temperatures follow the unit and missing ones show a dash', () => {
    assert.equal(almanacTemperature(79, 'F'), '79°');
    assert.equal(almanacTemperature(79, 'C'), '26°');
    assert.equal(almanacTemperature(null, 'F'), '–');
});

test('a trace is not shown as none', () => {
    assert.equal(almanacAmount(0.001, 2), 'Trace');
    assert.equal(almanacAmount(0, 2), '0.00 in');
    assert.equal(almanacAmount(1.9, 2), '1.90 in');
    assert.equal(almanacAmount(2, 1), '2.0 in');
    assert.equal(almanacAmount(null, 1), '–');
});

test('dates keep their day in any time zone', () => {
    assert.equal(almanacDate('2026-11-06'), 'November 6');
    assert.equal(almanacDate('2026-01-01'), 'January 1');
});

test('climate station names are title case', () => {
    assert.equal(climateStationName('PORTLAND JETPORT'), 'Portland Jetport');
    assert.equal(climateStationName('DURHAM'), 'Durham');
});

test('tabs are labelled month/day', () => {
    assert.equal(almanacTabLabel('2026-11-05'), '11/5');
    assert.equal(almanacTabLabel('2026-12-31'), '12/31');
});

test('today is selected until another tab is chosen', () => {
    const dates = ['2026-11-05', '2026-11-06', '2026-11-07'];
    assert.equal(selectedAlmanacDate(dates, undefined, '2026-11-06'), '2026-11-06');
    assert.equal(selectedAlmanacDate(dates, '2026-11-07', '2026-11-06'), '2026-11-07');
});

test('a chosen date that has gone falls back to today', () => {
    const dates = ['2026-11-06', '2026-11-07', '2026-11-08'];
    assert.equal(selectedAlmanacDate(dates, '2026-11-05', '2026-11-07'), '2026-11-07');
    assert.equal(selectedAlmanacDate(dates, undefined, null), '2026-11-06');
});

test('a departure is the difference between the values shown', () => {
    assert.equal(almanacDeparture(56, 50, 'F'), '6° above average');
    assert.equal(almanacDeparture(48, 50.4, 'F'), '2° below average');
    assert.equal(almanacDeparture(50, 50, 'F'), 'at the average');
    assert.equal(almanacDeparture(59, 50, 'C'), '5° above average');
    assert.equal(almanacDeparture(null, 50, 'F'), null);
    assert.equal(almanacDeparture(50, null, 'F'), null);
});
