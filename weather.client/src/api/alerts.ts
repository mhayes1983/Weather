import type { SourceStatus } from './forecast';

export interface WeatherAlert {
    id: string;
    event: string;
    severity: string;
    headline: string | null;
    description: string | null;
    instruction: string | null;
    starts: string | null;
    ends: string | null;
}

export interface AlertReport {
    stationName: string;
    status: SourceStatus;
    message: string | null;
    alerts: WeatherAlert[];
}

export async function fetchAlerts(station: string, signal: AbortSignal): Promise<AlertReport> {
    const response = await fetch(`/api/alerts?station=${encodeURIComponent(station)}`, { signal });
    if (!response.ok) {
        throw new Error('Alerts are unavailable.');
    }

    return await response.json() as AlertReport;
}
