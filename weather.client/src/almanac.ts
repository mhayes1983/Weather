import { displayTemperature, type Unit } from './temperature.ts';

const missing = '–';

// Core sends a trace as a tiny amount, so it isn't confused with none.
const largestTraceIn = 0.005;

export function almanacTemperature(fahrenheit: number | null, unit: Unit): string {
    return fahrenheit === null ? missing : `${displayTemperature(fahrenheit, unit)}°`;
}

export function almanacAmount(inches: number | null, digits: number): string {
    if (inches === null) {
        return missing;
    }

    return inches > 0 && inches < largestTraceIn ? 'Trace' : `${inches.toFixed(digits)} in`;
}

/**
 * "6° above average", or null when either value is missing. Worked out from the rounded values
 * the table shows, so the difference always matches them.
 */
export function almanacDeparture(fahrenheit: number | null, averageF: number | null, unit: Unit): string | null {
    if (fahrenheit === null || averageF === null) {
        return null;
    }

    const difference = Math.round(displayTemperature(fahrenheit, unit)) - Math.round(displayTemperature(averageF, unit));
    return difference === 0
        ? 'at the average'
        : `${Math.abs(difference)}° ${difference > 0 ? 'above' : 'below'} average`;
}

// Built from its parts, so no time zone shifts the day.
function localDate(date: string): Date {
    const [year, month, day] = date.split('-').map(Number);
    return new Date(year, month - 1, day);
}

/** "2026-11-06" as "November 6". */
export function almanacDate(date: string): string {
    return localDate(date).toLocaleDateString('en-US', { month: 'long', day: 'numeric' });
}

/** "2026-11-06" as "11/6", for a tab. */
export function almanacTabLabel(date: string): string {
    const parsed = localDate(date);
    return `${parsed.getMonth() + 1}/${parsed.getDate()}`;
}

/** The tab shown: the one chosen if it's still there, otherwise today. */
export function selectedAlmanacDate(dates: string[], chosen: string | undefined, today: string | null): string | undefined {
    if (chosen !== undefined && dates.includes(chosen)) {
        return chosen;
    }

    return today !== null && dates.includes(today) ? today : dates[0];
}

/** ACIS station names are upper case, such as "PORTLAND JETPORT". */
export function climateStationName(name: string): string {
    return name.toLowerCase().replace(/\b\p{L}/gu, letter => letter.toUpperCase());
}
