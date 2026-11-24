import { useEffect, useState, type PointerEvent } from 'react';
import type { SnowCell, SnowHourly, SnowHourlySource, SnowSourceRow, SnowTown, WindCell } from '../api/snow';
import { hourlyLinkSearch } from '../snowLink';
import { sourceTitle } from '../sourceDescriptions';
import {
    agreementByHour, datesBetween, dayLabel, gustSegments, hourAt, hoursBetween, intensity, localDayTime, periodSegments, spanSummary,
    timingSpan, timingText, townNow, windSummary, windySpans, type TimedAmount,
} from '../snowTiming';
import { viewInfo } from '../stormViews';
import { useNow } from '../useNow';

/** The hourly data for one town, as it loads. */
export interface HourlyState {
    data?: SnowHourly;
    error?: string;
}

interface SnowTimingProps {
    view: 'snow' | 'rain' | 'wind';
    towns: SnowTown[];
    sources: SnowSourceRow[];
    /** Snow or rain cells. Wind reads `windCells`. */
    cells: SnowCell[];
    windCells: WindCell[];
    start: string;
    end: string;
    hourly: (zip: string) => HourlyState | undefined;
    onTownShown: (zip: string) => void;
    onOpenSource: (sourceId: string, zip: string) => void;
}

/** The periods agreement counts for a source in a view: snow, liquid, or its windy hours. */
function activeSpans(source: SnowHourlySource, view: SnowTimingProps['view']): TimedAmount[] | null {
    return view === 'snow' ? source.periods
        : view === 'rain' ? source.liquid
            : source.gusts && windySpans(source.gusts);
}

/** A source's shaded segments on its bar, each with the rate that sets its shading. */
function segmentsFor(source: SnowHourlySource, view: SnowTimingProps['view'], start: string, end: string) {
    if (view === 'wind') {
        return source.gusts && gustSegments(source.gusts, start, end).map(segment => ({ ...segment, key: segment.period.start }));
    }

    const periods = view === 'rain' ? source.liquid : source.periods;
    return periods && periodSegments(periods, start, end).map(segment => ({ ...segment, key: segment.period.start }));
}

/**
 * When each source has snow or rain falling, or strong gusts, in one town, as text and as a bar across the dates.
 * With the town's hourly data, each bar is shaded by how hard it comes down or blows, and a first row counts
 * how many sources agree.
 */
