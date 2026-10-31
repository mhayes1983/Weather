export type StationStatus = 'Ok' | 'Offline' | 'NeedsKey' | 'Error';

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

export interface StationReport {
    status: StationStatus;
    message: string | null;
    generatedAt: string;
    current: StationReading | null;
    history: StationHistoryPoint[];
    historyMessage: string | null;
}

export async function fetchStation(signal: AbortSignal): Promise<StationReport> {
    const response = await fetch('/api/station', { signal });
    if (!response.ok) {
        throw new Error('The station data could not be loaded.');
    }

    return await response.json() as StationReport;
}
