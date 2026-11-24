import type { DepthPoint, GustPeriod, SnowHourlySource, SnowPeriod } from '../api/snow';
import type { HourlyLink } from '../snowLink';
import {
    cumulativeByHour, dayLabel, elapsedHours, fallingInchesPerHour, hourAt, hourlyRate, hoursBetween, inchesText, intensity,
    localDayTime, overlapInches, timingSpan, timingText, townNow, windyMph,
} from '../snowTiming';
import { formatAmount, type StormView } from '../stormViews';
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

const nouns: Record<StormView, string> = { snow: 'snow', rain: 'rain', wind: 'wind', depth: 'snow depth' };

/** Whether a source has anything for a view, so the source list can leave out the ones that don't. */
function hasView(source: SnowHourlySource, view: StormView) {
    return source.status === 'Ok' && (view === 'snow' || (view === 'rain' ? source.liquid : view === 'wind' ? source.gusts : source.depth) !== null);
}

/** The amounts a view adds up: snow, or rain and melted snow. */
function amounts(source: SnowHourlySource, view: 'snow' | 'rain'): SnowPeriod[] {
    return view === 'rain' ? source.liquid ?? [] : source.periods;
}

function DayGrid({ x, start, end }: { x: (hour: number) => number; start: string; end: string }) {
    const days = hoursBetween(start, end) / 24;
    return (
        <>
            {Array.from({ length: days }, (_, day) =>
                <g key={day}>
                    <line className="chart-grid" x1={x(day * 24)} x2={x(day * 24)} y1={top} y2={height - bottom} />
                    <text className="chart-label" x={x(day * 24 + 12)} y={height - 6} textAnchor="middle">
                        {dayLabel(hourAt(start, day * 24).slice(0, 10))}
                    </text>
                </g>
            )}
        </>
    );
}

function ValueTicks({ largest, y, digits }: { largest: number; y: (value: number) => number; digits: number }) {
    return (
        <>
            {[0, largest / 2, largest].map(tick =>
                <g key={tick}>
                    <line className="chart-grid" x1={left} x2={width - right} y1={y(tick)} y2={y(tick)} />
                    <text className="chart-label" x={left - 6} y={y(tick) + 4} textAnchor="end">{tick.toFixed(digits)}</text>
                </g>
            )}
        </>
    );
}

/** Each period's amount, or gust, as a bar as wide as the period, with NWS ice beside the snow. */
function PeriodBars({ bars, ice, view, start, end, label }: {
    bars: { start: string; end: string; value: number; low: number; rate: number }[];
    ice: SnowPeriod[] | null;
    view: 'snow' | 'rain' | 'wind';
    start: string;
    end: string;
    label: string;
}) {
    const largest = Math.max(view === 'wind' ? 10 : 0.1, ...bars.map(bar => bar.value), ...(ice ?? []).map(period => period.highInches));
    const plotWidth = width - left - right;
    const plotHeight = height - top - bottom;
    const hours = hoursBetween(start, end);
    const y = (value: number) => height - bottom - value / largest * plotHeight;
    const unit = view === 'wind' ? 'mph' : 'in';

    function rect(bar: { start: string; end: string; value: number; low: number }, className: string, text: string) {
        const span = timingSpan(bar.start, bar.end, start, end);
        return (
            <rect
                key={`${className}${bar.start}`}
                className={className}
                x={left + span.left / 100 * plotWidth + 0.5}
                width={Math.max(1, span.width / 100 * plotWidth - 1)}
                y={y(bar.value)}
                height={Math.max(0, height - bottom - y(bar.value))}
            >
                <title>{`${timingText(bar.start, bar.end)}: ${text}`}</title>
            </rect>
        );
    }

    return (
        <>
            <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={label}>
                <ValueTicks largest={largest} y={y} digits={view === 'wind' ? 0 : largest < 1 ? 2 : 1} />
                <DayGrid x={hour => left + hour / hours * plotWidth} start={start} end={end} />
                {bars.filter(bar => bar.value > 0).map(bar => rect(
                    bar,
                    `snow-bar ${intensity(bar.rate, view)}`,
                    view === 'wind' ? `${Math.round(bar.value)} mph` : inchesText(bar.low, bar.value, 2)))}
                {ice?.filter(period => period.highInches > 0).map(period =>
                    rect({ start: period.start, end: period.end, value: period.highInches, low: period.lowInches }, 'ice-bar', inchesText(period.lowInches, period.highInches, 2)))}
            </svg>
            <p className="chart-legend">
                <span><i className="snow-bar-key" />{view === 'wind' ? 'Gust' : view === 'rain' ? 'Rain' : 'Snow'}</span>
                {ice && <span><i className="ice-bar-key" />Ice</span>}
                <span>{unit}</span>
            </p>
        </>
    );
}

