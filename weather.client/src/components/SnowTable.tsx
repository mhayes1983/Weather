import { sourceTitle } from '../sourceDescriptions';
import type { SnowCell, SnowSourceRow, SnowTown, SnowTownReports } from '../api/snow';
import { agreementLimits, averageSnow, sourceSpread, type SnowRange } from '../snowAverages';
import { parseInches, type ManualRow, type ManualSnow, type ManualValue } from '../snowManual';
import { formatAmount, viewInfo } from '../stormViews';

interface SnowTableProps {
    /** Snow, or rain and melted snow. Both are totals in inches; rain shows hundredths. */
    view: 'snow' | 'rain';
    towns: SnowTown[];
    sources: SnowSourceRow[];
    cells: SnowCell[];
    /** What has been measured near each town so far, shown under the averages and never part of them. */
    reports: SnowTownReports[];
    manualRows: ManualRow[];
    manual: ManualSnow;
    /** Changes when values are cleared, so the inputs start empty again. */
    manualGeneration: number;
    onManualChange: (rowId: string, zip: string, part: keyof ManualValue, value: number | null) => void;
    onManualRename: (rowId: string, label: string) => void;
    onManualRemove: (rowId: string) => void;
}

function SnowTable({ view, towns, sources, cells, reports, manualRows, manual, manualGeneration, onManualChange, onManualRename, onManualRemove }: SnowTableProps) {
    const byKey = new Map(cells.map(cell => [`${cell.sourceId}|${cell.zip}`, cell]));
    const format = (value: number | null) => formatAmount(value, view);
    const cellText = (value: number | null, partial: boolean) => {
        const text = format(value);
        return text && partial ? `≥${text}` : text;
    };

    function rangesFor(zip: string): SnowRange[] {
        const fromSources = sources.map(source => {
            const cell = byKey.get(`${source.id}|${zip}`);
            return { low: cell?.lowInches ?? null, high: cell?.highInches ?? null, partial: cell?.partial ?? false };
        });
        const typed = manualRows.map(row => manual.values[row.id]?.[zip] ?? { low: null, high: null });
        return [...fromSources, ...typed];
    }

    const averages = towns.map(town => averageSnow(rangesFor(town.zip)));
    const spreads = towns.map(town => sourceSpread(rangesFor(town.zip), agreementLimits[view]));

    // Ice only gets a row when a source expects some, so a snow-only storm keeps a short table.
    const iceSources = sources.filter(source =>
        towns.some(town => (byKey.get(`${source.id}|${town.zip}`)?.iceInches ?? 0) > 0));
    const reported = new Map(reports.map(town => [town.zip, town]));
    const anyReported = towns.some(town => (reported.get(town.zip)?.reports.length ?? 0) > 0);

    return (
        <div className="table-scroll">
            <table className="snow-table">
                <thead>
                    <tr>
                        <th scope="col" rowSpan={2} className="date-column">{viewInfo[view].heading}</th>
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
                                // Snow shows how wet it is; both views flag a source whose liquid looks like rain or a mix.
                                const ratio = view === 'snow' && cell?.ratio !== null && cell?.ratio !== undefined
                                    && <span className="snow-ratio" title={`${cell.ratio} inches of snow for each inch of water. About 10:1 is typical; lower is wetter and heavier.`}>{cell.ratio}:1</span>;
                                const mixed = cell?.mixed
                                    && <span className="mix-badge" title="This source's liquid is well beyond what its snow accounts for, so it likely expects rain or a mix.">mix</span>;
                                return [
                                    <td key={`${town.zip}-low`} className="group-start" title={title}>
                                        {failed ? '—' : cellText(cell?.lowInches ?? null, cell?.partial ?? false)}
                                    </td>,
                                    <td key={`${town.zip}-high`} title={title}>
                                        {failed ? '—' : cellText(cell?.highInches ?? null, cell?.partial ?? false)}
                                        {!failed && ratio}
                                        {!failed && mixed}
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
                                            step={view === 'rain' ? 0.05 : 0.5}
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
                            [<td key={`${towns[index].zip}-low`} className="group-start">{format(average.low)}</td>,
                                <td key={`${towns[index].zip}-high`}>{format(average.high)}</td>]
                        )}
                    </tr>
                    <tr className="snow-average">
                        <th scope="row" className="date-column">Average</th>
                        {averages.map((average, index) =>
                            <td key={towns[index].zip} colSpan={2} className="group-start" title={`From ${average.count} ${average.count === 1 ? 'source' : 'sources'}`}>
                                {average.middle === null ? '' : average.middle.toFixed(viewInfo[view].digits)}
                            </td>
                        )}
                    </tr>
                    {spreads.some(spread => spread !== null) &&
                        <tr className="snow-average">
                            <th scope="row" className="date-column" title="The lowest and highest source, leaving out partial ones, and whether they're close enough to agree">
                                Spread
                            </th>
                            {spreads.map((spread, index) =>
                                <td key={towns[index].zip} colSpan={2} className="group-start">
                                    {spread &&
                                        <>
                                            {format(spread.lowest)}–{format(spread.highest)}
                                            <span className={`agree-badge ${spread.agree ? 'agree' : 'disagree'}`}>{spread.agree ? 'agree' : 'differ'}</span>
                                        </>}
                                </td>
                            )}
                        </tr>}
                    {iceSources.map(source =>
                        <tr key={`${source.id}-ice`} className="snow-extra">
                            <th scope="row" className="date-column" title="Ice build-up from freezing rain, in inches. It isn't part of the snow averages.">
                                {source.label} ice
                            </th>
                            {towns.map(town => {
                                const ice = byKey.get(`${source.id}|${town.zip}`)?.iceInches;
                                return (
                                    <td key={town.zip} colSpan={2} className="group-start">
                                        {ice === null || ice === undefined ? '' : ice === 0 ? '0' : ice.toFixed(2)}
                                    </td>
                                );
                            })}
                        </tr>
                    )}
                    {anyReported &&
                        <tr className="snow-extra">
                            <th scope="row" className="date-column" title="The lowest and highest totals reported near each town so far. They aren't part of the averages.">
                                Reported
                            </th>
                            {towns.map(town => {
                                const report = reported.get(town.zip);
                                const count = report?.reports.length ?? 0;
                                const title = count > 0 ? `From ${count} ${count === 1 ? 'place' : 'places'} nearby` : undefined;
                                return [
                                    <td key={`${town.zip}-low`} className="group-start" title={title}>{format(report?.low ?? null)}</td>,
                                    <td key={`${town.zip}-high`} title={title}>{format(report?.high ?? null)}</td>,
                                ];
                            })}
                        </tr>}
                </tbody>
            </table>
        </div>
    );
}

export default SnowTable;
