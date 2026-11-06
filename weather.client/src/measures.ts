export type Measure = 'low' | 'high';

/** Each source shows its low before its high. */
export const measures: Measure[] = ['low', 'high'];

export const measureLabels: Record<Measure, string> = {
    low: 'Low',
    high: 'High',
};

/** Shows or hides one measure, keeping low before high. The last one shown stays, so the table is never empty. */
export function toggleMeasure(shown: Measure[], measure: Measure): Measure[] {
    if (!shown.includes(measure)) {
        return measures.filter(other => other === measure || shown.includes(other));
    }

    return shown.length > 1 ? shown.filter(other => other !== measure) : shown;
}
