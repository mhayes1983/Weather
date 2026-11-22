import type { SnowHourlySource, SnowPeriod } from '../api/snow';
import type { HourlyLink } from '../snowLink';
import {
    cumulativeByHour, dayLabel, elapsedHours, fallingInchesPerHour, hourAt, hourlyRate, hoursBetween, inchesText, intensity,
    localDayTime, overlapInches, timingSpan, timingText, townNow,
} from '../snowTiming';
import { useNow } from '../useNow';
import type { HourlyState } from './SnowTiming';

interface SnowHourlyProps {
    link: HourlyLink;
    state: HourlyState | undefined;
    loading: boolean;
    onLoad: () => void;
    onSourceChange: (sourceId: string) => void;
    onBack: () => void;
}

const width = 600;
const height = 160;
const left = 40;
const right = 8;
const top = 8;
const bottom = 22;

/** Each period's snow as a bar as wide as the period, with NWS ice beside it. */
function PeriodBars({ periods, ice, start, end }: { periods: SnowPeriod[]; ice: SnowPeriod[] | null; start: string; end: string }) {
    const largest = Math.max(0.1, ...periods.map(period => period.highInches), ...(ice ?? []).map(period => period.highInches));
    const plotWidth = width - left - right;
    const plotHeight = height - top - bottom;
    const y = (inches: number) => height - bottom - inches / largest * plotHeight;
    const days = hoursBetween(start, end) / 24;

    function bar(period: SnowPeriod, className: string) {
        const span = timingSpan(period.start, period.end, start, end);
        return (
            <rect
                key={`${className}${period.start}`}
                className={className}
                x={left + span.left / 100 * plotWidth + 0.5}
                width={Math.max(1, span.width / 100 * plotWidth - 1)}
                y={y(period.highInches)}
                height={Math.max(0, height - bottom - y(period.highInches))}
            >
                <title>{`${timingText(period.start, period.end)}: ${inchesText(period.lowInches, period.highInches, 2)}`}</title>
            </rect>
        );
    }

    const total = periods.reduce((sum, period) => sum + period.highInches, 0);
    return (
        <>
            <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`Snow for each period, ${inchesText(total, total)} in all.`}>
                {[0, largest / 2, largest].map(tick =>
                    <g key={tick}>
                        <line className="chart-grid" x1={left} x2={width - right} y1={y(tick)} y2={y(tick)} />
                        <text className="chart-label" x={left - 6} y={y(tick) + 4} textAnchor="end">{tick.toFixed(largest < 1 ? 2 : 1)}</text>
                    </g>
                )}
                {Array.from({ length: days }, (_, day) =>
                    <g key={day}>
                        <line className="chart-grid" x1={left + day / days * plotWidth} x2={left + day / days * plotWidth} y1={top} y2={height - bottom} />
                        <text className="chart-label" x={left + (day + 0.5) / days * plotWidth} y={height - 6} textAnchor="middle">
                            {dayLabel(hourAt(start, day * 24).slice(0, 10))}
                        </text>
                    </g>
                )}
                {periods.filter(period => period.highInches > 0).map(period => bar(period, `snow-bar ${intensity(hourlyRate(period))}`))}
                {ice?.filter(period => period.highInches > 0).map(period => bar(period, 'ice-bar'))}
            </svg>
            <p className="chart-legend">
                <span><i className="snow-bar-key" />Snow</span>
                {ice && <span><i className="ice-bar-key" />Ice</span>}
                <span>in</span>
            </p>
        </>
    );
}

