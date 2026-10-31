import type { ForecastColumn } from '../api/forecast';

interface ColumnFiltersProps {
    columns: ForecastColumn[];
    hidden: string[];
    onToggle: (id: string) => void;
}

function ColumnFilters({ columns, hidden, onToggle }: ColumnFiltersProps) {
    const shown = columns.filter(column => !hidden.includes(column.id)).length;

    return (
        <details className="column-filters">
            <summary>Data sources ({shown} of {columns.length})</summary>
            <div className="column-filter-list">
                {columns.map(column =>
                    <label key={column.id}>
                        <input
                            type="checkbox"
                            checked={!hidden.includes(column.id)}
                            onChange={() => onToggle(column.id)}
                        />
                        {column.label}
                    </label>
                )}
            </div>
        </details>
    );
}

export default ColumnFilters;
