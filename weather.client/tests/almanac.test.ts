import assert from 'node:assert/strict';
import { test } from 'node:test';
import { almanacAmount, almanacDate, almanacDeparture, almanacDuration, almanacTabLabel, almanacTemperature, climateStationName, daylightChange, selectedAlmanacDate, tideRows } from '../src/almanac.ts';

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

test('daylight shows hours and minutes', () => {
    assert.equal(almanacDuration(555), '9h 15m');
    assert.equal(almanacDuration(600), '10h 0m');
    assert.equal(almanacDuration(4), '4m');
});

test('daylight change says which way it went', () => {
    assert.deepEqual(daylightChange(-2), ['Daylight decrease', '2m']);
    assert.deepEqual(daylightChange(3), ['Daylight increase', '3m']);
    assert.deepEqual(daylightChange(0), ['Daylight change', 'Under 1m']);
});

test('total daylight change since the solstice says which way it went', () => {
    assert.deepEqual(daylightChange(-313, true), ['Total daylight decrease', '5h 13m']);
    assert.deepEqual(daylightChange(45, true), ['Total daylight increase', '45m']);
    assert.deepEqual(daylightChange(0, true), ['Total daylight change', 'Under 1m']);
});

test('tides group into AM and PM highs and lows', () => {
    assert.deepEqual(tideRows([
        { time: '00:23', high: true, heightFt: 4 },
        { time: '08:18', high: false, heightFt: 0 },
        { time: '12:31', high: true, heightFt: 4.7 },
        { time: '21:00', high: false, heightFt: -0.5 },
    ]), [
        ['AM high', '12:23 AM, 4.0 ft'],
        ['AM low', '8:18 AM, 0.0 ft'],
        ['PM high', '12:31 PM, 4.7 ft'],
        ['PM low', '9:00 PM, -0.5 ft'],
    ]);
});

test('a half-day can have two of one tide and none of the other', () => {
    assert.deepEqual(tideRows([
        { time: '00:05', high: false, heightFt: 0.2 },
        { time: '06:10', high: true, heightFt: 9.1 },
        { time: '11:58', high: false, heightFt: 0.4 },
        { time: '18:30', high: true, heightFt: 9.4 },
    ]), [
        ['AM high', '6:10 AM, 9.1 ft'],
        ['AM low', '12:05 AM, 0.2 ft; 11:58 AM, 0.4 ft'],
        ['PM high', '6:30 PM, 9.4 ft'],
    ]);
    assert.deepEqual(tideRows([]), []);
});
