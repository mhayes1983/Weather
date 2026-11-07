/**
 * Rows typed in by hand for outlets that publish snow only as maps (NewsCenter Maine, WMTW, WGME).
 * Saved in localStorage until cleared, so they survive reloads during a storm.
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

const storageKey = 'snowManual';

export const defaultManualRows: ManualRow[] = [
    { id: 'ncm', label: 'NewsCenter Maine' },
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

export function loadManualSnow(): ManualSnow {
    try {
        const saved = JSON.parse(window.localStorage.getItem(storageKey) ?? 'null') as Partial<ManualSnow> | null;
        if (!saved) {
            return emptyManualSnow();
        }

        return {
            rows: Array.isArray(saved.rows) ? saved.rows.filter(isRow) : defaultManualRows,
            values: typeof saved.values === 'object' && saved.values !== null ? saved.values : {},
            start: typeof saved.start === 'string' ? saved.start : null,
            end: typeof saved.end === 'string' ? saved.end : null,
        };
    } catch {
        return emptyManualSnow();
    }
}

export function saveManualSnow(manual: ManualSnow): ManualSnow {
    try {
        window.localStorage.setItem(storageKey, JSON.stringify(manual));
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
