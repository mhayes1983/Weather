import { useEffect, useState, type PointerEvent } from 'react';
import type { SnowCell, SnowHourly, SnowSourceRow, SnowTown } from '../api/snow';
import { hourlyLinkSearch } from '../snowLink';
import { sourceTitle } from '../sourceDescriptions';
import {
    agreementByHour, datesBetween, dayLabel, hourAt, hoursBetween, intensity, localDayTime, periodSegments, spanSummary,
    timingSpan, timingText,
} from '../snowTiming';

/** The hourly data for one town, as it loads. */
export interface HourlyState {
    data?: SnowHourly;
    error?: string;
}

interface SnowTimingProps {
    towns: SnowTown[];
    sources: SnowSourceRow[];
    cells: SnowCell[];
    start: string;
    end: string;
    hourly: (zip: string) => HourlyState | undefined;
    onTownShown: (zip: string) => void;
    onOpenSource: (sourceId: string, zip: string) => void;
}

/**
 * When each source has snow falling in one town, as text and as a bar across the dates. With the town's
 * hourly data, each bar is shaded by how hard snow falls, and a first row counts how many sources agree.
 */
function SnowTiming({ towns, sources, cells, start, end, hourly, onTownShown, onOpenSource }: SnowTimingProps) {
    const [chosen, setChosen] = useState<string>();
    const [caption, setCaption] = useState<string>();
    const town = towns.find(other => other.zip === chosen) ?? towns[0] as SnowTown | undefined;
    const zip = town?.zip;

    useEffect(() => {
        if (zip) {
            onTownShown(zip);
        }
    }, [zip, onTownShown]);

    if (!town) {
        return null;
    }

    const days = datesBetween(start, end);
    const hours = hoursBetween(start, end);
    const data = hourly(town.zip)?.data;
    const rows = sources.flatMap(source => {
        const cell = cells.find(other => other.sourceId === source.id && other.zip === town.zip);
        return cell?.snowStart && cell.snowEnd ? [{ source, cell, snowStart: cell.snowStart, snowEnd: cell.snowEnd }] : [];
    });

    // Only the sources shown count towards agreement, so hiding one in the filters changes it.
    const agreeing = data ? sources.filter(source => data.sources.some(other => other.id === source.id && other.status === 'Ok')) : [];
    const agreement = data
        ? agreementByHour(agreeing.map(source => data.sources.find(other => other.id === source.id)!.periods), start, end)
        : undefined;
    const most = agreement ? Math.max(0, ...agreement) : 0;
    const now = data && data.now.slice(0, 10) >= start && data.now.slice(0, 10) <= end
        ? timingSpan(data.now, data.now, start, end).left
        : undefined;

    function hourText(hour: number) {
        return localDayTime(`${hourAt(start, hour)}:00`);
    }

    function handleAgreementPointer(event: PointerEvent<HTMLDivElement>) {
        if (!agreement) {
            return;
        }

        const box = event.currentTarget.getBoundingClientRect();
        const hour = Math.min(hours - 1, Math.max(0, Math.floor((event.clientX - box.left) / box.width * hours)));
        setCaption(`${hourText(hour)}: ${agreement[hour]} of ${agreeing.length} sources have snow falling.`);
    }

    function chooseTown(next: string) {
        setChosen(next);
        setCaption(undefined);
    }

    const nowLine = now !== undefined && <i className="snow-timing-now" style={{ left: `${now}%` }} />;
    const busiest = agreement && most > 0 ? agreement.indexOf(most) : -1;

    return (
        <section className="snow-timing">
            <div className="snow-timing-heading">
                <h2>Storm timing</h2>
                <label>
                    Town
                    <select value={town.zip} onChange={event => chooseTown(event.target.value)}>
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
                            {agreement && agreeing.length > 1 &&
                                <tr className="snow-timing-agreement">
                                    <th scope="row" className="date-column" title="How many of the sources shown have snow falling each hour">Agree</th>
                                    <td className="snow-timing-text">
                                        {busiest < 0 ? 'No snow' : `Most: ${most} of ${agreeing.length}, ${hourText(busiest)}`}
                                    </td>
                                    <td className="snow-timing-track">
                                        <div
                                            role="img"
                                            aria-label={busiest < 0
                                                ? 'No source shown has snow falling.'
                                                : `Most sources agree at ${hourText(busiest)}, when ${most} of ${agreeing.length} have snow falling.`}
                                            onPointerMove={handleAgreementPointer}
                                            onPointerDown={handleAgreementPointer}
                                            onPointerLeave={() => setCaption(undefined)}
                                        >
                                            {agreement.map((count, hour) => count > 0 &&
                                                <span
                                                    key={hour}
                                                    style={{
                                                        left: `${hour / hours * 100}%`,
                                                        width: `${100 / hours}%`,
                                                        opacity: 0.2 + 0.8 * count / agreeing.length,
                                                    }}
                                                />
                                            )}
                                            {nowLine}
                                        </div>
                                    </td>
                                </tr>}
                            {rows.map(({ source, cell, snowStart, snowEnd }) => {
                                const span = timingSpan(snowStart, snowEnd, start, end);
                                const periods = data?.sources.find(other => other.id === source.id && other.status === 'Ok')?.periods;
                                const summary = `${source.label}: ${spanSummary(cell)}`;
                                return (
                                    <tr key={source.id}>
                                        <th scope="row" className="date-column" title={sourceTitle(source.id, source.message)}>
                                            <a
                                                href={`/snow/hourly${hourlyLinkSearch({ zip: town.zip, source: source.id, start, end })}`}
                                                aria-label={`${source.label} hourly snow for ${town.name}`}
                                                onClick={event => {
                                                    // Modified clicks keep their normal behaviour, such as opening a new tab.
                                                    if (!event.ctrlKey && !event.metaKey && !event.shiftKey) {
                                                        event.preventDefault();
                                                        onOpenSource(source.id, town.zip);
                                                    }
                                                }}
                                            >
                                                {source.label}
                                            </a>
                                        </th>
                                        <td className="snow-timing-text">{timingText(snowStart, snowEnd)}</td>
                                        <td className="snow-timing-track">
                                            <button
                                                type="button"
                                                className="snow-timing-bar"
                                                aria-label={summary}
                                                onMouseEnter={() => setCaption(summary)}
                                                onMouseLeave={() => setCaption(undefined)}
                                                onFocus={() => setCaption(summary)}
                                                onBlur={() => setCaption(undefined)}
                                                onClick={() => setCaption(summary)}
                                            >
                                                <div style={{ backgroundSize: `${100 / days.length}% 100%` }}>
                                                    {periods
                                                        ? periodSegments(periods, start, end).map(segment =>
                                                            <span
                                                                key={segment.period.start}
                                                                className={intensity(segment.rate)}
                                                                style={{ left: `${segment.left}%`, width: `${segment.width}%` }}
                                                            />)
                                                        : <span style={{ left: `${span.left}%`, width: `${span.width}%` }} />}
                                                    {nowLine}
                                                </div>
                                            </button>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>}

            {rows.length > 0 &&
                <p className="snow-timing-caption" role="status">{caption ?? 'Hover over or tap a bar for its total. Select a source for its hourly forecast.'}</p>}

            <p className="table-note">
                From each source's first snow to its last on these dates, leaving out flurries. Darker shading is heavier snow: light
                under ¼ in/hr, moderate to 1 in/hr, heavy above. Agree counts the sources shown with snow falling each hour, and the
                line marks now. NWS and Weather Underground forecast in 6 to 12-hour blocks, so their times are approximate.
            </p>
        </section>
    );
}

export default SnowTiming;
