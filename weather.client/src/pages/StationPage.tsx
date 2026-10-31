import { useEffect, useState } from 'react';
import { fetchStation, fetchStations, type StationMatch, type StationReport, type StationSummary } from '../api/station';
import CurrentConditions from '../components/CurrentConditions';
import StationChart from '../components/StationChart';
import StationAlerts from '../components/StationAlerts';
import StationLookup from '../components/StationLookup';
import StationPicker from '../components/StationPicker';
import UnitToggle from '../components/UnitToggle';
import { homeStation } from '../defaultStation';
import { maxSavedStations } from '../savedStations';
import { displayStationTemperature, type Unit } from '../temperature';
import type { Route } from '../useRoute';

const refreshMs = 60_000;

interface StationPageProps {
    selected: string;
    onStationChange: (id: string) => void;
    defaultStation: string;
    onDefaultChange: (id: string) => void;
    savedStations: StationSummary[];
    onStationSaved: (station: StationSummary) => void;
    onStationRemoved: (id: string) => void;
    unit: Unit;
    onUnitChange: (unit: Unit) => void;
    onNavigate: (route: Route) => void;
}

function StationPage({ selected, onStationChange, defaultStation, onDefaultChange, savedStations, onStationSaved, onStationRemoved, unit, onUnitChange, onNavigate }: StationPageProps) {
    const [stationList, setStationList] = useState<StationSummary[]>();
    const [report, setReport] = useState<StationReport>();
    const [error, setError] = useState<string>();
    const stations = stationList ?? [];

    useEffect(() => {
        const controller = new AbortController();

        fetchStations(controller.signal)
            .then(setStationList)
            .catch(() => {
                // The page still works for the home station without the list.
                if (!controller.signal.aborted) {
                    setStationList([{ id: homeStation, name: 'Home station' }]);
                }
            });

        return () => controller.abort();
    }, []);

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

    function handleStationChange(id: string) {
        // Clearing the report stops old readings appearing under the new station.
        setReport(undefined);
        setError(undefined);
        onStationChange(id);
    }

    function handleStationFound(match: StationMatch) {
        if (!stations.some(station => station.id === match.id)) {
            onStationSaved({ id: match.id, name: match.name });
        }

        if (match.id !== selected) {
            handleStationChange(match.id);
        }
    }

    function handleStationRemove() {
        setReport(undefined);
        setError(undefined);
        onStationRemoved(selected);
    }

    const pickerStations = [
        ...stations,
        ...savedStations
            .filter(saved => !stations.some(station => station.id === saved.id))
            .map(saved => ({ id: saved.id, name: `${saved.name} (${saved.id})` })),
    ];

    // Without the list, a configured default still has to appear as the selected station.
    if (!pickerStations.some(station => station.id === selected)) {
        pickerStations.push({ id: selected, name: selected });
    }

    const canRemove = savedStations.some(saved => saved.id === selected) && !stations.some(station => station.id === selected);

    const current = report?.current;
    const history = report?.history ?? [];
    const times = history.map(point => point.time);
    const range = report?.historyRange === 'Today' ? 'today' : 'last 24 hours';

    return (
        <main>
            <header>
                <h1>{current?.stationName ?? 'Weather Station'}</h1>
                {current && <p className="location">Updated {new Date(current.observedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</p>}
            </header>

            <StationAlerts station={selected} onNavigate={onNavigate} />

            <div className="controls">
                {stationList && pickerStations.length > 1 && <StationPicker stations={pickerStations} selected={selected} defaultStation={defaultStation} canRemove={canRemove} onChange={handleStationChange} onDefaultChange={onDefaultChange} onRemove={handleStationRemove} />}
                <UnitToggle unit={unit} onChange={onUnitChange} />
            </div>

            <StationLookup ready={stationList !== undefined} full={savedStations.length >= maxSavedStations} onFound={handleStationFound} />

            {error && <p className="error" role="alert">{error}</p>}
            {!report && !error && <p>Loading station…</p>}
            {report?.status === 'NeedsKey' && <p className="error" role="alert">Add your {report.source} key to see station data.</p>}
            {report && report.status !== 'Ok' && report.status !== 'NeedsKey' && <p className="error" role="alert">{report.message}</p>}
            {current?.batteryLow && <p className="error" role="alert">The outdoor sensor's battery is low.</p>}

            {current && <CurrentConditions reading={current} unit={unit} />}

            {report?.historyMessage && <p className="error" role="alert">History: {report.historyMessage}</p>}
            {history.length > 0 &&
                <div className="station-charts">
                    <StationChart
                        title="Temperature"
                        range={range}
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
                        range={range}
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
                        range={range}
                        unit="inHg"
                        times={times}
                        digits={2}
                        series={[{ label: 'Pressure', colour: 'var(--chart-1)', values: history.map(point => point.pressureInHg) }]}
                    />
                    <StationChart
                        title="Rain rate"
                        range={range}
                        unit="in/hr"
                        times={times}
                        zeroBased
                        digits={2}
                        series={[{ label: 'Rain rate', colour: 'var(--chart-1)', values: history.map(point => point.rainRateInPerHour) }]}
                    />
                </div>}

            {current &&
                <footer className="attribution">
                    Station data from {report?.source}
                </footer>}
        </main>
    );
}

export default StationPage;
