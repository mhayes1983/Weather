import type { SourceStatus } from './forecast';

export interface ForecastDay {
    date: string;
    conditions: string;
    icon: string;
    highF: number | null;
    lowF: number | null;
    precipChancePercent: number | null;
    precipIn: number | null;
    snowIn: number | null;
    windMph: number | null;
    windDirectionDegrees: number | null;
    uvIndex: number | null;
    sunrise: string | null;
    sunset: string | null;
    hasHourly: boolean;
}

export interface ForecastHour {
    time: string;
    conditions: string;
    icon: string;
    temperatureF: number | null;
    feelsLikeF: number | null;
    precipChancePercent: number | null;
    precipIn: number | null;
    snowIn: number | null;
    cloudCoverPercent: number | null;
    dewPointF: number | null;
    humidityPercent: number | null;
    windMph: number | null;
    windDirectionDegrees: number | null;
    pressureInHg: number | null;
}

export interface PeriodPrecip {
    chancePercent: number | null;
    amountIn: number | null;
    snowIn: number | null;
}

export interface MoonInfo {
    phase: string | null;
    illuminationPercent: number | null;
    moonrise: string | null;
    moonset: string | null;
}

export interface DailyForecastReport {
    stationName: string;
    status: SourceStatus;
    message: string | null;
    today: string | null;
    days: ForecastDay[];
}

export interface ForecastDayDetail {
    stationName: string;
    day: ForecastDay;
    dayDescription: string | null;
    nightDescription: string | null;
    dayPrecip: PeriodPrecip | null;
    nightPrecip: PeriodPrecip | null;
    moon: MoonInfo | null;
    hours: ForecastHour[];
    previousDate: string | null;
    nextDate: string | null;
}

async function fetchJson<T>(url: string, signal: AbortSignal, fallback: string): Promise<T> {
    const response = await fetch(url, { signal });
    if (!response.ok) {
        const problem = await response.json().catch(() => null) as { detail?: string } | null;
        throw new Error(problem?.detail ?? fallback);
    }

    return await response.json() as T;
}

export function fetchDailyForecast(station: string, signal: AbortSignal): Promise<DailyForecastReport> {
    return fetchJson(`/api/daily-forecast?station=${encodeURIComponent(station)}`, signal, 'The daily forecast could not be loaded.');
}

export function fetchForecastDay(date: string, station: string, signal: AbortSignal): Promise<ForecastDayDetail> {
    return fetchJson(`/api/daily-forecast/${date}?station=${encodeURIComponent(station)}`, signal, 'That day could not be loaded.');
}
