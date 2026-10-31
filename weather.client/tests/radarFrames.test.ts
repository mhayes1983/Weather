import assert from 'node:assert/strict';
import { test } from 'node:test';
import { selectFrames } from '../src/api/radarFrames.ts';

const scanMs = 120_000;
const scanCount = 60;
const start = Date.parse('2026-10-31T12:03:00.000Z');

function scan(index: number) {
    return new Date(start + index * scanMs).toISOString();
}

// The scans NOAA would list once `newScans` have arrived after the first list.
function scans(newScans = 0) {
    return Array.from({ length: scanCount }, (_, index) => scan(index + newScans));
}

test('returns nothing without scans', () => {
    assert.deepEqual(selectFrames([]), []);
});

test('shows a single scan', () => {
    assert.deepEqual(selectFrames([scan(0)]), [scan(0)]);
});

test('ends on the newest scan of a short list', () => {
    const times = Array.from({ length: 8 }, (_, index) => scan(index));

    assert.equal(selectFrames(times).at(-1), times.at(-1));
});

test('spans the two-hour list with about a dozen frames in order', () => {
    const frames = selectFrames(scans());

    assert.ok(frames.length >= 11 && frames.length <= 13, `${frames.length} frames`);
    assert.equal(frames.at(-1), scan(scanCount - 1));
    assert.ok(Date.parse(frames.at(-1)!) - Date.parse(frames[0]) >= 100 * 60_000);
    assert.deepEqual(frames, [...frames].sort());
});

test('never lists the newest scan twice', () => {
    const frames = selectFrames(scans());

    assert.equal(new Set(frames).size, frames.length);
});

test('keeps most frames as new scans arrive', () => {
    for (let offset = 0; offset < 30; offset++) {
        for (let newScans = 1; newScans <= 4; newScans++) {
            const before = selectFrames(scans(offset));
            const after = selectFrames(scans(offset + newScans));

            const kept = before.filter(frame => after.includes(frame));
            assert.ok(kept.length >= before.length - 2, `offset ${offset}, ${newScans} new: ${kept.length} of ${before.length}`);
        }
    }
});

test('keeps a frame until NOAA is about to drop it', () => {
    const held = selectFrames(scans()).at(-2)!;
    const heldIndex = scans().indexOf(held);

    // Stop one scan short of the frame reaching the end of NOAA's list.
    for (let newScans = 1; newScans < heldIndex; newScans++) {
        assert.ok(selectFrames(scans(newScans)).includes(held), `dropped after ${newScans} new scans`);
    }
});
