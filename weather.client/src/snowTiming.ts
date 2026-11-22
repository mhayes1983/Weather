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
