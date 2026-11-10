import assert from 'node:assert/strict';
import { test } from 'node:test';
import { maxLinkTowns, readSnowLink, snowLinkPath } from '../src/snowLink.ts';
import { datesBetween, dayLabel, localDayTime, timingSpan, timingText } from '../src/snowTiming.ts';

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
