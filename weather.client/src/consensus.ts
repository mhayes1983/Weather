import type { ForecastRow } from './api/forecast.ts';
import type { Measure } from './measures.ts';
import { displayTemperature, type Unit } from './temperature.ts';

/** What the sources shown agree on for one date: their mean, and the lowest and highest of them. All °F. */
export interface Consensus {
    meanF: number;
    minF: number;
    maxF: number;
    count: number;
}

const fewestSources = 2;

/**
 * Worked out here and not in Core because hiding a column takes it out.
 * Returns null with fewer than two values, because one source isn't a consensus.
 */
export function consensus(row: ForecastRow, columnIds: string[], measure: Measure): Consensus | null {
    const values = columnIds.flatMap(id => {
        const cell = row.cells[id] as ForecastRow['cells'][string] | undefined;
        const value = measure === 'high' ? cell?.highF : cell?.lowF;
        return value === null || value === undefined ? [] : [value];
    });

    if (values.length < fewestSources) {
        return null;
    }

    return {
        meanF: values.reduce((sum, value) => sum + value, 0) / values.length,
        minF: Math.min(...values),
        maxF: Math.max(...values),
        count: values.length,
    };
}

export function consensusValue(value: Consensus, unit: Unit): number {
    return Math.round(displayTemperature(value.meanF, unit));
}

/** "48–55", or null when every source agrees. */
export function consensusRange(value: Consensus, unit: Unit): string | null {
    const low = Math.round(displayTemperature(value.minF, unit));
    const high = Math.round(displayTemperature(value.maxF, unit));
    return low === high ? null : `${low}–${high}`;
}