function SnowTiming({ view, towns, sources, cells, windCells, start, end, hourly, onTownShown, onOpenSource }: SnowTimingProps) {
    const info = viewInfo[view];
    const [chosen, setChosen] = useState<string>();
    const [caption, setCaption] = useState<string>();
    const town = towns.find(other => other.zip === chosen) ?? towns[0] as SnowTown | undefined;
    const zip = town?.zip;

    useEffect(() => {
        if (zip) {
            onTownShown(zip);
        }
    }, [zip, onTownShown]);

    // The now line moves with the clock rather than staying where it was when the data loaded.
    const clock = useNow();

    if (!town) {
        return null;
    }

    const days = datesBetween(start, end);
    const hours = hoursBetween(start, end);
    const data = hourly(town.zip)?.data;
    const rows = sources.flatMap(source => {
        if (view === 'wind') {
            const cell = windCells.find(other => other.sourceId === source.id && other.zip === town.zip);
            return cell?.windyStart && cell.windyEnd
                ? [{ source, summary: windSummary(cell), snowStart: cell.windyStart, snowEnd: cell.windyEnd }]
                : [];
        }

        const cell = cells.find(other => other.sourceId === source.id && other.zip === town.zip);
        return cell?.snowStart && cell.snowEnd
            ? [{ source, summary: spanSummary(cell, info.digits), snowStart: cell.snowStart, snowEnd: cell.snowEnd }]
            : [];
    });

    // Only the sources shown count towards agreement, so hiding one in the filters changes it.
    const agreeing = data
        ? sources.flatMap(source => {
            const found = data.sources.find(other => other.id === source.id && other.status === 'Ok');
            const spans = found && activeSpans(found, view);
            return spans ? [spans] : [];
        })
        : [];
    const agreement = data ? agreementByHour(agreeing, start, end) : undefined;
    const most = agreement ? Math.max(0, ...agreement) : 0;
    const current = data ? townNow(data.now, clock) : undefined;
    const now = current && current.slice(0, 10) >= start && current.slice(0, 10) <= end
        ? timingSpan(current, current, start, end).left
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
        setCaption(`${hourText(hour)}: ${agreement[hour]} of ${agreeing.length} sources have ${info.active}.`);
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
                ? <p>No source shown has {info.active} in {town.name} on these dates.</p>
                : <div className="table-scroll">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col" className="date-column">Source</th>
                                <th scope="col">{view === 'wind' ? 'Windy' : view === 'rain' ? 'Rain falling' : 'Snow falling'}</th>
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
                                    <th scope="row" className="date-column" title={`How many of the sources shown have ${info.active} each hour`}>Agree</th>
                                    <td className="snow-timing-text">
                                        {busiest < 0 ? 'None' : `Most: ${most} of ${agreeing.length}, ${hourText(busiest)}`}
                                    </td>
                                    <td className="snow-timing-track">
                                        <div className="snow-timing-strip"
                                            role="img"
                                            aria-label={busiest < 0
                                                ? `No source shown has ${info.active}.`
                                                : `Most sources agree at ${hourText(busiest)}, when ${most} of ${agreeing.length} have ${info.active}.`}
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
                            {rows.map(({ source, summary: text, snowStart, snowEnd }) => {
                                const span = timingSpan(snowStart, snowEnd, start, end);
                                const found = data?.sources.find(other => other.id === source.id && other.status === 'Ok');
                                const segments = found && segmentsFor(found, view, start, end);
                                const summary = `${source.label}: ${text}`;
                                return (
                                    <tr key={source.id}>
                                        <th scope="row" className="date-column" title={sourceTitle(source.id, source.message)}>
                                            <a
                                                href={`/storm/hourly${hourlyLinkSearch({ zip: town.zip, source: source.id, start, end, view })}`}
                                                aria-label={`${source.label} hourly ${info.label.toLowerCase()} for ${town.name}`}
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
                                                <span className="snow-timing-strip" style={{ backgroundSize: `${100 / days.length}% 100%` }}>
                                                    {segments
                                                        ? segments.map(segment =>
                                                            <span
                                                                key={segment.key}
                                                                className={intensity(segment.rate, view)}
                                                                style={{ left: `${segment.left}%`, width: `${segment.width}%` }}
                                                            />)
                                                        : <span style={{ left: `${span.left}%`, width: `${span.width}%` }} />}
                                                    {nowLine}
                                                </span>
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
                {view === 'wind'
                    ? <>From each source's first gust of 30 mph or more to its last on these dates. Darker shading is stronger: moderate
                        from 30 mph, heavy from 46 mph, when the NWS issues a Wind Advisory. Agree counts the sources shown with gusts of
                        30 mph or more each hour, and the line marks now. NWS forecasts gusts in blocks of an hour or more.</>
                    : view === 'rain'
                        ? <>From each source's first rain to its last on these dates, counting melted snow and leaving out a trace. Darker
                            shading is heavier: light under 0.1 in/hr, moderate to 0.3 in/hr, heavy above. Agree counts the sources shown
                            with rain falling each hour, and the line marks now. NWS and Weather Underground forecast in 6 to 12-hour
                            blocks, so their times are approximate.</>
                        : <>From each source's first snow to its last on these dates, leaving out flurries. Darker shading is heavier
                            snow: light under ¼ in/hr, moderate to 1 in/hr, heavy above. Agree counts the sources shown with snow falling
                            each hour, and the line marks now. NWS and Weather Underground forecast in 6 to 12-hour blocks, so their times
                            are approximate.</>}
            </p>
        </section>
    );
}

export default SnowTiming;
