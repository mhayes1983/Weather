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
