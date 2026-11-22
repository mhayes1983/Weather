import { sourceTitle } from '../sourceDescriptions';
import { useRef, useState, type PointerEvent } from 'react';
import type { Anomaly, ForecastCell, ForecastColumn, ForecastRow } from '../api/forecast';
import { consensus, consensusRange, consensusValue } from '../consensus';
import { measureLabels, type Measure } from '../measures';
import { displayTemperature, type Unit } from '../temperature';

interface ForecastTableProps {
    columns: ForecastColumn[];
    rows: ForecastRow[];
    measures: Measure[];
    unit: Unit;
    onMoveColumn: (id: string, before: string | null) => void;
}

interface ColumnDrag {
    id: string;
    startX: number;
    moved: boolean;
    // The column to drop in front of, null for the end, undefined for no change.
    dropBefore: string | null | undefined;
}

const dragThreshold = 5;

// Widths in rem. Source names are at most 5 characters (SourceColumns in Core), and the full name is in the hover text.
const dateColumnRem = 6;
const sourceColumnRem = (measures: number) => Math.max(4.5, measures * 2.75);

const consensusTitle = 'The average of the sources shown, with the lowest and highest beneath. Hiding a source leaves it out.';

const anomalyClasses: Record<Anomaly, string | undefined> = {
    None: undefined,
    Above: 'anomaly-above',
    Below: 'anomaly-below',
};

function measureValue(cell: ForecastCell, measure: Measure) {
    return measure === 'high'
        ? { valueF: cell.highF, anomaly: cell.highAnomaly }
        : { valueF: cell.lowF, anomaly: cell.lowAnomaly };
}

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
    return sourceTitle(column.id, [column.message, updated].filter(Boolean).join('. ') || null);
}

function averageTitle(row: ForecastRow, unit: Unit) {
    const parts = [
        row.averageHighF === null ? null : `high ${displayTemperature(row.averageHighF, unit)}°${unit}`,
        row.averageLowF === null ? null : `low ${displayTemperature(row.averageLowF, unit)}°${unit}`,
    ].filter(Boolean);
    return parts.length === 0 ? undefined : `Average ${parts.join(', ')}`;
}

function findDropTarget(headerRow: HTMLTableRowElement, id: string, x: number) {
    const headers = [...headerRow.querySelectorAll<HTMLElement>('.column-header')];
    const index = headers.findIndex(header => header.dataset.columnId === id);
    const target = headers
        .filter((_, other) => other !== index)
        .find(header => {
            const { left, width } = header.getBoundingClientRect();
            return x < left + width / 2;
        });

    const before = target?.dataset.columnId ?? null;
    const next = headers[index + 1]?.dataset.columnId ?? null;
    return before === next ? undefined : before;
}

