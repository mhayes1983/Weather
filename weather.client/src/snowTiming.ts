// Times arrive in the town's local time with its offset, such as "2026-11-12T19:00:00-05:00". They're
// read as wall-clock text and never through a zoned Date, which would shift them to the browser's time zone.

interface WallTime {
    date: string;
    hours: number;
    minutes: number;
}

function wallTime(iso: string): WallTime {
    return { date: iso.slice(0, 10), hours: Number(iso.slice(11, 13)), minutes: Number(iso.slice(14, 16)) };
}

function weekday(date: string): string {
    const [year, month, day] = date.split('-').map(Number);
    return new Date(year, month - 1, day).toLocaleDateString('en-US', { weekday: 'short' });
}

function clock({ hours, minutes }: WallTime): string {
    const hour = hours % 12 || 12;
    const suffix = hours < 12 ? 'AM' : 'PM';
    return minutes === 0 ? `${hour} ${suffix}` : `${hour}:${String(minutes).padStart(2, '0')} ${suffix}`;
}

/** "Thu 7 PM", or "Thu 7:30 PM" off the hour. */
export function localDayTime(iso: string): string {
    const time = wallTime(iso);
    return `${weekday(time.date)} ${clock(time)}`;
}

// Midnight at the end of a day reads better as the end of that day than the start of the next.
function endLabel(iso: string, startDate: string): string {
    const time = wallTime(iso);
    if (time.hours === 0 && time.minutes === 0) {
        const [year, month, day] = time.date.split('-').map(Number);
        const previous = new Date(year, month - 1, day - 1);
        const sameDay = previous.getFullYear() === Number(startDate.slice(0, 4))
            && previous.getMonth() + 1 === Number(startDate.slice(5, 7))
            && previous.getDate() === Number(startDate.slice(8, 10));
        const name = previous.toLocaleDateString('en-US', { weekday: 'short' });
        return sameDay ? 'midnight' : `${name} midnight`;
    }

    return time.date === startDate ? clock(time) : `${weekday(time.date)} ${clock(time)}`;
}

/** "Thu 7 PM – Fri 9 AM", or "Thu 7 PM – 11 PM" within one day. */
export function timingText(start: string, end: string): string {
    return `${localDayTime(start)} – ${endLabel(end, wallTime(start).date)}`;
}

function wallMinutes(iso: string): number {
    const { date, hours, minutes } = wallTime(iso);
    const [year, month, day] = date.split('-').map(Number);
    return Date.UTC(year, month - 1, day, hours, minutes) / 60_000;
}

/**
 * Where a span sits between midnight starting `firstDate` and midnight ending `lastDate`, as percentages.
 * It uses wall-clock time, so a day with a clock change is drawn an hour out.
 */
export function timingSpan(start: string, end: string, firstDate: string, lastDate: string): { left: number; width: number } {
    const from = wallMinutes(`${firstDate}T00:00`);
    const length = wallMinutes(`${lastDate}T00:00`) + 24 * 60 - from;
    const clamp = (value: number) => Math.min(100, Math.max(0, value));

    const left = clamp((wallMinutes(start) - from) / length * 100);
    const right = clamp((wallMinutes(end) - from) / length * 100);
    return { left, width: Math.max(0, right - left) };
}

/** Every date from `first` to `last`, as YYYY-MM-DD. */
export function datesBetween(first: string, last: string): string[] {
    const [year, month, day] = first.split('-').map(Number);
    const dates: string[] = [];
    for (let offset = 0; offset < 32; offset++) {
        const date = new Date(Date.UTC(year, month - 1, day + offset)).toISOString().slice(0, 10);
        if (date > last) {
            break;
        }

        dates.push(date);
    }

    return dates;
}

/** "Thu 11/12" for a column of the timing chart. */
export function dayLabel(date: string): string {
    return `${weekday(date)} ${Number(date.slice(5, 7))}/${Number(date.slice(8, 10))}`;
}

/** Snow expected over one period, in the town's local time. */
export interface TimedAmount {
    start: string;
    end: string;
    lowInches: number;
    highInches: number;
}

/** Below this rate a period is flurries, matching Core's `SnowWindow.FallingInchesPerHour`. */
export const fallingInchesPerHour = 0.01;

/**
 * Real hours between two times, using their offsets. Unlike the wall clock, this is right across a clock
 * change, when 1 AM to 3 AM can be one hour or three.
 */
export function elapsedHours(start: string, end: string): number {
    return (Date.parse(end) - Date.parse(start)) / 3_600_000;
}

/** Inches an hour for a period, by its high amount, over the time that really passes. */
export function hourlyRate(period: TimedAmount): number {
    const hours = elapsedHours(period.start, period.end);
    return hours > 0 ? period.highInches / hours : 0;
}

