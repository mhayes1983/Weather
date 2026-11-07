/** A value in a town's Low and High columns, from a source or typed in. */
export interface SnowRange {
    low: number | null;
    high: number | null;
    partial?: boolean;
}

export interface SnowAverage {
    low: number | null;
    high: number | null;
    /** The mean of each row's midpoint, like the spreadsheet's single Average row. */
    middle: number | null;
    count: number;
}

function mean(values: number[]) {
    return values.length ? values.reduce((sum, value) => sum + value, 0) / values.length : null;
}

/**
 * Averages a town's rows. Partial totals stop when a source's forecast does, so they'd pull the
 * average down and are left out. A row with only one of low or high uses it for both.
 */
export function averageSnow(ranges: SnowRange[]): SnowAverage {
    const complete = ranges
        .filter(range => !range.partial && (range.low !== null || range.high !== null))
        .map(range => ({ low: range.low ?? range.high!, high: range.high ?? range.low! }));

    return {
        low: mean(complete.map(range => range.low)),
        high: mean(complete.map(range => range.high)),
        middle: mean(complete.map(range => (range.low + range.high) / 2)),
        count: complete.length,
    };
}

/** Whole inches without a decimal, otherwise one decimal place. */
export function formatInches(value: number | null): string {
    if (value === null) {
        return '';
    }

    const rounded = Math.round(value * 10) / 10;
    return Number.isInteger(rounded) ? rounded.toFixed(0) : rounded.toFixed(1);
}

/** Dates as YYYY-MM-DD in the browser's time zone. */
export function isoDate(date: Date): string {
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
}

export function addDays(iso: string, days: number): string {
    const [year, month, day] = iso.split('-').map(Number);
    return isoDate(new Date(year, month - 1, day + days));
}
