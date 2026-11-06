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

// Map layers are kept per mode and image, because two modes can share scan times.
// An animated mode's image is its scan time, or 'latest' before times have loaded.
// A static mode's image is its version, which changes once per refresh interval.
export function mapLayerKey(modeId: string, time: string | undefined, version?: number) {
    return `${modeId}|${time ?? (version === undefined ? 'latest' : `v${version}`)}`;
}

// The version a static image should be on at `now`. Asking again within the same
// interval gives the same version, so nothing reloads, such as when a tab is refocused.
export function imageVersion(refreshMs: number, now = Date.now()) {
    return Math.floor(now / refreshMs);
}

// Once the shown layer has loaded, every other layer goes, except the loop's frames
// while it plays.
export function keepLayer(layerKey: string, shownKey: string, playing: boolean, frameKeys: string[]) {
    return layerKey === shownKey || (playing && frameKeys.includes(layerKey));
}