/** Each source's running total, hour by hour, with the chosen source drawn over the others. */
function RunningTotals({ chosen, others, start, end }: {
    chosen: SnowHourlySource; others: SnowHourlySource[]; start: string; end: string;
}) {
    const hours = hoursBetween(start, end);
    const lines = [...others.filter(other => other.id !== chosen.id), chosen]
        .map(source => ({ source, values: cumulativeByHour(source.periods, start, end) }));
    const largest = Math.max(0.1, ...lines.flatMap(line => line.values.filter(value => value !== null)));
    const plotWidth = width - left - right;
    const plotHeight = height - top - bottom;
    const x = (hour: number) => left + hour / hours * plotWidth;
    const y = (inches: number) => height - bottom - inches / largest * plotHeight;
    const days = hours / 24;

    function path(values: (number | null)[]) {
        return values.flatMap((value, hour) => value === null ? [] : [`${hour === 0 ? 'M' : 'L'}${x(hour).toFixed(1)} ${y(value).toFixed(1)}`]).join(' ');
    }

    const chosenTotal = lines[lines.length - 1].values.findLast(value => value !== null) ?? 0;
    return (
        <>
            <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`${chosen.label} reaches ${chosenTotal} in; grey lines are the other sources.`}>
                {[0, largest / 2, largest].map(tick =>
                    <g key={tick}>
                        <line className="chart-grid" x1={left} x2={width - right} y1={y(tick)} y2={y(tick)} />
                        <text className="chart-label" x={left - 6} y={y(tick) + 4} textAnchor="end">{tick.toFixed(largest < 1 ? 2 : 1)}</text>
                    </g>
                )}
                {Array.from({ length: days }, (_, day) =>
                    <g key={day}>
                        <line className="chart-grid" x1={x(day * 24)} x2={x(day * 24)} y1={top} y2={height - bottom} />
                        <text className="chart-label" x={x(day * 24 + 12)} y={height - 6} textAnchor="middle">
                            {dayLabel(hourAt(start, day * 24).slice(0, 10))}
                        </text>
                    </g>
                )}
                {lines.map(({ source, values }) =>
                    <path key={source.id} className={source.id === chosen.id ? 'chart-line' : 'chart-line snow-other-line'} d={path(values)}>
                        <title>{`${source.label}: ${values.findLast(value => value !== null) ?? 0} in`}</title>
                    </path>
                )}
            </svg>
            <p className="chart-legend">
                <span><i className="snow-bar-key" />{chosen.label}</span>
                <span><i className="snow-other-key" />Other sources</span>
                <span>in</span>
            </p>
        </>
    );
}

function totals(periods: SnowPeriod[]) {
    return periods.reduce(
        (sum, period) => ({ low: sum.low + period.lowInches, high: sum.high + period.highInches }),
        { low: 0, high: 0 });
}

/** One source's snow for one town, period by period, beside every other source's running total. */
function SnowHourly({ link, state, loading, onLoad, onSourceChange, onBack }: SnowHourlyProps) {
    const data = state?.data;
    const usable = data?.sources.filter(source => source.status === 'Ok') ?? [];
    const chosen = data?.sources.find(source => source.id === link.source);
    const label = chosen?.label ?? link.source.toUpperCase();
    const dates = link.start === link.end ? dayLabel(link.start) : `${dayLabel(link.start)} – ${dayLabel(link.end)}`;

    return (
        <main>
            <p className="back-link">
                <a href="/snow" onClick={event => { event.preventDefault(); onBack(); }}>← Snow Forecast</a>
            </p>
            <header>
                <h1>{label} hourly snow</h1>
                <p className="location">{data?.name ?? link.zip} · {dates}</p>
            </header>

            {!data &&
                <>
                    {state?.error && <p className="error" role="alert">{state.error}</p>}
                    {loading
                        ? <p>Loading hourly snow…</p>
                        : <>
                            <p>Press Load to get every source's snow for this town and these dates.</p>
                            <button type="button" onClick={onLoad}>Load</button>
                        </>}
                </>}

            {data?.message && <p className="error" role="alert">{data.message}</p>}

            {data && !data.message &&
                <>
                    <label className="snow-hourly-source">
                        Source
                        <select value={link.source} onChange={event => onSourceChange(event.target.value)}>
                            {!chosen && <option value={link.source}>{label}</option>}
                            {data.sources.map(source =>
                                <option key={source.id} value={source.id} disabled={source.status !== 'Ok'}>{source.label}</option>)}
                        </select>
                    </label>
                    {!chosen && <p className="error" role="alert">This source has no forecast for {data.name}.</p>}
                    {chosen && chosen.status !== 'Ok' && <p className="error" role="alert">{chosen.message ?? 'This source is unavailable right now.'}</p>}
                    {chosen && chosen.status === 'Ok' &&
                        <SourceDetail source={chosen} others={usable} start={link.start} end={link.end} now={data.now} />}
                </>}
        </main>
    );
}

