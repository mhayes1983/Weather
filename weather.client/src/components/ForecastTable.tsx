import { useRef, useState, type PointerEvent } from 'react';
import type { Anomaly, ForecastColumn, ForecastRow } from '../api/forecast';
import { displayTemperature, type Unit } from '../temperature';

interface ForecastTableProps {
    columns: ForecastColumn[];
    rows: ForecastRow[];
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

function ForecastTable({ columns, rows, unit, onMoveColumn }: ForecastTableProps) {
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

    function columnClass(column: ForecastColumn, index: number) {
        if (!drag?.moved) {
            return undefined;
        }
        if (column.id === drag.id) {
            return 'column-dragging';
        }
        if (column.id === drag.dropBefore) {
            return 'drop-before';
        }
        return drag.dropBefore === null && index === columns.length - 1 ? 'drop-after' : undefined;
    }

    return (
        <div className="table-scroll">
            <table>
                <thead>
                    <tr ref={headerRow}>
                        <th scope="col" className="date-column">Date</th>
                        {columns.map((column, index) =>
                            <th
                                key={column.id}
                                scope="col"
                                className={['column-header', columnClass(column, index)].filter(Boolean).join(' ')}
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
                                {columns.map((column, index) => {
                                    const cell = row.cells[column.id];
                                    const className = [cell && anomalyClasses[cell.anomaly], columnClass(column, index)].filter(Boolean).join(' ');
                                    return (
                                        <td key={column.id} className={className || undefined}>
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