/**
 * The high amount of `part` that falls between `from` and `to`, taking a share of a part that crosses
 * either edge. NWS ice comes in its own 6-hour blocks that rarely line up with its snow periods.
 */
export function overlapInches(part: TimedAmount, from: string, to: string): number {
    const start = Math.max(Date.parse(part.start), Date.parse(from));
    const end = Math.min(Date.parse(part.end), Date.parse(to));
    const length = Date.parse(part.end) - Date.parse(part.start);
    return end > start && length > 0 ? part.highInches * (end - start) / length : 0;
}

/**
 * The town's time now, worked out from the offset in the server's `now` so it keeps moving after the
 * data loads. A clock change since then leaves it an hour out until the data loads again.
 */
export function townNow(serverNow: string, nowMs: number): string {
    const offset = serverNow.slice(19).match(/([+-])(\d{2}):(\d{2})$/);
    const minutes = offset ? (offset[1] === '-' ? -1 : 1) * (Number(offset[2]) * 60 + Number(offset[3])) : 0;
    const wall = new Date(nowMs + minutes * 60_000).toISOString().slice(0, 16);
    return `${wall}:00${offset ? offset[0] : 'Z'}`;
}

export type Intensity = 'light' | 'moderate' | 'heavy';

export type RateView = 'snow' | 'rain' | 'wind' | 'depth';

/** Gusts from this speed count as windy, matching Core's `SnowWindow.WindyMph`. */
export const windyMph = 30;

/**
 * How hard it's coming down, or blowing:
 * - snow: light below a quarter inch an hour, heavy from an inch an hour, as the NWS describes snowfall rates
 * - rain: light below a tenth of an inch an hour, heavy from 0.3, as the AMS describes rainfall rates
 * - wind: light below 30 mph, heavy from 46, where the NWS issues a Wind Advisory for gusts
 */
export function intensity(rate: number, view: RateView = 'snow'): Intensity {
    switch (view) {
        case 'rain':
            return rate >= 0.3 ? 'heavy' : rate >= 0.1 ? 'moderate' : 'light';
        case 'wind':
            return rate >= 46 ? 'heavy' : rate >= windyMph ? 'moderate' : 'light';
        default:
            return rate >= 1 ? 'heavy' : rate >= 0.25 ? 'moderate' : 'light';
    }
}

/** A gust period, which counts by its speed rather than an amount spread over its hours. */
export interface TimedGust {
    start: string;
    end: string;
    mph: number;
}

/** Each period with snow falling, placed on the timing chart, oldest first. */
export function periodSegments(periods: TimedAmount[], firstDate: string, lastDate: string) {
    return periods
        .filter(period => hourlyRate(period) >= fallingInchesPerHour)
        .map(period => ({ ...timingSpan(period.start, period.end, firstDate, lastDate), rate: hourlyRate(period), period }))
        .filter(segment => segment.width > 0);
}

/** Each gust period of 30 mph or more, placed on the timing chart, oldest first. The rate is its speed. */
export function gustSegments(gusts: TimedGust[], firstDate: string, lastDate: string) {
    return gusts
        .filter(gust => gust.mph >= windyMph)
        .map(gust => ({ ...timingSpan(gust.start, gust.end, firstDate, lastDate), rate: gust.mph, period: gust }))
        .filter(segment => segment.width > 0);
}

/**
 * Gust periods as spans `agreementByHour` counts, keeping only the windy ones. Each is given one unit an hour,
 * which is over the falling rate, so every windy hour counts.
 */
export function windySpans(gusts: TimedGust[]): TimedAmount[] {
    return gusts
        .filter(gust => gust.mph >= windyMph)
        .map(gust => ({ start: gust.start, end: gust.end, lowInches: 1, highInches: Math.max(1, elapsedHours(gust.start, gust.end)) }));
}

/** How many hours the chart covers, from midnight starting `firstDate` to midnight ending `lastDate`. */
export function hoursBetween(firstDate: string, lastDate: string): number {
    return datesBetween(firstDate, lastDate).length * 24;
}

/** The wall-clock time `hour` hours after midnight starting `firstDate`, as "YYYY-MM-DDTHH:00". */
export function hourAt(firstDate: string, hour: number): string {
    const date = new Date(wallMinutes(`${firstDate}T00:00`) * 60_000 + hour * 3_600_000);
    return date.toISOString().slice(0, 13) + ':00';
}

