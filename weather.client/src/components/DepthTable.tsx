import { sourceTitle } from '../sourceDescriptions';
import type { DepthCell, SnowSourceRow, SnowTown } from '../api/snow';
import { averageSnow } from '../snowAverages';
import { localDayTime } from '../snowTiming';
import { formatAmount } from '../stormViews';

interface DepthTableProps {
    towns: SnowTown[];
    sources: SnowSourceRow[];
    cells: DepthCell[];
    /** Opens a model's hourly depth for one town. */
    onOpenSource: (sourceId: string, zip: string) => void;
}

const parts = [
    { key: 'nowInches', label: 'Now', title: "The model's snow on the ground at this hour" },
    { key: 'maxInches', label: 'Most', title: 'The most snow on the ground at any hour of these dates' },
    { key: 'endInches', label: 'End', title: 'Snow on the ground at the end of these dates' },
] as const;

/**
 * Each model's snow on the ground: now, the most during the dates and at their end. Only Open-Meteo's models
 * give depth, and it's modelled from their own snow and melt, never measured, so there are no manual rows.
 */
function DepthTable({ towns, sources, cells, onOpenSource }: DepthTableProps) {
    const byKey = new Map(cells.map(cell => [`${cell.sourceId}|${cell.zip}`, cell]));
    const format = (value: number | null | undefined) => formatAmount(value, 'depth');
    const averages = towns.map(town => Object.fromEntries(parts.map(part => [part.key, averageSnow(sources.map(source => {
        const cell = byKey.get(`${source.id}|${town.zip}`);
        const value = cell?.[part.key] ?? null;
        // A model that ends early has no end of the dates to give.
        return { low: value, high: value, partial: part.key === 'endInches' && (cell?.partial ?? false) };
    })).middle])) as Record<(typeof parts)[number]['key'], number | null>);

    return (
        <div className="table-scroll">
            <table className="snow-table">
                <thead>
                    <tr>
                        <th scope="col" rowSpan={2} className="date-column">Snow on the ground (inches)</th>
                        {towns.map(town =>
                            <th key={town.zip} scope="colgroup" colSpan={3} className="group-start" title={town.message ?? `ZIP ${town.zip}`}>
                                {town.name}
                                {town.message && <span className="column-status status-error">{town.message}</span>}
                            </th>
                        )}
                    </tr>
                    <tr>
                        {towns.map(town => parts.map((part, index) =>
                            <th key={`${town.zip}-${part.key}`} scope="col" title={part.title} className={`measure-header${index === 0 ? ' group-start' : ''}`}>{part.label}</th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {sources.map(source =>
                        <tr key={source.id}>
                            <th scope="row" className="date-column" title={sourceTitle(source.id, source.message)}>
                                <a
                                    href="/storm/hourly"
                                    aria-label={`${source.label} hourly snow depth for ${towns[0]?.name ?? 'the first town'}`}
                                    onClick={event => {
                                        event.preventDefault();
                                        if (towns[0]) {
                                            onOpenSource(source.id, towns[0].zip);
                                        }
                                    }}
                                >
                                    {source.label}
                                </a>
                            </th>
                            {towns.map(town => {
                                const cell = byKey.get(`${source.id}|${town.zip}`);
                                const failed = cell?.status === 'Error';
                                return parts.map((part, index) => {
                                    const title = part.key === 'maxInches' && cell?.maxAt
                                        ? `Most at ${localDayTime(cell.maxAt)}`
                                        : part.key === 'endInches' && cell?.partial
                                            ? 'This model ends before these dates do, so this is its last value.'
                                            : cell?.message ?? undefined;
                                    return (
                                        <td key={`${town.zip}-${part.key}`} className={index === 0 ? 'group-start' : undefined} title={title}>
                                            {failed ? '—' : format(cell?.[part.key])}
                                        </td>
                                    );
                                });
                            })}
                        </tr>
                    )}
                    <tr className="snow-average">
                        <th scope="row" className="date-column">Average</th>
                        {averages.map((average, index) => parts.map((part, partIndex) =>
                            <td key={`${towns[index].zip}-${part.key}`} className={partIndex === 0 ? 'group-start' : undefined}>{format(average[part.key])}</td>
                        ))}
                    </tr>
                </tbody>
            </table>
        </div>
    );
}

export default DepthTable;
