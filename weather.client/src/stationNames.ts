/** Names a browser gives stations, by station ID. They're display only and stay in that browser. */
export type StationNames = Record<string, string>;

export const maxStationNameLength = 40;

const storageKey = 'stationNames';

export function loadStationNames(): StationNames {
    try {
        const saved: unknown = JSON.parse(window.localStorage.getItem(storageKey) ?? '{}');
        if (typeof saved !== 'object' || saved === null || Array.isArray(saved)) {
            return {};
        }

        return Object.fromEntries(Object.entries(saved).filter((entry): entry is [string, string] => entry[0] !== '__proto__' && typeof entry[1] === 'string' && entry[1].trim() !== ''));
    } catch {
        return {};
    }
}

/** A blank name, or null, goes back to the station's own name. */
export function renameStation(names: StationNames, id: string, name: string | null): StationNames {
    const trimmed = name?.trim().slice(0, maxStationNameLength) ?? '';
    const next = Object.fromEntries(Object.entries(names).filter(([other]) => other !== id));
    if (trimmed !== '' && id !== '__proto__') {
        next[id] = trimmed;
    }

    try {
        window.localStorage.setItem(storageKey, JSON.stringify(next));
    } catch {
        // Without storage the name still applies until the page reloads.
    }

    return next;
}

export function stationName(names: StationNames, id: string, fallback: string): string;
export function stationName(names: StationNames, id: string, fallback: string | undefined): string | undefined;
export function stationName(names: StationNames, id: string, fallback: string | undefined) {
    return Object.hasOwn(names, id) ? names[id] : fallback;
}

/** Each tab keeps its own copy, which would otherwise overwrite another tab's changes. */
export function watchStationNames(onChange: (names: StationNames) => void) {
    function handleStorage(event: StorageEvent) {
        if (event.key === storageKey || event.key === null) {
            onChange(loadStationNames());
        }
    }

    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
}