/** A value for each hour of the chart from a source's periods or points, or null where it has none. */
function hourlyValues(source: SnowHourlySource, view: StormView, start: string, end: string): (number | null)[] {
    const hours = hoursBetween(start, end);
    if (view === 'snow' || view === 'rain') {
        return cumulativeByHour(amounts(source, view), start, end);
    }

    const values = new Array<number | null>(hours + 1).fill(null);
    const from = Date.parse(`${start}T00:00:00Z`);
    // Times are the town's wall clock, read as if UTC, the same way the chart places them.
    const hourOf = (iso: string) => (Date.parse(`${iso.slice(0, 19)}Z`) - from) / 3_600_000;
    if (view === 'wind') {
        for (const gust of source.gusts ?? []) {
            for (let hour = Math.max(0, Math.floor(hourOf(gust.start))); hour < Math.min(hours, Math.ceil(hourOf(gust.end))); hour++) {
                values[hour] = Math.max(values[hour] ?? 0, gust.mph);
            }
        }
    } else {
        for (const point of source.depth ?? []) {
            const hour = Math.round(hourOf(point.time));
            if (hour >= 0 && hour <= hours) {
                values[hour] = point.inches;
            }
        }
    }

    return values;
}

/** Each source as a line across the dates, with the chosen source drawn over the others in grey. */
function SourceLines({ chosen, others, view, start, end }: {
    chosen: SnowHourlySource; others: SnowHourlySource[]; view: StormView; start: string; end: string;
}) {
    const hours = hoursBetween(start, end);
    const lines = [...others.filter(other => other.id !== chosen.id), chosen]
        .map(source => ({ source, values: hourlyValues(source, view, start, end) }));
    const largest = Math.max(view === 'wind' ? 10 : 0.1, ...lines.flatMap(line => line.values.filter(value => value !== null)));
    const plotWidth = width - left - right;
    const plotHeight = height - top - bottom;
    const x = (hour: number) => left + hour / hours * plotWidth;
    const y = (value: number) => height - bottom - value / largest * plotHeight;
    const unit = view === 'wind' ? 'mph' : 'in';

    // A gap in a source's values starts a new piece of its line rather than joining across it.
    function path(values: (number | null)[]) {
        return values.flatMap((value, hour) => value === null
            ? []
            : [`${hour === 0 || values[hour - 1] === null ? 'M' : 'L'}${x(hour).toFixed(1)} ${y(value).toFixed(1)}`]).join(' ');
    }

    const last = (values: (number | null)[]) => values.findLast(value => value !== null) ?? 0;
    const describe = (values: (number | null)[]) => view === 'wind'
        ? `strongest ${Math.round(Math.max(0, ...values.filter(value => value !== null)))} mph`
        : view === 'depth' ? `${formatAmount(last(values), 'depth')} in at the end` : `${last(values)} in`;
    const chosenValues = lines[lines.length - 1].values;

    return (
        <>
            <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`${chosen.label}: ${describe(chosenValues)}; grey lines are the other sources.`}>
                <ValueTicks largest={largest} y={y} digits={view === 'wind' ? 0 : largest < 1 ? 2 : 1} />
                <DayGrid x={x} start={start} end={end} />
                {lines.map(({ source, values }) =>
                    <path key={source.id} className={source.id === chosen.id ? 'chart-line' : 'chart-line snow-other-line'} d={path(values)}>
                        <title>{`${source.label}: ${describe(values)}`}</title>
                    </path>
                )}
            </svg>
            <p className="chart-legend">
                <span><i className="snow-bar-key" />{chosen.label}</span>
                <span><i className="snow-other-key" />Other sources</span>
                <span>{unit}</span>
            </p>
        </>
    );
}

