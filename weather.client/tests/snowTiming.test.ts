import assert from 'node:assert/strict';
import { test } from 'node:test';
import { hourlyLinkSearch, maxLinkTowns, readHourlyLink, readSnowLink, snowLinkPath } from '../src/snowLink.ts';
import {
    agreementByHour, cumulativeByHour, datesBetween, dayLabel, hourAt, hourlyRate, inchesText, intensity, localDayTime, overlapInches,
    periodSegments, spanSummary, timingSpan, timingText, townNow,
} from '../src/snowTiming.ts';

test('times keep the town\'s clock whatever the browser\'s time zone', () => {
    assert.equal(localDayTime('2026-11-12T19:00:00-05:00'), 'Thu 7 PM');
    assert.equal(localDayTime('2026-11-12T07:30:00-08:00'), 'Thu 7:30 AM');
    assert.equal(localDayTime('2026-11-13T00:00:00-05:00'), 'Fri 12 AM');
    assert.equal(localDayTime('2026-11-13T12:00:00-05:00'), 'Fri 12 PM');
});

test('a span names its second day only when it differs', () => {
    assert.equal(timingText('2026-11-12T19:00:00-05:00', '2026-11-13T09:00:00-05:00'), 'Thu 7 PM – Fri 9 AM');
    assert.equal(timingText('2026-11-12T13:00:00-05:00', '2026-11-12T23:00:00-05:00'), 'Thu 1 PM – 11 PM');
});

test('a span ending at midnight ends on the day before', () => {
    assert.equal(timingText('2026-11-12T13:00:00-05:00', '2026-11-13T00:00:00-05:00'), 'Thu 1 PM – midnight');
    assert.equal(timingText('2026-11-12T13:00:00-05:00', '2026-11-14T00:00:00-05:00'), 'Thu 1 PM – Fri midnight');
});

test('a span is placed between the first and last dates', () => {
    assert.deepEqual(timingSpan('2026-11-12T12:00:00-05:00', '2026-11-13T00:00:00-05:00', '2026-11-12', '2026-11-13'), { left: 25, width: 25 });
    assert.deepEqual(timingSpan('2026-11-12T00:00:00-05:00', '2026-11-14T00:00:00-05:00', '2026-11-12', '2026-11-13'), { left: 0, width: 100 });
});

test('a span outside the dates is clipped', () => {
    assert.deepEqual(timingSpan('2026-11-11T12:00:00-05:00', '2026-11-12T06:00:00-05:00', '2026-11-12', '2026-11-12'), { left: 0, width: 25 });
    assert.deepEqual(timingSpan('2026-11-14T00:00:00-05:00', '2026-11-14T06:00:00-05:00', '2026-11-12', '2026-11-12'), { left: 100, width: 0 });
});

test('dates run from the first to the last across a month end', () => {
    assert.deepEqual(datesBetween('2026-11-29', '2026-12-01'), ['2026-11-29', '2026-11-30', '2026-12-01']);
    assert.deepEqual(datesBetween('2026-11-12', '2026-11-12'), ['2026-11-12']);
    assert.equal(dayLabel('2026-11-05'), 'Thu 11/5');
});

test('a link gives its dates and towns', () => {
    assert.deepEqual(readSnowLink('?start=2026-11-12&end=2026-11-13&zips=04086,04101'), {
        start: '2026-11-12',
        end: '2026-11-13',
        zips: ['04086', '04101'],
    });
    assert.equal(snowLinkPath('2026-11-12', '2026-11-13', ['04086', '04101']), '/snow?start=2026-11-12&end=2026-11-13&zips=04086,04101');
    assert.deepEqual(readSnowLink(snowLinkPath('2026-11-12', '2026-11-13', ['04086']).slice('/snow'.length)).zips, ['04086']);
});

test('anything in a link that is not a date or ZIP codes is left out', () => {
    assert.deepEqual(readSnowLink(''), { start: undefined, end: undefined, zips: undefined });
    assert.deepEqual(readSnowLink('?start=tomorrow&end=2026-11-13&zips=04086,abc'), { start: undefined, end: '2026-11-13', zips: undefined });
    assert.deepEqual(readSnowLink('?zips=<script>').zips, undefined);
});

test('a link\'s towns are listed once and capped', () => {
    assert.deepEqual(readSnowLink('?zips=04086, 04086,04101').zips, ['04086', '04101']);

    const many = Array.from({ length: 20 }, (_, index) => String(10000 + index));
    assert.equal(readSnowLink(`?zips=${many.join(',')}`).zips?.length, maxLinkTowns);
});

const hour = (start: string, end: string, highInches: number, lowInches = highInches) => ({ start, end, lowInches, highInches });

test('snowfall rates are light, moderate or heavy', () => {
    assert.equal(intensity(0.1), 'light');
    assert.equal(intensity(0.25), 'moderate');
    assert.equal(intensity(0.99), 'moderate');
    assert.equal(intensity(1), 'heavy');
});

test('segments leave out flurries and keep each rate', () => {
    const segments = periodSegments([
        hour('2026-11-12T06:00:00-05:00', '2026-11-12T07:00:00-05:00', 0.005),
        hour('2026-11-12T12:00:00-05:00', '2026-11-13T00:00:00-05:00', 6),
    ], '2026-11-12', '2026-11-13');

    assert.equal(segments.length, 1);
    assert.deepEqual({ left: segments[0].left, width: segments[0].width, rate: segments[0].rate }, { left: 25, width: 25, rate: 0.5 });
});

