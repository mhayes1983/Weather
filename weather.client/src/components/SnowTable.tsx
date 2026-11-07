import { sourceTitle } from '../sourceDescriptions';
import type { SnowCell, SnowSourceRow, SnowTown } from '../api/snow';
import { averageSnow, formatInches, type SnowRange } from '../snowAverages';
import { parseInches, type ManualRow, type ManualSnow, type ManualValue } from '../snowManual';

interface SnowTableProps {
    towns: SnowTown[];
    sources: SnowSourceRow[];
    cells: SnowCell[];
    manualRows: ManualRow[];
    manual: ManualSnow;
    /** Changes when values are cleared, so the inputs start empty again. */
    manualGeneration: number;
    onManualChange: (rowId: string, zip: string, part: keyof ManualValue, value: number | null) => void;
    onManualRename: (rowId: string, label: string) => void;
    onManualRemove: (rowId: string) => void;
}

function cellText(value: number | null, partial: boolean) {
    const text = formatInches(value);
    return text && partial ? `≥${text}` : text;
}

function SnowTable({ towns, sources, cells, manualRows, manual, manualGeneration, onManualChange, onManualRename, onManualRemove }: SnowTableProps) {
    const byKey = new Map(cells.map(cell => [`${cell.sourceId}|${cell.zip}`, cell]));

    function rangesFor(zip: string): SnowRange[] {
        const fromSources = sources.map(source => {
            const cell = byKey.get(`${source.id}|${zip}`);
            return { low: cell?.lowInches ?? null, high: cell?.highInches ?? null, partial: cell?.partial ?? false };
        });
        const typed = manualRows.map(row => manual.values[row.id]?.[zip] ?? { low: null, high: null });
        return [...fromSources, ...typed];
    }

    const averages = towns.map(town => averageSnow(rangesFor(town.zip)));

    return (
        <div className="table-scroll">
            <table className="snow-table">
                <thead>
                    <tr>
                        <th scope="col" rowSpan={2} className="date-column">Snow (inches)</th>
                        {towns.map(town =>
                            <th key={town.zip} scope="colgroup" colSpan={2} className="group-start" title={town.message ?? `ZIP ${town.zip}`}>
                                {town.name}
                                {town.message && <span className="column-status status-error">{town.message}</span>}
                            </th>
                        )}
                    </tr>
                    <tr>
                        {towns.map(town =>
                            [<th key={`${town.zip}-low`} scope="col" className="measure-header group-start">Low</th>,
                                <th key={`${town.zip}-high`} scope="col" className="measure-header">High</th>]
                        )}
                    </tr>
                </thead>
                <tbody>
                    {sources.map(source =>
                        <tr key={source.id}>
                            <th scope="row" className="date-column" title={sourceTitle(source.id, source.message)}>
                                {source.label}
                                {source.status !== 'Ok' &&
                                    <span className={`column-status status-${source.status.toLowerCase()}`}>
                                        {source.status === 'NeedsKey' ? 'needs key' : 'error'}
                                    </span>}
                            </th>
                            {towns.map(town => {
                                const cell = byKey.get(`${source.id}|${town.zip}`);
                                const title = cell?.partial
                                    ? "This forecast doesn't cover all of these dates, so this is a minimum."
                                    : cell?.message ?? undefined;
                                const failed = cell && cell.status === 'Error';
                                return [
                                    <td key={`${town.zip}-low`} className="group-start" title={title}>
                                        {failed ? '—' : cellText(cell?.lowInches ?? null, cell?.partial ?? false)}
                                    </td>,
                                    <td key={`${town.zip}-high`} title={title}>
                                        {failed ? '—' : cellText(cell?.highInches ?? null, cell?.partial ?? false)}
                                    </td>,
                                ];
                            })}
                        </tr>
                    )}
                    {manualRows.map(row =>
                        <tr key={row.id} className="manual-row">
                            <th scope="row" className="date-column" title={sourceTitle(row.id)}>
                                <span className="manual-label">
                                    <input
                                        type="text"
                                        aria-label="Source name"
                                        value={row.label}
                                        maxLength={40}
                                        onChange={event => onManualRename(row.id, event.target.value)}
                                    />
                                    <button type="button" className="icon-button" aria-label={`Remove ${row.label}`} title="Remove this row" onClick={() => onManualRemove(row.id)}>×</button>
                                </span>
                            </th>
                            {towns.map(town => {
                                const value = manual.values[row.id]?.[town.zip];
                                return (['low', 'high'] as const).map(part =>
                                    <td key={`${town.zip}-${part}-${manualGeneration}`} className={part === 'low' ? 'group-start' : undefined}>
                                        <input
                                            type="number"
                                            inputMode="decimal"
                                            min={0}
                                            step={0.5}
                                            aria-label={`${row.label} ${part} for ${town.name}`}
                                            defaultValue={value?.[part] ?? ''}
                                            onChange={event => onManualChange(row.id, town.zip, part, parseInches(event.target.value))}
                                        />
                                    </td>
                                );
                            })}
                        </tr>
                    )}
                    <tr className="snow-average">
                        <th scope="row" className="date-column">Averages</th>
                        {averages.map((average, index) =>
                            [<td key={`${towns[index].zip}-low`} className="group-start">{formatInches(average.low)}</td>,
                                <td key={`${towns[index].zip}-high`}>{formatInches(average.high)}</td>]
                        )}
                    </tr>
                    <tr className="snow-average">
                        <th scope="row" className="date-column">Average</th>
                        {averages.map((average, index) =>
                            <td key={towns[index].zip} colSpan={2} className="group-start" title={`From ${average.count} ${average.count === 1 ? 'source' : 'sources'}`}>
                                {average.middle === null ? '' : (Math.round(average.middle * 10) / 10).toFixed(1)}
                            </td>
                        )}
                    </tr>
                </tbody>
            </table>
        </div>
    );
}

export default SnowTable;
