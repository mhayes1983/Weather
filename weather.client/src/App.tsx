import { lazy, Suspense, useEffect, useState } from 'react';
import ErrorBoundary from './components/ErrorBoundary';
import NavBar from './components/NavBar';
import AlertsPage from './pages/AlertsPage';
import DailyForecastPage from './pages/DailyForecastPage';
import ForecastDayPage from './pages/ForecastDayPage';
import ForecastPage from './pages/ForecastPage';
import StationPage from './pages/StationPage';
import type { Unit } from './temperature';
import { forecastDate, useRoute, type Route } from './useRoute';
import './App.css';

// Leaflet is large, so it only loads when the radar page opens.
const RadarPage = lazy(() => import('./pages/RadarPage'));

const homeStation = 'home';

const titles: Partial<Record<Route, string>> = {
    '/': 'Weather Station',
    '/alerts': 'Weather Alerts',
    '/forecast': 'Forecast Comparison',
    '/radar': 'Weather Radar',
};

function App() {
    const [route, navigate] = useRoute();
    const [unit, setUnit] = useState<Unit>('F');
    const [station, setStation] = useState(homeStation);
    const [forecastOpened, setForecastOpened] = useState(route === '/forecast');

    // The forecast loads on first visit, then stays mounted to keep its ZIP and columns.
    if (route === '/forecast' && !forecastOpened) {
        setForecastOpened(true);
    }

    const date = forecastDate(route);

    useEffect(() => {
        document.title = titles[route] ?? 'Daily Forecast';
    }, [route]);

    return (
        <>
            <NavBar route={route} onNavigate={navigate} />
            {route === '/' && <StationPage selected={station} onStationChange={setStation} unit={unit} onUnitChange={setUnit} onNavigate={navigate} />}
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
