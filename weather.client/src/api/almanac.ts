import type { MoonInfo } from './dailyForecast';
import type { Tide } from '../almanac';
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
    /** Null when sun and moon times couldn't be loaded. */
    sky: AlmanacSky | null;
    /** Local highs and lows. Null when there's no tide station nearby or they couldn't be loaded. */
    tides: Tide[] | null;
}

/** Local times as "HH:mm". The moon doesn't rise or set every day. */
export interface AlmanacSky {
    sunrise: string | null;
    sunset: string | null;
    /** Null when USNO couldn't be reached, leaving the forecast's sun times. */
    moon: MoonInfo | null;
    /** Sunset minus sunrise as shown. */
    daylightMinutes: number | null;
    /** From the day before; negative when the days are getting shorter. */
    daylightChangeMinutes: number | null;
    /** Since the last solstice: negative after the June one, positive after the December one. */
    daylightSinceSolsticeMinutes: number | null;
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
    /** The NOAA station the tides are predicted for, or null when none is nearby. */
    tideStation: string | null;
}

export async function fetchAlmanac(station: string, signal: AbortSignal): Promise<AlmanacReport> {
    const response = await fetch(`/api/almanac?station=${encodeURIComponent(station)}`, { signal });
    if (!response.ok) {
        throw new Error('The almanac could not be loaded.');
    }

    return await response.json() as AlmanacReport;
}
