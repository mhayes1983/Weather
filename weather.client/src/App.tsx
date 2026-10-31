import { lazy, Suspense, useEffect, useState } from 'react';
import { fetchStations, type StationSummary } from './api/station';
import ErrorBoundary from './components/ErrorBoundary';
import NavBar from './components/NavBar';
import { homeStation, loadDefaultStation, saveDefaultStation } from './defaultStation';
import AlertsPage from './pages/AlertsPage';
import DailyForecastPage from './pages/DailyForecastPage';
import ForecastDayPage from './pages/ForecastDayPage';
import ForecastPage from './pages/ForecastPage';
import StationPage from './pages/StationPage';
import { addSavedStation, loadSavedStations, removeSavedStation, watchSavedStations } from './savedStations';
import type { Unit } from './temperature';
import { forecastDate, useRoute, type Route } from './useRoute';
import './App.css';

// Leaflet is large, so it only loads when the radar page opens.
const RadarPage = lazy(() => import('./pages/RadarPage'));

const titles: Partial<Record<Route, string>> = {
    '/': 'Weather Station',
    '/alerts': 'Weather Alerts',
    '/forecast': 'Forecast Comparison',
    '/radar': 'Weather Radar',
};

function App() {
    const [route, navigate] = useRoute();
    const [unit, setUnit] = useState<Unit>('F');
    const [defaultStation, setDefaultStation] = useState(loadDefaultStation);
    const [station, setStation] = useState(defaultStation);
    const [savedStations, setSavedStations] = useState(loadSavedStations);
    const [forecastOpened, setForecastOpened] = useState(route === '/forecast');

    // The forecast loads on first visit, then stays mounted to keep its ZIP and columns.
    if (route === '/forecast' && !forecastOpened) {
        setForecastOpened(true);
    }

    const date = forecastDate(route);

    // A saved default can outlive its station if the configured list changes.
    useEffect(() => {
        const saved = loadDefaultStation();
        if (saved === homeStation || loadSavedStations().some(other => other.id === saved)) {
            return;
        }

        const controller = new AbortController();
        fetchStations(controller.signal)
            .then(stations => {
                // A newer choice made while this was loading stays.
                if (!stations.some(other => other.id === saved) && loadDefaultStation() === saved) {
                    saveDefaultStation(homeStation);
                    setDefaultStation(homeStation);
                    setStation(current => current === saved ? homeStation : current);
                }
            })
            .catch(() => {
                // The list can't be checked, so the saved default stays.
            });

        return () => controller.abort();
    }, []);

    useEffect(() => watchSavedStations(setSavedStations), []);

    useEffect(() => {
        document.title = titles[route] ?? 'Daily Forecast';
    }, [route]);

    function handleDefaultChange(id: string) {
        setDefaultStation(id);
        saveDefaultStation(id);
    }

    function handleStationSaved(saved: StationSummary) {
        setSavedStations(addSavedStation(savedStations, saved));
    }

    function handleStationRemoved(id: string) {
        setSavedStations(removeSavedStation(savedStations, id));

        const nextDefault = defaultStation === id ? homeStation : defaultStation;
        if (nextDefault !== defaultStation) {
            handleDefaultChange(nextDefault);
        }

        if (station === id) {
            setStation(nextDefault);
        }
    }

    return (
        <>
            <NavBar route={route} onNavigate={navigate} />
            {route === '/' && <StationPage selected={station} onStationChange={setStation} defaultStation={defaultStation} onDefaultChange={handleDefaultChange} savedStations={savedStations} onStationSaved={handleStationSaved} onStationRemoved={handleStationRemoved} unit={unit} onUnitChange={setUnit} onNavigate={navigate} />}
            {route === '/radar' &&
                <ErrorBoundary message="The radar page couldn't be loaded.">
                    <Suspense fallback={<main><p>Loading radar…</p></main>}><RadarPage /></Suspense>
                </ErrorBoundary>}
            {route === '/alerts' && <AlertsPage station={station} onNavigate={navigate} />}
            {route === '/daily' && <DailyForecastPage station={station} unit={unit} onUnitChange={setUnit} onNavigate={navigate} />}
            {date && <ForecastDayPage date={date} station={station} unit={unit} onUnitChange={setUnit} onNavigate={navigate} />}
            {forecastOpened && <ForecastPage hidden={route !== '/forecast'} unit={unit} onUnitChange={setUnit} />}
        </>
    );
}

export default App;