function totals(periods: SnowPeriod[]) {
    return periods.reduce(
        (sum, period) => ({ low: sum.low + period.lowInches, high: sum.high + period.highInches }),
        { low: 0, high: 0 });
}

/** One source's snow, rain, wind or snow depth for one town, period by period, beside every other source. */
function SnowHourly({ link, state, loading, onLoad, onSourceChange, onBack }: SnowHourlyProps) {
    const view = link.view;
    const data = state?.data;
    const usable = data?.sources.filter(source => hasView(source, view)) ?? [];
    const chosen = data?.sources.find(source => source.id === link.source);
    const label = chosen?.label ?? link.source.toUpperCase();
    const dates = link.start === link.end ? dayLabel(link.start) : `${dayLabel(link.start)} – ${dayLabel(link.end)}`;
    const noun = nouns[view];

    return (
        <main>
            <p className="back-link">
                <a href="/storm" onClick={event => { event.preventDefault(); onBack(); }}>← Storm Forecast</a>
            </p>
            <header>
                <h1>{label} hourly {noun}</h1>
                <p className="location">{data?.name ?? link.zip} · {dates}</p>
            </header>

            {!data &&
                <>
                    {state?.error && <p className="error" role="alert">{state.error}</p>}
                    {loading
                        ? <p>Loading hourly forecast…</p>
                        : <>
                            <p>Press Load to get every source's forecast for this town and these dates.</p>
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
                                <option key={source.id} value={source.id} disabled={!hasView(source, view)}>{source.label}</option>)}
                        </select>
                    </label>
                    {!chosen && <p className="error" role="alert">This source has no forecast for {data.name}.</p>}
                    {chosen && chosen.status !== 'Ok' && <p className="error" role="alert">{chosen.message ?? 'This source is unavailable right now.'}</p>}
                    {chosen && chosen.status === 'Ok' && !hasView(chosen, view) &&
                        <p className="error" role="alert">{chosen.label} doesn't forecast {noun}.</p>}
                    {chosen && hasView(chosen, view) && (view === 'snow' || view === 'rain') &&
                        <AmountDetail source={chosen} others={usable} view={view} start={link.start} end={link.end} now={data.now} />}
                    {chosen && hasView(chosen, view) && view === 'wind' &&
                        <WindDetail source={chosen} gusts={chosen.gusts ?? []} others={usable} start={link.start} end={link.end} />}
                    {chosen && hasView(chosen, view) && view === 'depth' &&
                        <DepthDetail source={chosen} depth={chosen.depth ?? []} others={usable} start={link.start} end={link.end} now={data.now} />}
                </>}
        </main>
    );
}

