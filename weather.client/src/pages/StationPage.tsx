import { useEffect, useState } from 'react';
import { fetchStation, type StationReport } from '../api/station';
import CurrentConditions from '../components/CurrentConditions';
import StationChart from '../components/StationChart';
import StationAlerts from '../components/StationAlerts';
import StationHeader, { type StationControls } from '../components/StationHeader';
import { displayStationTemperature, type Unit } from '../temperature';
import type { Route } from '../useRoute';

const refreshMs = 60_000;

interface StationPageProps {
    selected: string;
    stationControls: StationControls;
    unit: Unit;
    onNavigate: (route: Route) => void;
}

function StationPage({ selected, stationControls, unit, onNavigate }: StationPageProps) {
    const [report, setReport] = useState<StationReport>();
    const [error, setError] = useState<string>();
    const [shownStation, setShownStation] = useState(selected);

    // Clearing the report stops old readings appearing under the new station.
    if (selected !== shownStation) {
        setShownStation(selected);
        setReport(undefined);
        setError(undefined);
    }

    useEffect(() => {
        const controller = new AbortController();

        function load() {
            fetchStation(selected, controller.signal)
                .then(data => {
                    setReport(data);
                    setError(undefined);
                })
                .catch((reason: Error) => {
                    if (!controller.signal.aborted) {
                        setError(reason.message);
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
        document.addEventListener('visibilitychange', loadIfVisible);

        return () => {
            controller.abort();
            window.clearInterval(timer);
            document.removeEventListener('visibilitychange', loadIfVisible);
        };
    }, [selected]);

    const current = report?.current;
    const history = report?.history ?? [];
    const times = history.map(point => point.time);
    const range = report?.historyRange === 'Today' ? 'today' : 'last 24 hours';
    const maxGapMinutes = report?.historyMaxGapMinutes ?? 30;

    return (
        <main>
            <StationHeader {...stationControls}>
                <h1>{current?.stationName ?? 'Weather Station'}</h1>
                {current && <p className="location">Updated {new Date(current.observedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</p>}
                {report?.modelled && <p className="location">Modelled estimate, not measured</p>}
            </StationHeader>

            <StationAlerts station={selected} onNavigate={onNavigate} />

            {error && <p className="error" role="alert">{error}</p>}
            {!report && !error && <p>Loading station…</p>}
            {report?.status === 'NeedsKey' && <p className="error" role="alert">Add your {report.source} key to see station data.</p>}
            {report && report.status !== 'Ok' && report.status !== 'NeedsKey' && <p className="error" role="alert">{report.message}</p>}
            {current?.batteryLow && <p className="error" role="alert">The outdoor sensor's battery is low.</p>}

            {current && <CurrentConditions reading={current} sky={report?.sky ?? null} unit={unit} />}

            {report?.historyMessage && <p className="error" role="alert">History: {report.historyMessage}</p>}
            {history.length > 0 &&
                <div className="station-charts">
                    <StationChart
                        title="Temperature"
                        range={range}
                        unit={`°${unit}`}
                        times={times}
                        maxGapMinutes={maxGapMinutes}
                        digits={1}
                        series={[{
                            label: 'Temperature',
                            colour: 'var(--chart-1)',
                            values: history.map(point => point.temperatureF === null ? null : displayStationTemperature(point.temperatureF, unit)),
                        }]}
                    />
                    <StationChart
                        title="Wind"
                        range={range}
                        unit="mph"
                        times={times}
                        maxGapMinutes={maxGapMinutes}
                        zeroBased
                        series={[
                            { label: 'Speed', colour: 'var(--chart-1)', values: history.map(point => point.windSpeedMph) },
                            { label: 'Gust', colour: 'var(--chart-2)', values: history.map(point => point.windGustMph) },
                        ]}
                    />
                    <StationChart
                        title="Pressure"
                        range={range}
                        unit="inHg"
                        times={times}
                        maxGapMinutes={maxGapMinutes}
                        digits={2}
                        series={[{ label: 'Pressure', colour: 'var(--chart-1)', values: history.map(point => point.pressureInHg) }]}
                    />
                    <StationChart
                        title="Rain rate"
                        range={range}
                        unit="in/hr"
                        times={times}
                        maxGapMinutes={maxGapMinutes}
                        zeroBased
                        digits={2}
                        series={[{ label: 'Rain rate', colour: 'var(--chart-1)', values: history.map(point => point.rainRateInPerHour) }]}
                    />
                </div>}

            {current &&
                <footer className="attribution">
                    {report?.modelled
                        ? <>Modelled data by <a href="https://open-meteo.com/">Open-Meteo.com</a> (<a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>)</>
                        : <>Station data from {report?.source}{report?.sky && <>. Sky conditions modelled by <a href="https://open-meteo.com/">Open-Meteo.com</a> (<a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>)</>}</>}
                </footer>}
        </main>
    );
}

export default StationPage;
