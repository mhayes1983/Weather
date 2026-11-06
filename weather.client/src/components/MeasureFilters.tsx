import { measureLabels, measures, type Measure } from '../measures';

interface MeasureFiltersProps {
    shown: Measure[];
    onToggle: (measure: Measure) => void;
}

function MeasureFilters({ shown, onToggle }: MeasureFiltersProps) {
    return (
        <fieldset className="measure-filters">
            <legend>Show</legend>
            {measures.map(measure => {
                const checked = shown.includes(measure);
                return (
                    <label key={measure}>
                        <input
                            type="checkbox"
                            checked={checked}
                            // The table always shows at least one of the two.
                            disabled={checked && shown.length === 1}
                            onChange={() => onToggle(measure)}
                        />
                        {measureLabels[measure]}
                    </label>
                );
            })}
        </fieldset>
    );
}

export default MeasureFilters;