/** For each hour of the chart, how many of the sources have snow falling at some point in it. */
export function agreementByHour(sources: TimedAmount[][], firstDate: string, lastDate: string): number[] {
    const from = wallMinutes(`${firstDate}T00:00`);
    const counts = new Array<number>(hoursBetween(firstDate, lastDate)).fill(0);
    for (const periods of sources) {
        const falling = new Set<number>();
        for (const period of periods) {
            if (hourlyRate(period) < fallingInchesPerHour) {
                continue;
            }

            const first = Math.max(0, Math.floor((wallMinutes(period.start) - from) / 60));
            const last = Math.min(counts.length, Math.ceil((wallMinutes(period.end) - from) / 60));
            for (let hour = first; hour < last; hour++) {
                falling.add(hour);
            }
        }

        falling.forEach(hour => counts[hour]++);
    }

    return counts;
}

/**
 * The running total at each hour of the chart, from 0 at the first midnight, taking a share of any
 * period an hour falls inside. Null after the source's last period ends, so its line stops there.
 */
export function cumulativeByHour(periods: TimedAmount[], firstDate: string, lastDate: string): (number | null)[] {
    const from = wallMinutes(`${firstDate}T00:00`);
    const hours = hoursBetween(firstDate, lastDate);
    const spans = periods.map(period => ({
        start: (wallMinutes(period.start) - from) / 60,
        end: (wallMinutes(period.end) - from) / 60,
        inches: period.highInches,
    }));
    const lastEnd = Math.max(0, ...spans.map(span => span.end));

    const values: (number | null)[] = [];
    for (let hour = 0; hour <= hours; hour++) {
        if (hour > Math.ceil(lastEnd) || spans.length === 0) {
            values.push(null);
            continue;
        }

        const total = spans.reduce((sum, span) => {
            const length = span.end - span.start;
            const covered = Math.min(hour, span.end) - span.start;
            return length <= 0 || covered <= 0 ? sum : sum + span.inches * Math.min(1, covered / length);
        }, 0);
        values.push(Math.round(total * 100) / 100);
    }

    return values;
}

/** "1.5–2 in", or "2 in" when the low and high agree. */
export function inchesText(low: number, high: number, digits = 1): string {
    const format = (value: number) => String(Number(value.toFixed(digits)));
    return low === high || format(low) === format(high) ? `${format(high)} in` : `${format(low)}–${format(high)} in`;
}

/** The parts of a snow cell the timing summary reads. */
export interface TimingCell {
    snowStart: string | null;
    snowEnd: string | null;
    timingLowInches: number | null;
    timingHighInches: number | null;
    peakStart: string | null;
    peakEnd: string | null;
    peakInchesPerHour: number | null;
}

/**
 * "Thu 7 PM – Fri 1 AM: 1.5–2 in · heaviest 0.8 in/hr Thu 8 PM" for a source's timing bar. Rain gives
 * its amounts to hundredths.
 */
export function spanSummary(cell: TimingCell, digits = 1): string {
    if (!cell.snowStart || !cell.snowEnd) {
        return '';
    }

    let span = timingText(cell.snowStart, cell.snowEnd);
    if (cell.timingHighInches !== null) {
        span += `: ${inchesText(cell.timingLowInches ?? cell.timingHighInches, cell.timingHighInches, digits)}`;
    }

    if (!cell.peakStart || !cell.peakEnd || cell.peakInchesPerHour === null) {
        return span;
    }

    // NWS and Weather Underground blocks only give an average rate.
    const hours = elapsedHours(cell.peakStart, cell.peakEnd);
    const peak = hours > 1
        ? `heaviest ${cell.peakInchesPerHour} in/hr on average, ${timingText(cell.peakStart, cell.peakEnd)}`
        : `heaviest ${cell.peakInchesPerHour} in/hr ${localDayTime(cell.peakStart)}`;
    return `${span} · ${peak}`;
}

/** The parts of a wind cell the timing summary reads. */
export interface WindTimingCell {
    maxMph: number | null;
    windyStart: string | null;
    windyEnd: string | null;
    peakStart: string | null;
    peakEnd: string | null;
}

/** "Thu 7 PM – Fri 1 AM: gusts to 48 mph, strongest Thu 11 PM" for a source's wind timing bar. */
export function windSummary(cell: WindTimingCell): string {
    if (!cell.windyStart || !cell.windyEnd) {
        return '';
    }

    const span = timingText(cell.windyStart, cell.windyEnd);
    if (cell.maxMph === null || !cell.peakStart || !cell.peakEnd) {
        return span;
    }

    const when = elapsedHours(cell.peakStart, cell.peakEnd) > 1
        ? timingText(cell.peakStart, cell.peakEnd)
        : localDayTime(cell.peakStart);
    return `${span}: gusts to ${Math.round(cell.maxMph)} mph, strongest ${when}`;
}
