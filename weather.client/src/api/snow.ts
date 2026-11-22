import type { WeatherAlert } from './alerts';
import type { SourceStatus } from './forecast';

export interface SnowTownSummary {
    zip: string;
    name: string;
}

export interface SnowTown extends SnowTownSummary {
    /** Why the town has no values, such as an unknown ZIP. */
    message: string | null;
}

export interface SnowSourceRow {
    id: string;
    label: string;
    status: SourceStatus;
    message: string | null;
    fetchedAt: string | null;
}

export interface SnowCell {
    sourceId: string;
    zip: string;
    lowInches: number | null;
    highInches: number | null;
    /** The source's forecast doesn't cover the whole range, so the total is a minimum. */
    partial: boolean;
    status: SourceStatus;
    message: string | null;
    /** When the source first and last has snow falling on these dates, in the town's local time. */
    snowStart: string | null;
    snowEnd: string | null;
    /** Null for a source that doesn't forecast ice. */
    iceInches: number | null;
    /** The snow from `snowStart` to `snowEnd`. */
    timingLowInches: number | null;
    timingHighInches: number | null;
    /** The heaviest period: an hour for hourly sources, a 6 to 12-hour block for NWS and Weather Underground. */
    peakStart: string | null;
    peakEnd: string | null;
    peakInchesPerHour: number | null;
}

/** Snow expected from `start` to `end`, in the town's local time. */
export interface SnowPeriod {
    start: string;
    end: string;
    lowInches: number;
    highInches: number;
}

export interface SnowHourlySource {
    id: string;
    label: string;
    status: SourceStatus;
    message: string | null;
    periods: SnowPeriod[];
    /** Null for a source that doesn't forecast ice. */
    ice: SnowPeriod[] | null;
    /** The forecast doesn't cover all of the dates, so its total is a minimum. */
    partial: boolean;
}

/** Every source's periods for one town, trimmed to the dates. `now` is the town's local time. */
export interface SnowHourly {
    zip: string;
    name: string;
    /** Why the town has no sources, such as an unknown ZIP. */
    message: string | null;
    start: string;
    end: string;
    now: string;
    sources: SnowHourlySource[];
}

/** A snow total someone reported to the NWS. `observedAt` is in the town's local time. */
export interface SnowReport {
    place: string;
    inches: number;
    observedAt: string;
    distanceMiles: number;
    source: string | null;
    estimated: boolean;
}

export interface SnowTownReports {
    zip: string;
    lowInches: number | null;
    highInches: number | null;
    reports: SnowReport[];
}

/** Towns are missing until their dates begin, and when their reports couldn't be loaded. */
export interface SnowReportTable {
    status: SourceStatus;
    message: string | null;
    radiusMiles: number;
    towns: SnowTownReports[];
}

export interface SnowTable {
    start: string;
    end: string;
    generatedAt: string;
    towns: SnowTown[];
    sources: SnowSourceRow[];
    cells: SnowCell[];
}

export interface SnowAlert {
    alert: WeatherAlert;
    zips: string[];
}

export interface SnowAlertReport {
    status: SourceStatus;
    message: string | null;
    alerts: SnowAlert[];
}

async function getJson<T>(url: string, signal: AbortSignal, fallback: string): Promise<T> {
    const response = await fetch(url, { signal });
    if (!response.ok) {
        const problem = await response.json().catch(() => null) as { detail?: string } | null;
        throw new Error(problem?.detail ?? fallback);
    }

    return await response.json() as T;
}

export function fetchSnowTowns(signal: AbortSignal) {
    return getJson<SnowTownSummary[]>('/api/snow/towns', signal, 'The town list could not be loaded.');
}

export function fetchSnow(zips: string[], start: string, end: string, signal: AbortSignal) {
    const query = new URLSearchParams({ zips: zips.join(','), start, end });
    return getJson<SnowTable>(`/api/snow?${query}`, signal, 'The snow forecast could not be loaded.');
}

export function fetchSnowHourly(zip: string, start: string, end: string, signal: AbortSignal) {
    const query = new URLSearchParams({ zip, start, end });
    return getJson<SnowHourly>(`/api/snow/hourly?${query}`, signal, 'The hourly snow forecast could not be loaded.');
}

export function fetchSnowReports(zips: string[], start: string, end: string, signal: AbortSignal) {
    const query = new URLSearchParams({ zips: zips.join(','), start, end });
    return getJson<SnowReportTable>(`/api/snow/reports?${query}`, signal, 'Snow reports are unavailable right now.');
}

export function fetchSnowAlerts(zips: string[], signal: AbortSignal) {
    const query = new URLSearchParams({ zips: zips.join(',') });
    return getJson<SnowAlertReport>(`/api/snow/alerts?${query}`, signal, 'Alerts are unavailable right now.');
}
