import { useState } from 'react';
import type { SnowCell, SnowSourceRow, SnowTown } from '../api/snow';
import { sourceTitle } from '../sourceDescriptions';
import { datesBetween, dayLabel, timingSpan, timingText } from '../snowTiming';

interface SnowTimingProps {
    towns: SnowTown[];
    sources: SnowSourceRow[];
    cells: SnowCell[];
    start: string;
    end: string;
}

/** When each source has snow falling in one town, as a bar across the dates and as text. */
function SnowTiming({ towns, sources, cells, start, end }: SnowTimingProps) {
    const [chosen, setChosen] = useState<string>();
    const town = towns.find(other => other.zip === chosen) ?? towns[0] as SnowTown | undefined;
    if (!town) {
        return null;
    }

    const days = datesBetween(start, end);
    const rows = sources.flatMap(source => {
        const cell = cells.find(other => other.sourceId === source.id && other.zip === town.zip);
        return cell?.snowStart && cell.snowEnd ? [{ source, snowStart: cell.snowStart, snowEnd: cell.snowEnd }] : [];
    });

    return (
        <section className="snow-timing">
            <div className="snow-timing-heading">
                <h2>Storm timing</h2>
                <label>
                    Town
                    <select value={town.zip} onChange={event => setChosen(event.target.value)}>
                        {towns.map(other => <option key={other.zip} value={other.zip}>{other.name}</option>)}
                    </select>
                </label>
            </div>

            {rows.length === 0
                ? <p>No source shown has snow falling in {town.name} on these dates.</p>
                : <div className="table-scroll">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col" className="date-column">Source</th>
                                <th scope="col">Snow falling</th>
                                <td className="snow-timing-days" aria-hidden="true">
                                    <div style={{ gridTemplateColumns: `repeat(${days.length}, 1fr)` }}>
                                        {days.map(day => <span key={day}>{dayLabel(day)}</span>)}
                                    </div>
                                </td>
                            </tr>
                        </thead>
                        <tbody>
                            {rows.map(({ source, snowStart, snowEnd }) => {
                                const span = timingSpan(snowStart, snowEnd, start, end);
                                return (
                                    <tr key={source.id}>
                                        <th scope="row" className="date-column" title={sourceTitle(source.id, source.message)}>{source.label}</th>
                                        <td className="snow-timing-text">{timingText(snowStart, snowEnd)}</td>
                                        <td className="snow-timing-track" aria-hidden="true">
                                            <div style={{ backgroundSize: `${100 / days.length}% 100%` }}>
                                                <span style={{ left: `${span.left}%`, width: `${span.width}%` }} />
                                            </div>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>}

            <p className="table-note">
                From each source's first snow to its last on these dates, leaving out flurries. NWS and Weather Underground
                forecast in 6 to 12-hour blocks, so their times are approximate.
            </p>
        </section>
    );
}

export default SnowTiming;
