export type SourceStatus = 'Ok' | 'NeedsKey' | 'Error';
export type Anomaly = 'None' | 'Above' | 'Below';

export interface ForecastCell {
    highF: number | null;
    highAnomaly: Anomaly;
    lowF: number | null;
    lowAnomaly: Anomaly;
}

export interface ForecastRow {
    date: string;
    averageHighF: number | null;
    averageLowF: number | null;
    cells: Record<string, ForecastCell>;
}

export interface ForecastColumn {
    id: string;
    label: string;
    status: SourceStatus;
    message: string | null;
    fetchedAt: string | null;
}

export interface AverageRange {
    startF: number;
    endF: number;
    station: string;
}

export interface ForecastTable {
    location: {
        zip: string;
        place: string;
        state: string;
    };
    generatedAt: string;
    averageHigh: AverageRange | null;
    averageLow: AverageRange | null;
    averageMessage: string | null;
    aboveAverageF: number;
    belowAverageF: number;
    columns: ForecastColumn[];
    rows: ForecastRow[];
}

export async function fetchForecast(zip: string, signal: AbortSignal): Promise<ForecastTable> {
    const response = await fetch(`/api/forecast?zip=${encodeURIComponent(zip)}`, { signal });
    if (!response.ok) {
        const problem = await response.json().catch(() => null) as { detail?: string } | null;
        throw new Error(problem?.detail ?? 'The forecast could not be loaded.');
    }

    return await response.json() as ForecastTable;
}