function AmountDetail({ source, others, view, start, end, now }: {
    source: SnowHourlySource; others: SnowHourlySource[]; view: 'snow' | 'rain'; start: string; end: string; now: string;
}) {
    const periods = amounts(source, view);
    const ice = view === 'snow' ? source.ice : null;
    const current = townNow(now, useNow());
    const falling = periods.filter(period => hourlyRate(period) >= fallingInchesPerHour);
    const total = totals(periods);
    const peak = falling.reduce<SnowPeriod | undefined>((best, period) => !best || hourlyRate(period) > hourlyRate(best) ? period : best, undefined);
    const blocks = periods.some(period => elapsedHours(period.start, period.end) > 1.5);
    const iceTotal = ice ? totals(ice).high : undefined;
    const digits = view === 'rain' ? 2 : 1;
    const noun = view === 'rain' ? 'Rain' : 'Snow';
    // Like the table, a forecast that doesn't cover every date gives a minimum.
    const atLeast = (view === 'rain' ? source.liquidPartial : source.partial) ? '≥ ' : '';

    // The table leaves out dry periods, and keeps a running total of the rest. Ice comes in its own
    // blocks, so each row takes only the share of each block that falls inside it.
    const rows = periods.map((period, index) => {
        const sofar = totals(periods.slice(0, index + 1));
        const iceShare = ice?.reduce((sum, part) => sum + overlapInches(part, period.start, period.end), 0);
        return { period, runningLow: sofar.low, runningHigh: sofar.high, ice: iceShare };
    }).filter(row => row.period.highInches > 0 || (row.ice ?? 0) >= 0.005);

    return (
        <>
            <dl className="snow-hourly-summary">
                <div><dt>Total</dt><dd>{atLeast}{inchesText(total.low, total.high, digits)}</dd></div>
                <div><dt>{noun} falling</dt><dd>{falling.length > 0 ? timingText(falling[0].start, falling[falling.length - 1].end) : 'None'}</dd></div>
                {peak &&
                    <div>
                        <dt>Heaviest</dt>
                        <dd>
                            {hourlyRate(peak).toFixed(2)} in/hr {blocks ? `on average, ${timingText(peak.start, peak.end)}` : localDayTime(peak.start)}
                        </dd>
                    </div>}
                {iceTotal !== undefined && <div><dt>Ice</dt><dd>{iceTotal.toFixed(2)} in</dd></div>}
            </dl>
            {view === 'rain' && <p className="table-note">Rain here is all precipitation as water, so it counts melted snow too.</p>}

            <section className="snow-hourly-chart">
                <h2>{noun} each {blocks ? 'period' : 'hour'}</h2>
                <PeriodBars
                    bars={periods.map(period => ({ start: period.start, end: period.end, value: period.highInches, low: period.lowInches, rate: hourlyRate(period) }))}
                    ice={ice}
                    view={view}
                    start={start}
                    end={end}
                    label={`${noun} for each period, ${inchesText(total.low, total.high, digits)} in all.`}
                />
                {blocks && <p className="table-note">This source forecasts in blocks of several hours, so each bar covers its whole block.</p>}
            </section>

            <section className="snow-hourly-chart">
                <h2>Running total</h2>
                <SourceLines chosen={source} others={others} view={view} start={start} end={end} />
                <p className="table-note">
                    {source.label} is coloured; the grey lines are the other sources for comparison.
                    {current.slice(0, 10) >= start && current.slice(0, 10) <= end && ` It's now ${localDayTime(current)} in this town.`}
                </p>
            </section>

            <section className="snow-hourly-table">
                <h2>By {blocks ? 'period' : 'hour'}</h2>
                {rows.length === 0
                    ? <p>{source.label} has no {noun.toLowerCase()} for these dates.</p>
                    : <div className="table-scroll">
                        <table>
                            <thead>
                                <tr>
                                    <th scope="col" className="date-column">Time</th>
                                    <th scope="col">{noun}</th>
                                    <th scope="col">Rate</th>
                                    <th scope="col">Running total</th>
                                    {ice && <th scope="col">Ice</th>}
                                </tr>
                            </thead>
                            <tbody>
                                {rows.map(({ period, runningLow: low, runningHigh: high, ice: iceShare }) =>
                                    <tr key={period.start}>
                                        <th scope="row" className="date-column">{timingText(period.start, period.end)}</th>
                                        <td>{inchesText(period.lowInches, period.highInches, 2)}</td>
                                        <td className={`snow-rate ${intensity(hourlyRate(period), view)}`}>{hourlyRate(period).toFixed(2)} in/hr</td>
                                        <td>{inchesText(low, high, digits)}</td>
                                        {ice && <td>{(iceShare ?? 0).toFixed(2)} in</td>}
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>}
                <p className="table-note">
                    Periods with no {noun.toLowerCase()} are left out. Times are the town's local time.
                    {ice && ' NWS forecasts ice in its own blocks, so each row takes its share of them.'}
                    {atLeast && " This forecast doesn't cover all of these dates, so its total is a minimum."}
                </p>
            </section>
        </>
    );
}

function WindDetail({ source, gusts, others, start, end }: {
    source: SnowHourlySource; gusts: GustPeriod[]; others: SnowHourlySource[]; start: string; end: string;
}) {
    const windy = gusts.filter(gust => gust.mph >= windyMph);
    const strongest = gusts.reduce<GustPeriod | undefined>((best, gust) => !best || gust.mph > best.mph ? gust : best, undefined);
    const blocks = gusts.some(gust => elapsedHours(gust.start, gust.end) > 1.5);

    return (
        <>
            <dl className="snow-hourly-summary">
                <div><dt>Strongest gust</dt><dd>{strongest ? `${source.gustsPartial ? '≥ ' : ''}${Math.round(strongest.mph)} mph` : 'None'}</dd></div>
                {strongest && <div><dt>When</dt><dd>{blocks ? timingText(strongest.start, strongest.end) : localDayTime(strongest.start)}</dd></div>}
                <div><dt>Gusts of {windyMph} mph or more</dt><dd>{windy.length > 0 ? timingText(windy[0].start, windy[windy.length - 1].end) : 'None'}</dd></div>
            </dl>

            <section className="snow-hourly-chart">
                <h2>Gusts each {blocks ? 'period' : 'hour'}</h2>
                <PeriodBars
                    bars={gusts.map(gust => ({ start: gust.start, end: gust.end, value: gust.mph, low: gust.mph, rate: gust.mph }))}
                    ice={null}
                    view="wind"
                    start={start}
                    end={end}
                    label={strongest ? `Gusts for each period, strongest ${Math.round(strongest.mph)} mph.` : 'No gusts forecast.'}
                />
            </section>

            <section className="snow-hourly-chart">
                <h2>Every source</h2>
                <SourceLines chosen={source} others={others} view="wind" start={start} end={end} />
                <p className="table-note">{source.label} is coloured; the grey lines are the other sources' gusts for comparison.</p>
            </section>

            <section className="snow-hourly-table">
                <h2>By {blocks ? 'period' : 'hour'}</h2>
                {windy.length === 0
                    ? <p>{source.label} has no gusts of {windyMph} mph or more for these dates.</p>
                    : <div className="table-scroll">
                        <table>
                            <thead>
                                <tr>
                                    <th scope="col" className="date-column">Time</th>
                                    <th scope="col">Gust</th>
                                </tr>
                            </thead>
                            <tbody>
                                {windy.map(gust =>
                                    <tr key={gust.start}>
                                        <th scope="row" className="date-column">{timingText(gust.start, gust.end)}</th>
                                        <td className={`snow-rate ${intensity(gust.mph, 'wind')}`}>{Math.round(gust.mph)} mph</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>}
                <p className="table-note">
                    Gusts under {windyMph} mph are left out. Heavy shading is from 46 mph, when the NWS issues a Wind Advisory. Times are
                    the town's local time.
                    {source.gustsPartial && " This forecast doesn't cover all of these dates, so a stronger gust could follow."}
                </p>
            </section>
        </>
    );
}

function DepthDetail({ source, depth, others, start, end, now }: {
    source: SnowHourlySource; depth: DepthPoint[]; others: SnowHourlySource[]; start: string; end: string; now: string;
}) {
    const current = townNow(now, useNow());
    const deepest = depth.reduce<DepthPoint | undefined>((best, point) => !best || point.inches > best.inches ? point : best, undefined);
    // Every sixth hour keeps the table short; the chart has every hour.
    const rows = depth.filter(point => Number(point.time.slice(11, 13)) % 6 === 0);

    return (
        <>
            <dl className="snow-hourly-summary">
                <div><dt>Most</dt><dd>{deepest ? `${formatAmount(deepest.inches, 'depth')} in` : 'None'}</dd></div>
                {deepest && <div><dt>When</dt><dd>{localDayTime(deepest.time)}</dd></div>}
                {depth.length > 0 && <div><dt>At the end</dt><dd>{formatAmount(depth[depth.length - 1].inches, 'depth')} in</dd></div>}
            </dl>
            <p className="table-note">Snow depth is modelled from the model's own snow and melt, never measured.</p>

            <section className="snow-hourly-chart">
                <h2>Snow on the ground</h2>
                <SourceLines chosen={source} others={others} view="depth" start={start} end={end} />
                <p className="table-note">
                    {source.label} is coloured; the grey lines are the other models for comparison.
                    {current.slice(0, 10) >= start && current.slice(0, 10) <= end && ` It's now ${localDayTime(current)} in this town.`}
                </p>
            </section>

            <section className="snow-hourly-table">
                <h2>Every 6 hours</h2>
                <div className="table-scroll">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col" className="date-column">Time</th>
                                <th scope="col">Depth</th>
                            </tr>
                        </thead>
                        <tbody>
                            {rows.map(point =>
                                <tr key={point.time}>
                                    <th scope="row" className="date-column">{localDayTime(point.time)}</th>
                                    <td>{formatAmount(point.inches, 'depth')} in</td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>
            </section>
        </>
    );
}

export default SnowHourly;
