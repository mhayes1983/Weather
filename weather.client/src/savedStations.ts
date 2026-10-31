import type { StationSummary } from './api/station';

const storageKey = 'savedStations';
const maxStations = 20;

export function loadSavedStations(): StationSummary[] {
    try {
        const saved: unknown = JSON.parse(window.localStorage.getItem(storageKey) ?? '[]');
        return Array.isArray(saved) ? saved as StationSummary[] : [];
    } catch {
        return [];
    }
}

function save(stations: StationSummary[]) {
    try {
        window.localStorage.setItem(storageKey, JSON.stringify(stations));
    } catch {
        // Without storage the list still applies until the page reloads.
    }

    return stations;
}

export function addSavedStation(station: StationSummary): StationSummary[] {
    const others = loadSavedStations().filter(other => other.id !== station.id);
    return save([...others, station].slice(-maxStations));
}

export function removeSavedStation(id: string): StationSummary[] {
    return save(loadSavedStations().filter(other => other.id !== id));
}