function ForecastTable({ columns, rows, measures, unit, onMoveColumn }: ForecastTableProps) {
    const headerRow = useRef<HTMLTableRowElement>(null);
    const [drag, setDrag] = useState<ColumnDrag>();

    function handlePointerDown(event: PointerEvent<HTMLElement>, id: string) {
        if (event.button === 0) {
            event.currentTarget.setPointerCapture(event.pointerId);
            setDrag({ id, startX: event.clientX, moved: false, dropBefore: undefined });
        }
    }

    function handlePointerMove(event: PointerEvent<HTMLElement>) {
        if (drag && headerRow.current && (drag.moved || Math.abs(event.clientX - drag.startX) >= dragThreshold)) {
            setDrag({ ...drag, moved: true, dropBefore: findDropTarget(headerRow.current, drag.id, event.clientX) });
        }
    }

    function handlePointerUp() {
        if (drag && drag.dropBefore !== undefined) {
            onMoveColumn(drag.id, drag.dropBefore);
        }
        setDrag(undefined);
    }

    // Each source spans one cell per measure, so the drop markers go on its outer edges.
    function columnClasses(column: ForecastColumn, index: number, first: boolean, last: boolean) {
        const classes = first ? ['group-start'] : [];
        if (drag?.moved) {
            if (column.id === drag.id) {
                classes.push('column-dragging');
            } else if (column.id === drag.dropBefore && first) {
                classes.push('drop-before');
            } else if (drag.dropBefore === null && index === columns.length - 1 && last) {
                classes.push('drop-after');
            }
        }
        return classes;
    }

    // Columns in error have no values, so they don't count towards a consensus.
    const columnIds = columns.map(column => column.id);
    const showConsensus = rows.some(row => measures.some(measure => consensus(row, columnIds, measure) !== null));

    return (
        <div className="table-scroll">
            {/* A fixed layout gives every source the same width, whatever its name. The minimum width makes narrow
                screens scroll instead of squeezing the columns. */}
            <table className="forecast-table" style={{ minWidth: `${dateColumnRem + (columns.length + (showConsensus ? 1 : 0)) * sourceColumnRem(measures.length)}rem` }}>
                <colgroup>
                    <col style={{ width: `${dateColumnRem}rem` }} />
                    {showConsensus && measures.map(measure => <col key={`consensus-${measure}`} />)}
                    {columns.map(column => measures.map(measure => <col key={`${column.id}-${measure}`} />))}
                </colgroup>
                <thead>
                    <tr ref={headerRow}>
                        <th scope="col" rowSpan={2} className="date-column">Date</th>
                        {showConsensus &&
                            <th scope="colgroup" colSpan={measures.length} className="group-start" title={consensusTitle}>
                                Consensus
                            </th>}
                        {columns.map((column, index) =>
                            <th
                                key={column.id}
                                scope="colgroup"
                                colSpan={measures.length}
                                className={['column-header', ...columnClasses(column, index, true, true)].join(' ')}
                                title={columnTitle(column)}
                                data-column-id={column.id}
                                onPointerDown={event => handlePointerDown(event, column.id)}
                                onPointerMove={handlePointerMove}
                                onPointerUp={handlePointerUp}
                                onPointerCancel={() => setDrag(undefined)}
                            >
                                {column.label}
                                {column.message !== null &&
                                    <span className={`column-status status-${column.status.toLowerCase()}`}>
                                        {column.status === 'NeedsKey' ? 'needs key' : 'error'}
                                    </span>}
                            </th>
                        )}
                    </tr>
                    <tr>
                        {showConsensus && measures.map((measure, position) =>
                            <th key={measure} scope="col" className={position === 0 ? 'measure-header group-start' : 'measure-header'}>
                                {measureLabels[measure]}
                            </th>
                        )}
                        {columns.map((column, index) => measures.map((measure, position) =>
                            <th
                                key={`${column.id}-${measure}`}
                                scope="col"
                                className={['measure-header', ...columnClasses(column, index, position === 0, position === measures.length - 1)].join(' ')}
                            >
                                {measureLabels[measure]}
                            </th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {rows.map(row => {
                        const date = formatDate(row.date);
                        return (
                            <tr key={row.date}>
                                <th scope="row" className="date-column" title={averageTitle(row, unit)}>
                                    <span className="weekday">{date.weekday}</span> {date.short}
                                </th>
                                {showConsensus && measures.map((measure, position) => {
                                    const value = consensus(row, columnIds, measure);
                                    const range = value && consensusRange(value, unit);
                                    return (
                                        <td
                                            key={measure}
                                            className={position === 0 ? 'consensus-cell group-start' : 'consensus-cell'}
                                            title={value ? `From ${value.count} sources` : undefined}
                                        >
                                            {value && consensusValue(value, unit)}
                                            {range && <span className="consensus-range">{range}</span>}
                                        </td>
                                    );
                                })}
                                {columns.map((column, index) => {
                                    const cell = row.cells[column.id] as ForecastCell | undefined;
                                    return measures.map((measure, position) => {
                                        const value = cell && measureValue(cell, measure);
                                        const className = [
                                            value && anomalyClasses[value.anomaly],
                                            ...columnClasses(column, index, position === 0, position === measures.length - 1),
                                        ].filter(Boolean).join(' ');
                                        return (
                                            <td key={`${column.id}-${measure}`} className={className || undefined}>
                                                {value && value.valueF !== null && displayTemperature(value.valueF, unit)}
                                            </td>
                                        );
                                    });
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
