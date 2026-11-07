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

export function fetchSnowAlerts(zips: string[], signal: AbortSignal) {
    const query = new URLSearchParams({ zips: zips.join(',') });
    return getJson<SnowAlertReport>(`/api/snow/alerts?${query}`, signal, 'Alerts are unavailable right now.');
}
