import type { SnowReportTable } from '../api/snow';
import { dayLabel, localDayTime } from '../snowTiming';
import { formatAmount, reportTowns, type StormView } from '../stormViews';

interface SnowReportsProps {
    view: StormView;
    reports: SnowReportTable | undefined;
    error: string | undefined;
    names: Map<string, string>;
}

const wording = {
    snow: { heading: 'Snow reports', none: 'snow totals', unit: 'in', note: 'Totals' },
    rain: { heading: 'Rain in the past week', none: 'rain totals', unit: 'in', note: 'Totals' },
    wind: { heading: 'Wind reports', none: 'gusts', unit: 'mph', note: 'Gusts' },
} as const;

/** "Tue 11/17 7 PM". A week of rain can hold the same weekday twice, so it needs the date. */
function datedTime(iso: string) {
    return `${dayLabel(iso.slice(0, 10))} ${localDayTime(iso).split(' ').slice(1).join(' ')}`;
}

/** What people have measured near each town so far. It only adds detail, so it's left out until the dates begin. */
function SnowReports({ view, reports, error, names }: SnowReportsProps) {
    if (view === 'depth') {
        return null;
    }

    const towns = reportTowns(reports, view);
    const problem = error ?? reports?.message;
    if (towns.length === 0 && !problem) {
        return null;
    }

    const words = wording[view];
    const withReports = towns.filter(town => town.reports.length > 0);

    return (
        <section className="snow-reports">
            <h2>{words.heading}</h2>
            {problem && <p className="error" role="alert">{problem}</p>}
            {reports && towns.length > 0 && withReports.length === 0 &&
                <p>
                    No {words.none} have been reported within {reports.radiusMiles} miles of these towns
                    {view === 'rain' ? ' in the past week.' : ' for these dates yet.'}
                </p>}
            {withReports.map(town =>
                <div key={town.zip} className="snow-report-town">
                    <h3>{names.get(town.zip) ?? town.zip}</h3>
                    <ul>
                        {town.reports.map(report =>
                            <li key={`${report.place}|${report.observedAt}`}>
                                <strong>{formatAmount(report.amount, view)} {words.unit}</strong>
                                <span>
                                    {report.place}, {report.distanceMiles.toFixed(1)} mi away · {view === 'rain' ? datedTime(report.observedAt) : localDayTime(report.observedAt)}
                                    {report.source && ` · ${report.source}`}
                                    {report.estimated && ' · estimated'}
                                </span>
                            </li>
                        )}
                    </ul>
                </div>
            )}
            {reports && towns.length > 0 && view === 'rain' &&
                <p className="table-note">
                    Rain totals sent to the National Weather Service by spotters, the public and its own observers, within {reports.radiusMiles} miles
                    of each town{reports.rainSince && ` since ${dayLabel(reports.rainSince)}`}, newest first, from
                    the <a href="https://mesonet.agron.iastate.edu/lsr/">Iowa Environmental Mesonet</a>. It's what has already fallen,
                    whatever dates you chose, which shows how wet the ground is before the storm. Each place shows its highest report on
                    each date; a report usually covers the previous 24 hours or a storm so far.
                </p>}
            {reports && towns.length > 0 && view !== 'rain' &&
                <p className="table-note">
                    {words.note} sent to the National Weather Service by spotters, the public and its own observers, within {reports.radiusMiles} miles
                    of each town, from the <a href="https://mesonet.agron.iastate.edu/lsr/">Iowa Environmental Mesonet</a>. Each place shows its
                    highest report so far, so an early report can be lower than the storm's final {view === 'wind' ? 'peak' : 'total'}.
                </p>}
        </section>
    );
}

export default SnowReports;
