import { sourceTitle } from '../sourceDescriptions';
import type { SnowSourceRow, SnowTown, SnowTownReports, WindCell } from '../api/snow';
import { agreementLimits, averageSnow, sourceSpread, type SnowRange } from '../snowAverages';
import { parseInches, type ManualRow, type ManualSnow, type ManualValue } from '../snowManual';
import { localDayTime, timingText } from '../snowTiming';

interface WindTableProps {
    towns: SnowTown[];
    sources: SnowSourceRow[];
    cells: WindCell[];
    /** The strongest gusts measured near each town so far, never part of the averages. */
    reports: SnowTownReports[];
    manualRows: ManualRow[];
    manual: ManualSnow;
    manualGeneration: number;
    onManualChange: (rowId: string, zip: string, part: keyof ManualValue, value: number | null) => void;
    onManualRename: (rowId: string, label: string) => void;
    onManualRemove: (rowId: string) => void;
}

function mph(value: number | null | undefined) {
    return value === null || value === undefined ? '' : String(Math.round(value));
}

/** Each source's strongest gust for each town over the dates, in mph, with averages. Manual rows hold one speed. */
function WindTable({ towns, sources, cells, reports, manualRows, manual, manualGeneration, onManualChange, onManualRename, onManualRemove }: WindTableProps) {
    const byKey = new Map(cells.map(cell => [`${cell.sourceId}|${cell.zip}`, cell]));

    function rangesFor(zip: string): SnowRange[] {
        const fromSources = sources.map(source => {
            const cell = byKey.get(`${source.id}|${zip}`);
            return { low: cell?.maxMph ?? null, high: cell?.maxMph ?? null, partial: cell?.partial ?? false };
        });
        const typed = manualRows.map(row => {
            const value = manual.values[row.id]?.[zip]?.high ?? null;
            return { low: value, high: value };
        });
        return [...fromSources, ...typed];
    }

    const averages = towns.map(town => averageSnow(rangesFor(town.zip)));
    const spreads = towns.map(town => sourceSpread(rangesFor(town.zip), agreementLimits.wind));
    const reported = new Map(reports.map(town => [town.zip, town]));
    const anyReported = towns.some(town => (reported.get(town.zip)?.reports.length ?? 0) > 0);

    return (
        <div className="table-scroll">
            <table className="snow-table">
                <thead>
                    <tr>
                        <th scope="col" className="date-column">Strongest gust (mph)</th>
                        {towns.map(town =>
                            <th key={town.zip} scope="col" className="group-start" title={town.message ?? `ZIP ${town.zip}`}>
                                {town.name}
                                {town.message && <span className="column-status status-error">{town.message}</span>}
                            </th>
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
                                const failed = cell?.status === 'Error';
                                const text = mph(cell?.maxMph);
                                const when = cell?.peakStart && cell.peakEnd
                                    ? `Strongest ${timingText(cell.peakStart, cell.peakEnd)}`
                                    : undefined;
                                const title = cell?.partial
                                    ? "This forecast doesn't cover all of these dates, so this is a minimum."
                                    : when ?? cell?.message ?? undefined;
                                return (
                                    <td key={town.zip} className="group-start" title={title}>
                                        {failed ? '—' : text && cell?.partial ? `≥${text}` : text}
                                    </td>
                                );
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
                            {towns.map(town =>
                                <td key={`${town.zip}-${manualGeneration}`} className="group-start">
                                    <input
                                        type="number"
                                        inputMode="numeric"
                                        min={0}
                                        step={5}
                                        aria-label={`${row.label} strongest gust for ${town.name}`}
                                        defaultValue={manual.values[row.id]?.[town.zip]?.high ?? ''}
                                        // A gust is one speed, kept as the row's high.
                                        onChange={event => onManualChange(row.id, town.zip, 'high', parseInches(event.target.value))}
                                    />
                                </td>
                            )}
                        </tr>
                    )}
                    <tr className="snow-average">
                        <th scope="row" className="date-column">Average</th>
                        {averages.map((average, index) =>
                            <td key={towns[index].zip} className="group-start" title={`From ${average.count} ${average.count === 1 ? 'source' : 'sources'}`}>
                                {mph(average.middle)}
                            </td>
                        )}
                    </tr>
                    {spreads.some(spread => spread !== null) &&
                        <tr className="snow-average">
                            <th scope="row" className="date-column" title="The lowest and highest source, leaving out partial ones, and whether they're close enough to agree">
                                Spread
                            </th>
                            {spreads.map((spread, index) =>
                                <td key={towns[index].zip} className="group-start">
                                    {spread &&
                                        <>
                                            {mph(spread.lowest)}–{mph(spread.highest)}
                                            <span className={`agree-badge ${spread.agree ? 'agree' : 'disagree'}`}>{spread.agree ? 'agree' : 'differ'}</span>
                                        </>}
                                </td>
                            )}
                        </tr>}
                    {anyReported &&
                        <tr className="snow-extra">
                            <th scope="row" className="date-column" title="The strongest gust measured near each town so far. It isn't part of the averages.">
                                Reported
                            </th>
                            {towns.map(town => {
                                const report = reported.get(town.zip);
                                const top = report?.reports.reduce<typeof report.reports[number] | undefined>(
                                    (best, other) => !best || other.amount > best.amount ? other : best, undefined);
                                return (
                                    <td key={town.zip} className="group-start" title={top ? `${top.place}, ${localDayTime(top.observedAt)}` : undefined}>
                                        {mph(report?.high)}
                                    </td>
                                );
                            })}
                        </tr>}
                </tbody>
            </table>
        </div>
    );
}

export default WindTable;
