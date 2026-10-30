import type { Anomaly, ForecastColumn, ForecastRow } from '../api/forecast';
import { displayTemperature, type Unit } from '../temperature';

interface ForecastTableProps {
    columns: ForecastColumn[];
    rows: ForecastRow[];
    unit: Unit;
}

const anomalyClasses: Record<Anomaly, string | undefined> = {
    None: undefined,
    Above: 'anomaly-above',
    Below: 'anomaly-below',
};

function formatDate(date: string) {
    const [year, month, day] = date.split('-').map(Number);
    const value = new Date(year, month - 1, day);
    return {
        weekday: value.toLocaleDateString('en-US', { weekday: 'short' }),
        short: `${month}/${day}`,
    };
}

function columnTitle(column: ForecastColumn) {
    const updated = column.fetchedAt === null
        ? null
        : `Updated ${new Date(column.fetchedAt).toLocaleString()}`;
    return [column.message, updated].filter(Boolean).join('. ');
}

function ForecastTable({ columns, rows, unit }: ForecastTableProps) {
    return (
        <div className="table-scroll">
            <table>
                <thead>
                    <tr>
                        <th scope="col" className="date-column">Date</th>
                        {columns.map(column =>
                            <th key={column.id} scope="col" title={columnTitle(column)}>
                                {column.label}
                                {column.message !== null &&
                                    <span className={`column-status status-${column.status.toLowerCase()}`}>
                                        {column.status === 'NeedsKey' ? 'needs key' : 'error'}
                                    </span>}
                            </th>
                        )}
                    </tr>
                </thead>
                <tbody>
                    {rows.map(row => {
                        const date = formatDate(row.date);
                        return (
                            <tr key={row.date}>
                                <th
                                    scope="row"
                                    className="date-column"
                                    title={row.averageF === null ? undefined : `Average high ${displayTemperature(row.averageF, unit)}°${unit}`}
                                >
                                    <span className="weekday">{date.weekday}</span> {date.short}
                                </th>
                                {columns.map(column => {
                                    const cell = row.cells[column.id];
                                    return (
                                        <td key={column.id} className={cell && anomalyClasses[cell.anomaly]}>
                                            {cell && displayTemperature(cell.highF, unit)}
                                        </td>
                                    );
                                })}
                            </tr>
                        );
                    })}
                </tbody>
            </table>
        </div>
    );
}

export default ForecastTable;
