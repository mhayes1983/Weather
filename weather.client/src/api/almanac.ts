import type { SourceStatus } from './forecast';

/** Temperatures are °F and amounts inches. `value` is the forecast for today and later, and what was measured before today. */
export interface AlmanacRow {
    value: number | null;
    average: number | null;
    record: number | null;
    recordYear: number | null;
}

export interface AlmanacDay {
    date: string;
    high: AlmanacRow;
    low: AlmanacRow;
    precip: AlmanacRow;
    snow: AlmanacRow;
}

export interface AlmanacReport {
    stationName: string;
    status: SourceStatus;
    message: string | null;
    climateStation: string | null;
    recordsSince: number | null;
    today: string | null;
    /** Yesterday, today and tomorrow, in date order. */
    days: AlmanacDay[];
}

export async function fetchAlmanac(station: string, signal: AbortSignal): Promise<AlmanacReport> {
    const response = await fetch(`/api/almanac?station=${encodeURIComponent(station)}`, { signal });
    if (!response.ok) {
        throw new Error('The almanac could not be loaded.');
    }

    return await response.json() as AlmanacReport;
}
