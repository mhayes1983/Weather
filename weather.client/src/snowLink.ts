/** A storm's dates and towns carried in the Snow Forecast page's address, so the same table can be shared. */
export interface SnowLink {
    start?: string;
    end?: string;
    zips?: string[];
}

const datePattern = /^\d{4}-\d{2}-\d{2}$/;
const zipPattern = /^\d{5}$/;

/** The server refuses more towns than this, so a longer list in a link is cut short. */
export const maxLinkTowns = 15;

/** Reads `?start=&end=&zips=`, leaving out anything that isn't a date or a list of ZIP codes. */
export function readSnowLink(search: string): SnowLink {
    const query = new URLSearchParams(search);
    const date = (name: string) => {
        const value = query.get(name);
        return value !== null && datePattern.test(value) ? value : undefined;
    };

    const listed = (query.get('zips') ?? '').split(',').map(zip => zip.trim()).filter(Boolean);
    const zips = [...new Set(listed)];
    const valid = zips.length > 0 && zips.every(zip => zipPattern.test(zip));

    return {
        start: date('start'),
        end: date('end'),
        zips: valid ? zips.slice(0, maxLinkTowns) : undefined,
    };
}

/** "/snow?start=2026-11-12&end=2026-11-13&zips=04086,04101". */
export function snowLinkPath(start: string, end: string, zips: string[]): string {
    return `/snow?start=${start}&end=${end}&zips=${zips.join(',')}`;
}

/** One source's hourly snow for one town, carried in the hourly page's address. */
export interface HourlyLink {
    zip: string;
    source: string;
    start: string;
    end: string;
}

/** Reads `?zip=&source=&start=&end=`, or undefined when any part is missing or malformed. */
export function readHourlyLink(search: string): HourlyLink | undefined {
    const query = new URLSearchParams(search);
    const zip = query.get('zip') ?? '';
    const source = query.get('source') ?? '';
    const start = query.get('start') ?? '';
    const end = query.get('end') ?? '';
    return zipPattern.test(zip) && /^[\w-]{1,40}$/.test(source) && datePattern.test(start) && datePattern.test(end) && end >= start
        ? { zip, source, start, end }
        : undefined;
}

/** "?zip=04086&source=icon&start=2026-11-12&end=2026-11-13", the search part of the hourly page's address. */
export function hourlyLinkSearch({ zip, source, start, end }: HourlyLink): string {
    return `?${new URLSearchParams({ zip, source, start, end })}`;
}
