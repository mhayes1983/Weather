import { useEffect, useState } from 'react';
import { fetchForecast, type ForecastTable as ForecastData } from '../api/forecast';
import { currentSetup, isApp } from '../appBridge';
import ColumnFilters from '../components/ColumnFilters';
import ForecastTable from '../components/ForecastTable';
import Legend from '../components/Legend';
import ZipForm from '../components/ZipForm';
import { moveColumn, orderColumns } from '../columns';
import { displayTemperature, type Unit } from '../temperature';

const defaultZip = '04086';

interface ForecastPageProps {
    hidden: boolean;
    unit: Unit;
}

function ForecastPage({ hidden, unit }: ForecastPageProps) {
    const [zip, setZip] = useState(currentSetup()?.zip ?? defaultZip);
    const [forecast, setForecast] = useState<ForecastData>();
    const [error, setError] = useState<string>();
    const [loading, setLoading] = useState(true);
    const [hiddenColumns, setHiddenColumns] = useState<string[]>([]);
    const [columnOrder, setColumnOrder] = useState<string[]>([]);

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

    function handleToggleColumn(id: string) {
        setHiddenColumns(hidden => hidden.includes(id)
            ? hidden.filter(other => other !== id)
            : [...hidden, id]);
    }

    function handleMoveColumn(id: string, before: string | null) {
        setColumnOrder(moveColumn(columns.map(column => column.id), id, before));
    }

    const average = forecast?.averageHigh;

    // Skipping a key is a valid choice in the app, so its column isn't shown as a problem.
    const available = forecast?.columns.filter(column => !isApp || column.status !== 'NeedsKey') ?? [];
    const columns = orderColumns(available, columnOrder);
    const visibleColumns = columns.filter(column => !hiddenColumns.includes(column.id));

    return (
        <main hidden={hidden}>
            <header>
                <h1>Forecast Comparison</h1>
                {forecast && <p className="location">{forecast.location.place}, {forecast.location.state} {forecast.location.zip}</p>}
            </header>

            <div className="controls">
                <ZipForm initialZip={currentSetup()?.zip ?? defaultZip} loading={loading} onSubmit={handleZipSubmit} />
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
                    <ColumnFilters columns={available} hidden={hiddenColumns} onToggle={handleToggleColumn} />
                    <ForecastTable columns={visibleColumns} rows={forecast.rows} unit={unit} onMoveColumn={handleMoveColumn} />
                    <Legend aboveAverageF={forecast.aboveAverageF} belowAverageF={forecast.belowAverageF} />
                </>}

            <footer className="attribution">
                Weather data by <a href="https://open-meteo.com/">Open-Meteo.com</a> (<a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>)
            </footer>
        </main>
    );
}

export default ForecastPage;
