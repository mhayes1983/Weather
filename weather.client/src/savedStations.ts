import type { StationSummary } from './api/station.ts';

export const maxSavedStations = 20;

const storageKey = 'savedStations';

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

/** A full list stays as it is, so the user chooses which station to remove. */
export function addSavedStation(stations: StationSummary[], station: StationSummary): StationSummary[] {
    const others = stations.filter(other => other.id !== station.id);
    return others.length < maxSavedStations ? save([...others, station]) : stations;
}

export function removeSavedStation(stations: StationSummary[], id: string): StationSummary[] {
    return save(stations.filter(other => other.id !== id));
}

/** Each tab keeps its own copy, which would otherwise overwrite another tab's changes. */
export function watchSavedStations(onChange: (stations: StationSummary[]) => void) {
    function handleStorage(event: StorageEvent) {
        if (event.key === storageKey || event.key === null) {
            onChange(loadSavedStations());
        }
    }

    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
}
