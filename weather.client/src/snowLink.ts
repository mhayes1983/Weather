import { readView, type StormView } from './stormViews.ts';

/** A storm's dates, towns and view carried in the Storm Forecast page's address, so the same table can be shared. */
export interface SnowLink {
    start?: string;
    end?: string;
    zips?: string[];
    /** Snow when the link names none, as links from before the other views do. */
    view: StormView;
}

const datePattern = /^\d{4}-\d{2}-\d{2}$/;
const zipPattern = /^\d{5}$/;

/** The server refuses more towns than this, so a longer list in a link is cut short. */
export const maxLinkTowns = 15;

/** Reads `?start=&end=&zips=&type=`, leaving out anything that isn't a date, a list of ZIP codes or a view. */
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
        view: readView(query.get('type')),
    };
}

/** "/storm?start=2026-11-12&end=2026-11-13&zips=04086,04101", with "&type=rain" for any view but snow. */
export function snowLinkPath(start: string, end: string, zips: string[], view: StormView = 'snow'): string {
    const type = view === 'snow' ? '' : `&type=${view}`;
    return `/storm?start=${start}&end=${end}&zips=${zips.join(',')}${type}`;
}

/** One source's hourly forecast for one town, carried in the hourly page's address. */
export interface HourlyLink {
    zip: string;
    source: string;
    start: string;
    end: string;
    view: StormView;
}

/** Reads `?zip=&source=&start=&end=&type=`, or undefined when any part but the view is missing or malformed. */
export function readHourlyLink(search: string): HourlyLink | undefined {
    const query = new URLSearchParams(search);
    const zip = query.get('zip') ?? '';
    const source = query.get('source') ?? '';
    const start = query.get('start') ?? '';
    const end = query.get('end') ?? '';
    return zipPattern.test(zip) && /^[\w-]{1,40}$/.test(source) && datePattern.test(start) && datePattern.test(end) && end >= start
        ? { zip, source, start, end, view: readView(query.get('type')) }
        : undefined;
}

/**
 * "?zip=04086&source=icon&start=2026-11-12&end=2026-11-13", the search part of the hourly page's address, with
 * "&type=rain" for any view but snow.
 */
export function hourlyLinkSearch({ zip, source, start, end, view = 'snow' }: Omit<HourlyLink, 'view'> & { view?: StormView }): string {
    const query = new URLSearchParams({ zip, source, start, end });
    if (view !== 'snow') {
        query.set('type', view);
    }

    return `?${query}`;
}
