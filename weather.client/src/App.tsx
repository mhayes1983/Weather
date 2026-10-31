import { useEffect, useState } from 'react';
import NavBar from './components/NavBar';
import ForecastPage from './pages/ForecastPage';
import StationPage from './pages/StationPage';
import type { Unit } from './temperature';
import { useRoute } from './useRoute';
import './App.css';

function App() {
    const [route, navigate] = useRoute();
    const [unit, setUnit] = useState<Unit>('F');
    const [forecastOpened, setForecastOpened] = useState(route === '/forecast');

    // The forecast loads on first visit, then stays mounted to keep its ZIP and columns.
    if (route === '/forecast' && !forecastOpened) {
        setForecastOpened(true);
    }

    useEffect(() => {
        document.title = route === '/forecast' ? 'Forecast Comparison' : 'Weather Station';
    }, [route]);

    return (
        <>
            <NavBar route={route} onNavigate={navigate} />
            {route === '/' && <StationPage unit={unit} onUnitChange={setUnit} />}
            {forecastOpened && <ForecastPage hidden={route !== '/forecast'} unit={unit} onUnitChange={setUnit} />}
        </>
    );
}

export default App;
