import { useEffect, useState } from 'react';
import { fetchStation, type StationReport } from '../api/station';
import CurrentConditions from '../components/CurrentConditions';
import StationChart from '../components/StationChart';
import UnitToggle from '../components/UnitToggle';
import { displayStationTemperature, type Unit } from '../temperature';

const refreshMs = 60_000;

interface StationPageProps {
    unit: Unit;
    onUnitChange: (unit: Unit) => void;
}

function StationPage({ unit, onUnitChange }: StationPageProps) {
    const [report, setReport] = useState<StationReport>();
    const [error, setError] = useState<string>();

    useEffect(() => {
        const controller = new AbortController();

        function load() {
            fetchStation(controller.signal)
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
    }, []);

    const current = report?.current;
    const history = report?.history ?? [];
    const times = history.map(point => point.time);

    return (
        <main>
            <header>
                <h1>{current?.stationName ?? 'Weather Station'}</h1>
                {current && <p className="location">Updated {new Date(current.observedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</p>}
            </header>

            <div className="controls">
                <UnitToggle unit={unit} onChange={onUnitChange} />
            </div>

            {error && <p className="error" role="alert">{error}</p>}
            {!report && !error && <p>Loading station…</p>}
            {report?.status === 'NeedsKey' && <p className="error" role="alert">Add your Ambient Weather keys to see station data.</p>}
            {report && report.status !== 'Ok' && report.status !== 'NeedsKey' && <p className="error" role="alert">{report.message}</p>}
            {current?.batteryLow && <p className="error" role="alert">The outdoor sensor's battery is low.</p>}

            {current && <CurrentConditions reading={current} unit={unit} />}

            {report?.historyMessage && <p className="error" role="alert">24-hour history: {report.historyMessage}</p>}
            {history.length > 0 &&
                <div className="station-charts">
                    <StationChart
                        title="Temperature"
                        unit={`°${unit}`}
                        times={times}
                        digits={1}
                        series={[{
                            label: 'Temperature',
                            colour: 'var(--chart-1)',
                            values: history.map(point => point.temperatureF === null ? null : displayStationTemperature(point.temperatureF, unit)),
                        }]}
                    />
                    <StationChart
                        title="Wind"
                        unit="mph"
                        times={times}
                        zeroBased
                        series={[
                            { label: 'Speed', colour: 'var(--chart-1)', values: history.map(point => point.windSpeedMph) },
                            { label: 'Gust', colour: 'var(--chart-2)', values: history.map(point => point.windGustMph) },
                        ]}
                    />
                    <StationChart
                        title="Pressure"
                        unit="inHg"
                        times={times}
                        digits={2}
                        series={[{ label: 'Pressure', colour: 'var(--chart-1)', values: history.map(point => point.pressureInHg) }]}
                    />
                    <StationChart
                        title="Rain rate"
                        unit="in/hr"
                        times={times}
                        zeroBased
                        digits={2}
                        series={[{ label: 'Rain rate', colour: 'var(--chart-1)', values: history.map(point => point.rainRateInPerHour) }]}
                    />
                </div>}

            {current &&
                <footer className="attribution">
                    Station data from <a href="https://ambientweather.net/">AmbientWeather.net</a>
                </footer>}
        </main>
    );
}

export default StationPage;
