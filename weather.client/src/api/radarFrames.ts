const frameCount = 12;
const frameSlotMs = 600_000;

// NOAA keeps about two hours of scans, roughly two minutes apart. The first scan of
// each ten-minute slot never changes as new scans arrive, so frames and their loaded
// tiles last until NOAA drops them. The oldest slot is skipped because its first scan
// may already have gone, and the newest scan is added so the loop ends on current radar.
export function selectFrames(times: string[]): string[] {
    const slots = new Map<number, string>();
    for (const time of times) {
        const slot = Math.floor(Date.parse(time) / frameSlotMs);
        if (!slots.has(slot)) {
            slots.set(slot, time);
        }
    }

    const frames = [...slots.values()].slice(1).slice(-frameCount);
    const newest = times.at(-1);
    if (newest && !frames.includes(newest)) {
        frames.push(newest);
    }

    return frames;
}
