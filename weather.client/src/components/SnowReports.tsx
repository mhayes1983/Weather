import type { SnowReportTable } from '../api/snow';
import { formatInches } from '../snowAverages';
import { localDayTime } from '../snowTiming';

interface SnowReportsProps {
    reports: SnowReportTable | undefined;
    error: string | undefined;
    names: Map<string, string>;
}

/** What people have measured near each town so far. It only adds detail, so it's left out until the dates begin. */
function SnowReports({ reports, error, names }: SnowReportsProps) {
    const towns = reports?.towns ?? [];
    const problem = error ?? reports?.message;
    if (towns.length === 0 && !problem) {
        return null;
    }

    const withReports = towns.filter(town => town.reports.length > 0);

    return (
        <section className="snow-reports">
            <h2>Snow reports</h2>
            {problem && <p className="error" role="alert">{problem}</p>}
            {reports && towns.length > 0 && withReports.length === 0 &&
                <p>No snow totals have been reported within {reports.radiusMiles} miles of these towns for these dates yet.</p>}
            {withReports.map(town =>
                <div key={town.zip} className="snow-report-town">
                    <h3>{names.get(town.zip) ?? town.zip}</h3>
                    <ul>
                        {town.reports.map(report =>
                            <li key={`${report.place}|${report.observedAt}`}>
                                <strong>{formatInches(report.inches)} in</strong>
                                <span>
                                    {report.place}, {report.distanceMiles.toFixed(1)} mi away · {localDayTime(report.observedAt)}
                                    {report.source && ` · ${report.source}`}
                                    {report.estimated && ' · estimated'}
                                </span>
                            </li>
                        )}
                    </ul>
                </div>
            )}
            {reports && towns.length > 0 &&
                <p className="table-note">
                    Totals sent to the National Weather Service by spotters, the public and its own observers, within {reports.radiusMiles} miles
                    of each town, from the <a href="https://mesonet.agron.iastate.edu/lsr/">Iowa Environmental Mesonet</a>. Each place shows its
                    highest report so far, so an early report can be lower than the storm's final total.
                </p>}
        </section>
    );
}

export default SnowReports;
