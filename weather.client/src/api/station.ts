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
    // Only Ambient Weather stations fill these, and add-on sensors appear only when fitted.
    windSpeedAvg2mMph?: number | null;
    windDirectionAvg2mDegrees?: number | null;
    windSpeedAvg10mMph?: number | null;
    windDirectionAvg10mDegrees?: number | null;
    windGustDirectionDegrees?: number | null;
    rain24HourIn?: number | null;
    totalRainIn?: number | null;
    extraSensors?: ExtraSensor[];
    soilSensors?: SoilSensor[];
    leakSensors?: LeakSensor[];
    airQuality?: AirQuality | null;
    lightning?: Lightning | null;
    /** Names of other sensors whose batteries are low, such as "rain gauge". */
    lowBatteries?: string[];
}

export interface ExtraSensor {
    channel: number;
    temperatureF: number | null;
    humidityPercent: number | null;
    batteryLow: boolean | null;
}

export interface SoilSensor {
    channel: number;
    temperatureF: number | null;
    moisturePercent: number | null;
}

export interface LeakSensor {
    channel: number;
    leak: boolean;
}

export interface AirQuality {
    pm25: number | null;
    pm25Avg24h: number | null;
    aqi: number | null;
    indoorPm25: number | null;
    indoorCo2Ppm: number | null;
}

export interface Lightning {
    strikesToday: number | null;
    strikesLastHour: number | null;
    lastStrike: string | null;
    lastDistanceMi: number | null;
}

export interface StationHistoryPoint {
    time: string;
    temperatureF: number | null;
    windSpeedMph: number | null;
    windGustMph: number | null;
    pressureInHg: number | null;
    rainRateInPerHour: number | null;
    humidityPercent?: number | null;
    dewPointF?: number | null;
    uvIndex?: number | null;
    solarRadiationWm2?: number | null;
    indoorTemperatureF?: number | null;
}

export interface StationSummary {
    id: string;
    name: string;
    /** Starts the Temperature Forecast page on this ZIP. A looked-up station keeps the ZIP it was found by. */
    zip?: string | null;
}

export interface CurrentSky {
    conditions: string;
    icon: string;
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
    modelled: boolean;
    historyMaxGapMinutes: number;
    sky: CurrentSky | null;
}

export interface StationMatch {
    id: string;
    name: string;
    distanceMi: number | null;
}

export type StationLookupSource = 'nearest' | 'open-meteo';

export async function lookUpStation(query: string, source: StationLookupSource, signal: AbortSignal): Promise<StationMatch> {
    const sourceQuery = source === 'open-meteo' ? '&source=open-meteo' : '';
    const response = await fetch(`/api/stations/lookup?q=${encodeURIComponent(query)}${sourceQuery}`, { signal });
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
