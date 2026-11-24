/**
 * The Storm Forecast page's views. They all come from the same loaded data, so switching never calls the server.
 * Rain is liquid: rain plus melted snow, because only Open-Meteo forecasts rain on its own.
 */
export type StormView = 'snow' | 'rain' | 'wind' | 'depth';

export const stormViews: StormView[] = ['snow', 'rain', 'wind', 'depth'];

export interface StormViewInfo {
    /** The toggle's label. */
    label: string;
    /** What the table's first column heading says. */
    heading: string;
    unit: 'in' | 'mph';
    /** Decimal places shown for amounts. */
    digits: number;
    /** The rate unit on timing bars and the hourly page. */
    rateUnit: string;
    /** "snow falling", "rain falling" or "gusts of 30 mph or more", for sentences about timing. */
    active: string;
}

export const viewInfo: Record<StormView, StormViewInfo> = {
    snow: { label: 'Snow', heading: 'Snow (inches)', unit: 'in', digits: 1, rateUnit: 'in/hr', active: 'snow falling' },
    rain: { label: 'Rain', heading: 'Rain and melted snow (inches)', unit: 'in', digits: 2, rateUnit: 'in/hr', active: 'rain falling' },
    wind: { label: 'Wind', heading: 'Strongest gust (mph)', unit: 'mph', digits: 0, rateUnit: 'mph', active: 'gusts of 30 mph or more' },
    depth: { label: 'Depth', heading: 'Snow on the ground (inches)', unit: 'in', digits: 1, rateUnit: 'in', active: 'snow on the ground' },
};

/** A view from an address, or snow, so links from before the other views keep working. */
export function readView(value: string | null | undefined): StormView {
    return stormViews.find(view => view === value) ?? 'snow';
}

/** The towns' storm reports for a view. Depth has none: nothing measured is sent to the NWS for it. */
export function reportTowns<T>(reports: { towns: T[]; rainTowns: T[] | null; windTowns: T[] | null } | undefined, view: StormView): T[] {
    return view === 'snow' ? reports?.towns ?? []
        : view === 'rain' ? reports?.rainTowns ?? []
            : view === 'wind' ? reports?.windTowns ?? []
                : [];
}

/** An amount in a view's units: whole values without a decimal, otherwise to the view's digits. Empty for null. */
export function formatAmount(value: number | null | undefined, view: StormView): string {
    if (value === null || value === undefined) {
        return '';
    }

    const digits = viewInfo[view].digits;
    const scale = 10 ** digits;
    const rounded = Math.round(value * scale) / scale;
    return Number.isInteger(rounded) ? rounded.toFixed(0) : rounded.toFixed(digits);
}
