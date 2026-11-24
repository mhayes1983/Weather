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
    /** The views this source has data for. Every source has snow and rain; only some have wind or depth. */
    views: string[] | null;
}

/** A source's snow, or liquid, for one town over the whole range. */
export interface SnowCell {
    sourceId: string;
    zip: string;
    lowInches: number | null;
    highInches: number | null;
    /** The source's forecast doesn't cover the whole range, so the total is a minimum. */
    partial: boolean;
    status: SourceStatus;
    message: string | null;
    /** When the source first and last has snow (or rain) falling on these dates, in the town's local time. */
    snowStart: string | null;
    snowEnd: string | null;
    /** Null for a source that doesn't forecast ice, and always for rain. */
    iceInches: number | null;
    /** The amount from `snowStart` to `snowEnd`. */
    timingLowInches: number | null;
    timingHighInches: number | null;
    /** The heaviest period: an hour for hourly sources, a 6 to 12-hour block for NWS and Weather Underground. */
    peakStart: string | null;
    peakEnd: string | null;
    peakInchesPerHour: number | null;
    /** Snow over liquid, such as 10 for 10:1. Null when there's too little liquid or either total is partial. */
    ratio: number | null;
    /** Snow, but far more liquid than it accounts for, so rain or a mix is likely. */
    mixed: boolean;
}

/** A source's strongest gust for one town, in mph, and when gusts reach 30 mph, in the town's local time. */
export interface WindCell {
    sourceId: string;
    zip: string;
    maxMph: number | null;
    partial: boolean;
    status: SourceStatus;
    message: string | null;
    windyStart: string | null;
    windyEnd: string | null;
    peakStart: string | null;
    peakEnd: string | null;
}

/** A model's snow on the ground for one town, in inches. Modelled, never measured. */
export interface DepthCell {
    sourceId: string;
    zip: string;
    nowInches: number | null;
    maxInches: number | null;
    maxAt: string | null;
    endInches: number | null;
    partial: boolean;
    status: SourceStatus;
    message: string | null;
}

/** Snow, or liquid, expected from `start` to `end`, in the town's local time. */
export interface SnowPeriod {
    start: string;
    end: string;
    lowInches: number;
    highInches: number;
}

/** The strongest gust from `start` to `end`, in mph. */
export interface GustPeriod {
    start: string;
    end: string;
    mph: number;
}

/** Modelled snow on the ground at `time`, in inches. */
export interface DepthPoint {
    time: string;
    inches: number;
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
    /** Rain plus melted snow. Null for a source without it. */
    liquid: SnowPeriod[] | null;
    liquidPartial: boolean;
    gusts: GustPeriod[] | null;
    gustsPartial: boolean;
    depth: DepthPoint[] | null;
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

/** A snow or rain total in inches, or a gust in mph, reported to the NWS. `observedAt` is in the town's local time. */
export interface SnowReport {
    place: string;
    amount: number;
    observedAt: string;
    distanceMiles: number;
    source: string | null;
    estimated: boolean;
}

export interface SnowTownReports {
    zip: string;
    low: number | null;
    high: number | null;
    reports: SnowReport[];
}

/** Towns are missing until their dates begin, and when their reports couldn't be loaded. */
export interface SnowReportTable {
    status: SourceStatus;
    message: string | null;
    radiusMiles: number;
    /** Snow reports, for the storm's dates once they've begun. */
    towns: SnowTownReports[];
    /** Rain reports from `rainSince` through today, whatever the storm's dates, newest first. */
    rainTowns: SnowTownReports[] | null;
    windTowns: SnowTownReports[] | null;
    rainSince: string | null;
}

/** Every view's cells, so switching views needs no more calls. */
export interface SnowTable {
    start: string;
    end: string;
    generatedAt: string;
    towns: SnowTown[];
    sources: SnowSourceRow[];
    cells: SnowCell[];
    rainCells: SnowCell[] | null;
    windCells: WindCell[] | null;
    depthCells: DepthCell[] | null;
}

export type StormAlertKind = 'Winter' | 'Flood' | 'Wind';

export interface SnowAlert {
    alert: WeatherAlert;
    zips: string[];
    kind: StormAlertKind;
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
    return getJson<SnowTable>(`/api/snow?${query}`, signal, 'The storm forecast could not be loaded.');
}

export function fetchSnowHourly(zip: string, start: string, end: string, signal: AbortSignal) {
    const query = new URLSearchParams({ zip, start, end });
    return getJson<SnowHourly>(`/api/snow/hourly?${query}`, signal, 'The hourly forecast could not be loaded.');
}

export function fetchSnowReports(zips: string[], start: string, end: string, signal: AbortSignal) {
    const query = new URLSearchParams({ zips: zips.join(','), start, end });
    return getJson<SnowReportTable>(`/api/snow/reports?${query}`, signal, 'Storm reports are unavailable right now.');
}

export function fetchSnowAlerts(zips: string[], signal: AbortSignal) {
    const query = new URLSearchParams({ zips: zips.join(',') });
    return getJson<SnowAlertReport>(`/api/snow/alerts?${query}`, signal, 'Alerts are unavailable right now.');
}