test('agreement counts each source once an hour, across block and hourly sources', () => {
    const counts = agreementByHour([
        [hour('2026-11-12T00:00:00-05:00', '2026-11-12T12:00:00-05:00', 2)],
        [hour('2026-11-12T03:00:00-05:00', '2026-11-12T04:00:00-05:00', 0.3), hour('2026-11-12T03:30:00-05:00', '2026-11-12T04:30:00-05:00', 0.3)],
        [hour('2026-11-12T05:00:00-05:00', '2026-11-12T06:00:00-05:00', 0.001)],
    ], '2026-11-12', '2026-11-12');

    assert.equal(counts.length, 24);
    assert.deepEqual(counts.slice(0, 6), [1, 1, 1, 2, 2, 1]);
    assert.equal(counts[12], 0);
});

test('the running total shares a period that crosses midnight and stops after the last one', () => {
    const values = cumulativeByHour([hour('2026-11-12T22:00:00-05:00', '2026-11-13T02:00:00-05:00', 4)], '2026-11-12', '2026-11-13');

    assert.equal(values.length, 49);
    assert.equal(values[0], 0);
    assert.equal(values[23], 1);
    assert.equal(values[24], 2);
    assert.equal(values[26], 4);
    assert.equal(values[27], null);
});

test('hours count from the first midnight in wall-clock time', () => {
    assert.equal(hourAt('2026-11-12', 0), '2026-11-12T00:00');
    assert.equal(hourAt('2026-11-12', 30), '2026-11-13T06:00');
});

test('amounts read as one value when the low and high agree', () => {
    assert.equal(inchesText(1.5, 2), '1.5–2 in');
    assert.equal(inchesText(2, 2), '2 in');
    assert.equal(inchesText(0.04, 0.05, 2), '0.04–0.05 in');
});

test('a bar summary gives its total and its heaviest hour or block', () => {
    const cell = {
        snowStart: '2026-11-12T19:00:00-05:00', snowEnd: '2026-11-13T01:00:00-05:00',
        timingLowInches: 1.5, timingHighInches: 2,
        peakStart: '2026-11-12T20:00:00-05:00', peakEnd: '2026-11-12T21:00:00-05:00', peakInchesPerHour: 0.8,
    };

    assert.equal(spanSummary(cell), 'Thu 7 PM – Fri 1 AM: 1.5–2 in · heaviest 0.8 in/hr Thu 8 PM');
    assert.equal(
        spanSummary({ ...cell, peakStart: '2026-11-12T19:00:00-05:00', peakEnd: '2026-11-13T07:00:00-05:00', peakInchesPerHour: 0.17 }),
        'Thu 7 PM – Fri 1 AM: 1.5–2 in · heaviest 0.17 in/hr on average, Thu 7 PM – Fri 7 AM');
    assert.equal(spanSummary({ ...cell, snowStart: null }), '');
});

test('an hourly link needs a town, source and dates', () => {
    const link = { zip: '04240', source: 'ecmwf-aifs', start: '2026-11-26', end: '2026-11-28' };

    assert.deepEqual(readHourlyLink(hourlyLinkSearch(link)), link);
    assert.equal(readHourlyLink('?zip=04240&source=icon&start=2026-11-28&end=2026-11-26'), undefined);
    assert.equal(readHourlyLink('?zip=4240&source=icon&start=2026-11-26&end=2026-11-26'), undefined);
    assert.equal(readHourlyLink('?zip=04240&start=2026-11-26&end=2026-11-26'), undefined);
});

test('ice blocks are shared between the snow periods they cross, so the rows add up to the total', () => {
    const ice = [
        { start: '2026-11-27T01:00:00-05:00', end: '2026-11-27T07:00:00-05:00', lowInches: 0.12, highInches: 0.12 },
        { start: '2026-11-27T07:00:00-05:00', end: '2026-11-27T13:00:00-05:00', lowInches: 0.06, highInches: 0.06 },
    ];
    const night = ['2026-11-26T18:00:00-05:00', '2026-11-27T06:00:00-05:00'];
    const day = ['2026-11-27T06:00:00-05:00', '2026-11-27T18:00:00-05:00'];

    const share = (from: string, to: string) => ice.reduce((sum, part) => sum + overlapInches(part, from, to), 0);

    assert.equal(share(night[0], night[1]).toFixed(2), '0.10');
    assert.equal(share(day[0], day[1]).toFixed(2), '0.08');
    assert.equal((share(night[0], night[1]) + share(day[0], day[1])).toFixed(2), '0.18');
});

test('rates use the real hours across a clock change', () => {
    // 1 AM to 3 AM on the spring change is one real hour.
    assert.equal(hourlyRate({ start: '2027-03-14T01:00:00-05:00', end: '2027-03-14T03:00:00-04:00', lowInches: 1, highInches: 1 }), 1);
    assert.equal(hourlyRate({ start: '2026-11-12T01:00:00-05:00', end: '2026-11-12T03:00:00-05:00', lowInches: 1, highInches: 1 }), 0.5);
});

test('the town clock keeps moving from the offset the server gave', () => {
    const later = Date.parse('2026-11-26T20:30:00Z');
    assert.equal(townNow('2026-11-26T13:05:00-05:00', later), '2026-11-26T15:30:00-05:00');
    assert.equal(townNow('2026-07-01T09:00:00+00:00', Date.parse('2026-07-01T10:00:00Z')), '2026-07-01T10:00:00+00:00');
});
