// Forecast times are the station's local wall time, so they're formatted without
// going through Date, which would shift them to the browser's time zone.
export function formatTime(time: string | null): string {
    if (!time) {
        return '—';
    }

    const clock = time.includes('T') ? time.slice(11) : time;
    const [hours, minutes] = clock.split(':').map(Number);
    return `${hours % 12 || 12}:${String(minutes).padStart(2, '0')} ${hours < 12 ? 'AM' : 'PM'}`;
}

export function formatDate(date: string, options: Intl.DateTimeFormatOptions): string {
    const [year, month, day] = date.split('-').map(Number);
    return new Date(year, month - 1, day).toLocaleDateString(undefined, options);
}
