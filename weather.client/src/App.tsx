import { useEffect, useState } from 'react';
import { fetchForecast, type ForecastTable as ForecastData } from './api/forecast';
import ForecastTable from './components/ForecastTable';
import Legend from './components/Legend';
import UnitToggle from './components/UnitToggle';
import ZipForm from './components/ZipForm';
import { displayTemperature, type Unit } from './temperature';
import './App.css';

const defaultZip = '04086';

function App() {
    const [zip, setZip] = useState(defaultZip);
    const [forecast, setForecast] = useState<ForecastData>();
    const [error, setError] = useState<string>();
    const [loading, setLoading] = useState(true);
    const [unit, setUnit] = useState<Unit>('F');

    useEffect(() => {
        const controller = new AbortController();

        fetchForecast(zip, controller.signal)
            .then(data => {
                setForecast(data);
                setError(undefined);
                setLoading(false);
            })
            .catch((reason: Error) => {
                if (!controller.signal.aborted) {
                    setError(reason.message);
                    setLoading(false);
                }
            });

        return () => controller.abort();
    }, [zip]);

    function handleZipSubmit(nextZip: string) {
        if (nextZip !== zip) {
            setLoading(true);
            setZip(nextZip);
        }
    }

    const average = forecast?.averageHigh;

    return (
        <main>
            <header>
                <h1>Forecast Comparison</h1>
                {forecast && <p className="location">{forecast.location.place}, {forecast.location.state} {forecast.location.zip}</p>}
            </header>

            <div className="controls">
                <ZipForm initialZip={defaultZip} loading={loading} onSubmit={handleZipSubmit} />
                <UnitToggle unit={unit} onChange={setUnit} />
            </div>

            {error && <p className="error" role="alert">{error}</p>}
            {!forecast && loading && <p>Loading forecast…</p>}

            {forecast &&
                <>
                    <p className="average">
                        {average
                            ? <>Average High <strong>{displayTemperature(average.startF, unit)} → {displayTemperature(average.endF, unit)}</strong> <span className="station">{average.station}</span></>
                            : forecast.averageMessage}
                    </p>
                    <ForecastTable columns={forecast.columns} rows={forecast.rows} unit={unit} />
                    <Legend aboveAverageF={forecast.aboveAverageF} belowAverageF={forecast.belowAverageF} />
                </>}
        </main>
    );
}

export default App;
