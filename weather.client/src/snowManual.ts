/**
 * Rows typed in by hand for outlets that publish snow, rain or gusts only as maps or on air (NewsCenter Maine,
 * WMTW, WGME). Saved in localStorage until cleared, so they survive reloads during a storm. Wind values are mph.
 */
export interface ManualRow {
    id: string;
    label: string;
}

export interface ManualValue {
    low: number | null;
    high: number | null;
}

export interface ManualSnow {
    rows: ManualRow[];
    /** Values by row ID, then ZIP. */
    values: Record<string, Record<string, ManualValue>>;
    /** The date range the values were entered for. */
    start: string | null;
    end: string | null;
}

/** The views with manual rows. Depth is modelled only, so it has none. */
export type ManualView = 'snow' | 'rain' | 'wind';

/** Each view keeps its own rows and values. Snow's key is the one it had before the other views. */
export const manualStorageKeys: Record<ManualView, string> = {
    snow: 'snowManual',
    rain: 'rainManual',
    wind: 'windManual',
};

export const defaultManualRows: ManualRow[] = [
    { id: 'ncm', label: 'NCM' },
    { id: 'wmtw', label: 'WMTW' },
    { id: 'wgme', label: 'WGME' },
];

export function emptyManualSnow(rows: ManualRow[] = defaultManualRows): ManualSnow {
    return { rows, values: {}, start: null, end: null };
}

function isRow(value: unknown): value is ManualRow {
    const row = value as ManualRow | null;
    return typeof row?.id === 'string' && typeof row.label === 'string';
}

// The NewsCenter Maine row was saved as "NewsCenter Maine" before the tables switched to short names. A name the
// person typed themselves is kept.
const oldDefaultLabels: Record<string, string> = { ncm: 'NewsCenter Maine' };

function renameOldDefault(row: ManualRow): ManualRow {
    const current = defaultManualRows.find(fallback => fallback.id === row.id);
    return current && row.label === oldDefaultLabels[row.id] ? { ...row, label: current.label } : row;
}

export function loadManualSnow(view: ManualView = 'snow'): ManualSnow {
    try {
        const saved = JSON.parse(window.localStorage.getItem(manualStorageKeys[view]) ?? 'null') as Partial<ManualSnow> | null;
        if (!saved) {
            return emptyManualSnow();
        }

        return {
            rows: Array.isArray(saved.rows) ? saved.rows.filter(isRow).map(renameOldDefault) : defaultManualRows,
            values: typeof saved.values === 'object' && saved.values !== null ? saved.values : {},
            start: typeof saved.start === 'string' ? saved.start : null,
            end: typeof saved.end === 'string' ? saved.end : null,
        };
    } catch {
        return emptyManualSnow();
    }
}

export function saveManualSnow(manual: ManualSnow, view: ManualView = 'snow'): ManualSnow {
    try {
        window.localStorage.setItem(manualStorageKeys[view], JSON.stringify(manual));
    } catch {
        // Without storage the values still apply until the page reloads.
    }

    return manual;
}

/** Empty or unreadable text clears the value. Negative amounts aren't allowed. */
export function parseInches(text: string): number | null {
    const value = Number.parseFloat(text);
    return Number.isFinite(value) && value >= 0 ? value : null;
}

export function setManualValue(
    manual: ManualSnow, rowId: string, zip: string, part: keyof ManualValue, value: number | null, start: string, end: string,
): ManualSnow {
    const row = manual.values[rowId] ?? {};
    const current = row[zip] ?? { low: null, high: null };
    return {
        ...manual,
        values: { ...manual.values, [rowId]: { ...row, [zip]: { ...current, [part]: value } } },
        start: manual.start ?? start,
        end: manual.end ?? end,
    };
}

/** Clears every value for the next storm, keeping the rows. */
export function clearManualValues(manual: ManualSnow): ManualSnow {
    return emptyManualSnow(manual.rows);
}

export function hasManualValues(manual: ManualSnow): boolean {
    return Object.values(manual.values).some(row =>
        Object.values(row).some(value => value.low !== null || value.high !== null));
}

export function addManualRow(manual: ManualSnow, label: string): ManualSnow {
    const id = `manual-${Date.now().toString(36)}`;
    return { ...manual, rows: [...manual.rows, { id, label }] };
}

export function renameManualRow(manual: ManualSnow, id: string, label: string): ManualSnow {
    return { ...manual, rows: manual.rows.map(row => row.id === id ? { ...row, label } : row) };
}

export function removeManualRow(manual: ManualSnow, id: string): ManualSnow {
    const values = { ...manual.values };
    delete values[id];
    return { ...manual, rows: manual.rows.filter(row => row.id !== id), values };
}
