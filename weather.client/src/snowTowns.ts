/**
 * The towns this browser shows on the Snow Forecast page: the configured towns it hasn't removed,
 * then the ZIPs it added. Saved in localStorage, like saved stations.
 */
export const maxAddedTowns = 8;

const storageKey = 'snowTowns';

export interface SnowTownChoices {
    added: string[];
    removed: string[];
}

const empty: SnowTownChoices = { added: [], removed: [] };

function zipList(value: unknown): string[] {
    return Array.isArray(value) ? value.filter((zip): zip is string => typeof zip === 'string' && /^\d{5}$/.test(zip)) : [];
}

export function loadSnowTowns(): SnowTownChoices {
    try {
        const saved = JSON.parse(window.localStorage.getItem(storageKey) ?? 'null') as Partial<SnowTownChoices> | null;
        return saved ? { added: zipList(saved.added), removed: zipList(saved.removed) } : empty;
    } catch {
        return empty;
    }
}

function save(choices: SnowTownChoices) {
    try {
        window.localStorage.setItem(storageKey, JSON.stringify(choices));
    } catch {
        // Without storage the choice still applies until the page reloads.
    }

    return choices;
}

export function townZips(configured: string[], choices: SnowTownChoices): string[] {
    const shown = configured.filter(zip => !choices.removed.includes(zip));
    return [...shown, ...choices.added.filter(zip => !shown.includes(zip))];
}

/** Adding a removed configured town restores it. A full added list stays as it is. */
export function addSnowTown(configured: string[], choices: SnowTownChoices, zip: string): SnowTownChoices {
    if (configured.includes(zip)) {
        return save({ ...choices, removed: choices.removed.filter(other => other !== zip) });
    }

    if (choices.added.includes(zip) || choices.added.length >= maxAddedTowns) {
        return choices;
    }

    return save({ ...choices, added: [...choices.added, zip] });
}

export function removeSnowTown(configured: string[], choices: SnowTownChoices, zip: string): SnowTownChoices {
    return save(configured.includes(zip)
        ? { ...choices, removed: [...choices.removed.filter(other => other !== zip), zip] }
        : { ...choices, added: choices.added.filter(other => other !== zip) });
}

export function resetSnowTowns(): SnowTownChoices {
    return save(empty);
}
