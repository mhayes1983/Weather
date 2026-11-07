import type { StationSummary } from './api/station.ts';

const openMeteoId = /^om-(\d{5})$/;

/** The ZIP the Temperature and Snow Forecast pages starts on for a station, or the home ZIP when it has none. */
export function stationZip(id: string, saved: StationSummary[], listed: StationSummary[], homeZip: string): string {
    return saved.find(station => station.id === id)?.zip
        ?? listed.find(station => station.id === id)?.zip
        ?? openMeteoId.exec(id)?.[1]
        ?? homeZip;
}
