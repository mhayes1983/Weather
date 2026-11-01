import { lazy, Suspense, useEffect, useState } from 'react';
import { fetchStations, type StationSummary } from './api/station';
import { currentSetup } from './appBridge';
import ErrorBoundary from './components/ErrorBoundary';
import NavBar from './components/NavBar';
import SettingsPanel from './components/SettingsPanel';
import SiteFooter from './components/SiteFooter';
import { homeStation, loadDefaultStation, saveDefaultStation } from './defaultStation';
import AlertsPage from './pages/AlertsPage';
import DailyForecastPage from './pages/DailyForecastPage';
import ForecastDayPage from './pages/ForecastDayPage';
import ForecastPage from './pages/ForecastPage';
import HelpPage from './pages/HelpPage';
import PrivacyPage from './pages/PrivacyPage';
import StationPage from './pages/StationPage';
import TermsPage from './pages/TermsPage';
import { addSavedStation, loadSavedStations, removeSavedStation, watchSavedStations } from './savedStations';
import { loadUnit, saveUnit, type Unit } from './temperature';
import { applyTheme, loadTheme, saveTheme, type Theme } from './theme';
import { forecastDate, useRoute, type Route } from './useRoute';
import './App.css';

// Leaflet is large, so it only loads when the radar page opens.
const RadarPage = lazy(() => import('./pages/RadarPage'));

// Setup is only in the Android app, so the site's build leaves this out.
const SetupPage = import.meta.env.MODE === 'app' ? lazy(() => import('./pages/SetupPage')) : null;

const titles: Partial<Record<Route, string>> = {
    '/': 'Weather Station',
    '/alerts': 'Weather Alerts',
    '/forecast': 'Forecast Comparison',
    '/radar': 'Weather Radar',
    '/help': 'Help & About',
    '/terms': 'Terms of Use',
    '/privacy': 'Privacy Policy',
    '/setup': 'Setup',
};

function App() {
    const [route, navigate] = useRoute();
    const [unit, setUnit] = useState(loadUnit);
    const [theme, setTheme] = useState(loadTheme);
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

    function handleUnitChange(next: Unit) {
        setUnit(next);
        saveUnit(next);
    }

    function handleThemeChange(next: Theme) {
        setTheme(next);
        applyTheme(next);
        saveTheme(next);
    }

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

    const stationControls = {
        selected: station,
        onStationChange: setStation,
        defaultStation,
        onDefaultChange: handleDefaultChange,
        savedStations,
        onStationSaved: handleStationSaved,
        onStationRemoved: handleStationRemoved,
    };

    if (SetupPage && (route === '/setup' || !currentSetup()?.complete)) {
        return <main><Suspense fallback={<p>Loading setup…</p>}><SetupPage /></Suspense></main>;
    }

    return (
        <>
            <NavBar
                route={route}
                onNavigate={navigate}
                settings={<SettingsPanel theme={theme} onThemeChange={handleThemeChange} unit={unit} onUnitChange={handleUnitChange} onNavigate={navigate} />}
            />
            {route === '/' && <StationPage selected={station} stationControls={stationControls} unit={unit} onNavigate={navigate} />}
            {route === '/radar' &&
                <ErrorBoundary message="The radar page couldn't be loaded.">
                    <Suspense fallback={<main><p>Loading radar…</p></main>}><RadarPage /></Suspense>
                </ErrorBoundary>}
            {route === '/alerts' && <AlertsPage station={station} onNavigate={navigate} />}
            {route === '/daily' && <DailyForecastPage station={station} stationControls={stationControls} unit={unit} onNavigate={navigate} />}
            {date && <ForecastDayPage date={date} station={station} unit={unit} onNavigate={navigate} />}
            {forecastOpened && <ForecastPage hidden={route !== '/forecast'} unit={unit} />}
            {route === '/help' && <HelpPage onNavigate={navigate} />}
            {route === '/terms' && <TermsPage onNavigate={navigate} />}
            {route === '/privacy' && <PrivacyPage onNavigate={navigate} />}
            <SiteFooter onNavigate={navigate} />
        </>
    );
}

export default App;