function SourceDetail({ source, others, start, end, now }: {
    source: SnowHourlySource; others: SnowHourlySource[]; start: string; end: string; now: string;
}) {
    const periods = source.periods;
    const current = townNow(now, useNow());
    const falling = periods.filter(period => hourlyRate(period) >= fallingInchesPerHour);
    const total = totals(periods);
    const peak = falling.reduce<SnowPeriod | undefined>((best, period) => !best || hourlyRate(period) > hourlyRate(best) ? period : best, undefined);
    const blocks = periods.some(period => elapsedHours(period.start, period.end) > 1.5);
    const iceTotal = source.ice ? totals(source.ice).high : undefined;
    // Like the table, a forecast that doesn't cover every date gives a minimum.
    const atLeast = source.partial ? '≥ ' : '';

    // The table leaves out dry periods, and keeps a running total of the rest. Ice comes in its own
    // blocks, so each row takes only the share of each block that falls inside it.
    const rows = periods.map((period, index) => {
        const sofar = totals(periods.slice(0, index + 1));
        const ice = source.ice?.reduce((sum, part) => sum + overlapInches(part, period.start, period.end), 0);
        return { period, runningLow: sofar.low, runningHigh: sofar.high, ice };
    }).filter(row => row.period.highInches > 0 || (row.ice ?? 0) >= 0.005);

    return (
        <>
            <dl className="snow-hourly-summary">
                <div><dt>Total</dt><dd>{atLeast}{inchesText(total.low, total.high)}</dd></div>
                <div><dt>Snow falling</dt><dd>{falling.length > 0 ? timingText(falling[0].start, falling[falling.length - 1].end) : 'None'}</dd></div>
                {peak &&
                    <div>
                        <dt>Heaviest</dt>
                        <dd>
                            {hourlyRate(peak).toFixed(2)} in/hr {blocks ? `on average, ${timingText(peak.start, peak.end)}` : localDayTime(peak.start)}
                        </dd>
                    </div>}
                {iceTotal !== undefined && <div><dt>Ice</dt><dd>{iceTotal.toFixed(2)} in</dd></div>}
            </dl>

            <section className="snow-hourly-chart">
                <h2>Snow each {blocks ? 'period' : 'hour'}</h2>
                <PeriodBars periods={periods} ice={source.ice} start={start} end={end} />
                {blocks && <p className="table-note">This source forecasts in blocks of several hours, so each bar covers its whole block.</p>}
            </section>

            <section className="snow-hourly-chart">
                <h2>Running total</h2>
                <RunningTotals chosen={source} others={others} start={start} end={end} />
                <p className="table-note">
                    {source.label} is coloured; the grey lines are the other sources for comparison.
                    {current.slice(0, 10) >= start && current.slice(0, 10) <= end && ` It's now ${localDayTime(current)} in this town.`}
                </p>
            </section>

            <section className="snow-hourly-table">
                <h2>By {blocks ? 'period' : 'hour'}</h2>
                {rows.length === 0
                    ? <p>{source.label} has no snow for these dates.</p>
                    : <div className="table-scroll">
                        <table>
                            <thead>
                                <tr>
                                    <th scope="col" className="date-column">Time</th>
                                    <th scope="col">Snow</th>
                                    <th scope="col">Rate</th>
                                    <th scope="col">Running total</th>
                                    {source.ice && <th scope="col">Ice</th>}
                                </tr>
                            </thead>
                            <tbody>
                                {rows.map(({ period, runningLow: low, runningHigh: high, ice }) =>
                                    <tr key={period.start}>
                                        <th scope="row" className="date-column">{timingText(period.start, period.end)}</th>
                                        <td>{inchesText(period.lowInches, period.highInches, 2)}</td>
                                        <td className={`snow-rate ${intensity(hourlyRate(period))}`}>{hourlyRate(period).toFixed(2)} in/hr</td>
                                        <td>{inchesText(low, high)}</td>
                                        {source.ice && <td>{(ice ?? 0).toFixed(2)} in</td>}
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>}
                <p className="table-note">
                    Periods with no snow are left out. Times are the town's local time.
                    {source.ice && ' NWS forecasts ice in its own blocks, so each row takes its share of them.'}
                    {source.partial && " This forecast doesn't cover all of these dates, so its total is a minimum."}
                </p>
            </section>
        </>
    );
}

export default SnowHourly;
