import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { fetchAlmanac, type AlmanacDay, type AlmanacReport, type AlmanacSky } from '../api/almanac';
import { almanacAmount, almanacDate, almanacDeparture, almanacTabLabel, almanacTemperature, climateStationName, selectedAlmanacDate } from '../almanac';
import { formatTime } from '../localTime';
import type { Unit } from '../temperature';

// The data changes once a day, but a page left open overnight should move on to the new day.
const refreshMs = 60 * 60_000;

interface StationAlmanacProps {
    station: string;
    unit: Unit;
}

function AlmanacTable({ day, valueLabel, unit }: { day: AlmanacDay; valueLabel: string; unit: Unit }) {
    const rows: [string, string, string, string, number | null][] = [
        ['High', almanacTemperature(day.high.value, unit), almanacTemperature(day.high.average, unit), almanacTemperature(day.high.record, unit), day.high.recordYear],
        ['Low', almanacTemperature(day.low.value, unit), almanacTemperature(day.low.average, unit), almanacTemperature(day.low.record, unit), day.low.recordYear],
        ['Precipitation', almanacAmount(day.precip.value, 2), almanacAmount(day.precip.average, 2), almanacAmount(day.precip.record, 2), day.precip.recordYear],
        ['Snow', almanacAmount(day.snow.value, 1), almanacAmount(day.snow.average, 1), almanacAmount(day.snow.record, 1), day.snow.recordYear],
    ];

    const departures = [
        ['High', almanacDeparture(day.high.value, day.high.average, unit)],
        ['Low', almanacDeparture(day.low.value, day.low.average, unit)],
    ].filter(([, departure]) => departure !== null);

    return (
        <>
            <table>
                <caption>{almanacDate(day.date)}</caption>
                <thead>
                    <tr>
                        <td />
                        <th scope="col">{valueLabel}</th>
                        <th scope="col">Average</th>
                        <th scope="col">Record</th>
                    </tr>
                </thead>
                <tbody>
                    {rows.map(([label, value, average, record, year]) =>
                        <tr key={label}>
                            <th scope="row">{label}</th>
                            <td>{value}</td>
                            <td>{average}</td>
                            <td>
                                {record}
                                {year !== null && <span className="almanac-year">{year}</span>}
                            </td>
                        </tr>
                    )}
                </tbody>
            </table>
            {departures.length > 0 &&
                <p className="almanac-departure">
                    {departures.map(([label, departure]) => `${label} ${departure}.`).join(' ')}
                </p>}
        </>
    );
}

function presentRows(rows: [string, string | null][]): [string, string][] {
    return rows.filter((row): row is [string, string] => row[1] !== null);
}

function SkyGroup({ title, rows }: { title: string; rows: [string, string][] }) {
    return (
        <div>
            <h3>{title}</h3>
            <dl>
                {rows.map(([label, value]) =>
                    <div key={label}>
                        <dt>{label}</dt>
                        <dd>{value}</dd>
                    </div>
                )}
            </dl>
        </div>
    );
}

function AlmanacSkyTimes({ sky }: { sky: AlmanacSky }) {
    const { moon } = sky;
    const sunRows = presentRows([
        ['Sunrise', sky.sunrise && formatTime(sky.sunrise)],
        ['Sunset', sky.sunset && formatTime(sky.sunset)],
    ]);
    const moonRows = presentRows([
        ['Moonrise', moon.moonrise && formatTime(moon.moonrise)],
        ['Moonset', moon.moonset && formatTime(moon.moonset)],
        ['Phase', moon.phase],
        ['Visible', moon.illuminationPercent === null ? null : `${moon.illuminationPercent}%`],
    ]);

    if (sunRows.length === 0 && moonRows.length === 0) {
        return null;
    }

    return (
        <div className="almanac-sky">
            {sunRows.length > 0 && <SkyGroup title="Sun" rows={sunRows} />}
            {moonRows.length > 0 && <SkyGroup title="Moon" rows={moonRows} />}
        </div>
    );
}

/** Hides itself when the almanac can't be loaded, because it only adds detail. */
function StationAlmanac({ station, unit }: StationAlmanacProps) {
    const [report, setReport] = useState<AlmanacReport>();
    const [chosen, setChosen] = useState<string>();
    const [shownStation, setShownStation] = useState(station);
    const tabs = useRef<(HTMLButtonElement | null)[]>([]);

    // Clearing the report stops another station's almanac appearing under this one, and the new one starts on today.
    if (station !== shownStation) {
        setShownStation(station);
        setReport(undefined);
        setChosen(undefined);
    }

    useEffect(() => {
        const controller = new AbortController();

        function load() {
            fetchAlmanac(station, controller.signal)
                .then(setReport)
                .catch(() => {
                    if (!controller.signal.aborted) {
                        setReport(undefined);
                    }
                });
        }

        function loadIfVisible() {
            if (!document.hidden) {
                load();
            }
        }

        load();
        const timer = window.setInterval(loadIfVisible, refreshMs);

        return () => {
            controller.abort();
            window.clearInterval(timer);
        };
    }, [station]);

    if (report?.status !== 'Ok' || report.days.length === 0) {
        return null;
    }

    const days = report.days;
    const selected = selectedAlmanacDate(days.map(day => day.date), chosen, report.today);
    const day = days.find(other => other.date === selected) ?? days[0];

    // Arrow keys move between tabs, as in other tab lists.
    function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
        const step = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0;
        if (step === 0) {
            return;
        }

        event.preventDefault();
        const index = (days.indexOf(day) + step + days.length) % days.length;
        setChosen(days[index].date);
        tabs.current[index]?.focus();
    }

    return (
        <div className="almanac-row">
            <section className="station-card almanac-card">
                <div className="almanac-heading">
                    <h2 id="almanac-title">Almanac</h2>
                    <div className="almanac-tabs" role="tablist" aria-labelledby="almanac-title" onKeyDown={onKeyDown}>
                        {days.map((tab, index) =>
                            <button
                                key={tab.date}
                                ref={element => { tabs.current[index] = element; }}
                                type="button"
                                role="tab"
                                id={`almanac-tab-${tab.date}`}
                                aria-selected={tab === day}
                                aria-controls="almanac-panel"
                                aria-label={almanacDate(tab.date)}
                                tabIndex={tab === day ? 0 : -1}
                                onClick={() => setChosen(tab.date)}
                            >
                                {almanacTabLabel(tab.date)}
                            </button>
                        )}
                    </div>
                </div>

                <div id="almanac-panel" role="tabpanel" aria-labelledby={`almanac-tab-${day.date}`}>
                    <AlmanacTable
                        day={day}
                        valueLabel={report.today !== null && day.date < report.today ? 'Actual' : 'Forecast'}
                        unit={unit}
                    />
                    {day.sky && <AlmanacSkyTimes sky={day.sky} />}
                </div>

                {report.climateStation &&
                    <p className="almanac-source">
                        Forecast by <a href="https://open-meteo.com/">Open-Meteo.com</a> (<a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>). Averages, records{report.recordsSince !== null && ` since ${report.recordsSince}`} and past actuals from the {climateStationName(report.climateStation)} climate station, via ACIS.
                        {days.some(other => other.sky) && <> Moon data, and sunrise and sunset before today, from the <a href="https://aa.usno.navy.mil/">U.S. Naval Observatory</a>.</>}
                    </p>}
            </section>
        </div>
    );
}

export default StationAlmanac;
