export type Measure = 'low' | 'high';

/** Each source shows its low before its high. */
export const measures: Measure[] = ['low', 'high'];

export const measureLabels: Record<Measure, string> = {
    low: 'Low',
    high: 'High',
};
