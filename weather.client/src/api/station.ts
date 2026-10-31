export type StationStatus = 'Ok' | 'Offline' | 'NeedsKey' | 'Error';
export type StationHistoryRange = 'Last24Hours' | 'Today';

export interface StationReading {
    stationName: string | null;
    observedAt: string;
    temperatureF: number | null;
    feelsLikeF: number | null;
    dewPointF: number | null;
    humidityPercent: number | null;
    indoorTemperatureF: number | null;
    indoorHumidityPercent: number | null;
    windSpeedMph: number | null;
    windGustMph: number | null;
    windDirectionDegrees: number | null;
    maxDailyGustMph: number | null;
    pressureInHg: number | null;
    rainRateInPerHour: number | null;
    eventRainIn: number | null;
    dailyRainIn: number | null;
    weeklyRainIn: number | null;
    monthlyRainIn: number | null;
    yearlyRainIn: number | null;
    lastRain: string | null;
    uvIndex: number | null;
    solarRadiationWm2: number | null;
    batteryLow: boolean | null;
}

export interface StationHistoryPoint {
    time: string;
    temperatureF: number | null;
    windSpeedMph: number | null;
    windGustMph: number | null;
    pressureInHg: number | null;
    rainRateInPerHour: number | null;
}

export interface StationSummary {
    id: string;
    name: string;
}

export interface StationReport {
    status: StationStatus;
    message: string | null;
    generatedAt: string;
    source: string;
    current: StationReading | null;
    history: StationHistoryPoint[];
    historyRange: StationHistoryRange;
    historyMessage: string | null;
}

export interface StationMatch {
    id: string;
    name: string;
    distanceMi: number | null;
}

export async function lookUpStation(query: string, signal: AbortSignal): Promise<StationMatch> {
    const response = await fetch(`/api/stations/lookup?q=${encodeURIComponent(query)}`, { signal });
    if (!response.ok) {
        const problem = await response.json().catch(() => null) as { detail?: string } | null;
        throw new Error(problem?.detail ?? 'The station lookup failed.');
    }

    return await response.json() as StationMatch;
}

export async function fetchStations(signal: AbortSignal): Promise<StationSummary[]> {
    const response = await fetch('/api/stations', { signal });
    if (!response.ok) {
        throw new Error('The station list could not be loaded.');
    }

    return await response.json() as StationSummary[];
}

export async function fetchStation(id: string, signal: AbortSignal): Promise<StationReport> {
    const response = await fetch(`/api/station?id=${encodeURIComponent(id)}`, { signal });
    if (!response.ok) {
        throw new Error('The station data could not be loaded.');
    }

    return await response.json() as StationReport;
}
