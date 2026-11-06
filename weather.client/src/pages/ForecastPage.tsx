import { useEffect, useState } from 'react';
import { fetchForecast, type AverageRange, type ForecastTable as ForecastData } from '../api/forecast';
import { fetchStations, type StationSummary } from '../api/station';
import { currentSetup, isApp } from '../appBridge';
import ColumnFilters from '../components/ColumnFilters';
import ForecastTable from '../components/ForecastTable';
import Legend from '../components/Legend';
import MeasureFilters from '../components/MeasureFilters';
import ZipForm from '../components/ZipForm';
import { moveColumn, orderColumns } from '../columns';
import { measureLabels, measures, toggleMeasure, type Measure } from '../measures';
import { stationZip } from '../stationZip';
import { displayTemperature, type Unit } from '../temperature';

const defaultZip = '04086';

interface ForecastPageProps {
    hidden: boolean;
    unit: Unit;
    station: string;
    savedStations: StationSummary[];
}

interface ForecastRequest {
    zip: string;
    // Pressing Go again on the same ZIP refreshes the table.
    attempt: number;
}

function ForecastPage({ hidden, unit, station, savedStations }: ForecastPageProps) {
    const [request, setRequest] = useState<ForecastRequest>();
    const [forecast, setForecast] = useState<ForecastData>();
    const [error, setError] = useState<string>();
    const [loading, setLoading] = useState(false);
    const [stationList, setStationList] = useState<StationSummary[]>();
    const [shownMeasures, setShownMeasures] = useState<Measure[]>(measures);
    const [hiddenColumns, setHiddenColumns] = useState<string[]>([]);
    const [columnOrder, setColumnOrder] = useState<string[]>([]);

    // The station list holds configured ZIPs and uses no forecast quota.
    useEffect(() => {
        const controller = new AbortController();

        fetchStations(controller.signal)
            .then(setStationList)
            .catch(() => {
                // Without the list, the ZIP box falls back to the home ZIP.
                if (!controller.signal.aborted) {
                    setStationList([]);
                }
            });

        return () => controller.abort();
    }, []);

    // The table only loads when asked, so opening the page uses no forecast quota.
    useEffect(() => {
        if (!request) {
            return;
        }

        const controller = new AbortController();

        fetchForecast(request.zip, controller.signal)
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
    }, [request]);

    function handleZipSubmit(nextZip: string) {
        setLoading(true);
        setRequest(current => ({ zip: nextZip, attempt: (current?.attempt ?? 0) + 1 }));
    }

    function handleToggleMeasure(measure: Measure) {
        setShownMeasures(shown => toggleMeasure(shown, measure));
    }

    function handleToggleColumn(id: string) {
        setHiddenColumns(hidden => hidden.includes(id)
            ? hidden.filter(other => other !== id)
            : [...hidden, id]);
    }

    function handleMoveColumn(id: string, before: string | null) {
        setColumnOrder(moveColumn(columns.map(column => column.id), id, before));
    }

    function averageText(measure: Measure, average: AverageRange) {
        return (
            <span key={measure}>
                Average {measureLabels[measure]} <strong>{displayTemperature(average.startF, unit)} → {displayTemperature(average.endF, unit)}</strong>
            </span>
        );
    }

    // Waiting for the list means a configured ZIP can't arrive late and replace what someone has typed.
    const zip = stationList && stationZip(station, savedStations, stationList, currentSetup()?.zip ?? defaultZip);
    const shownAverages = forecast
        ? shownMeasures.flatMap(measure => {
            const average = measure === 'high' ? forecast.averageHigh : forecast.averageLow;
            return average ? [{ measure, average }] : [];
        })
        : [];

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
                {/* A new station starts the box on its ZIP. */}
                {zip !== undefined && <ZipForm key={zip} initialZip={zip} loading={loading} onSubmit={handleZipSubmit} />}
            </div>

            {error && <p className="error" role="alert">{error}</p>}
            {!forecast && !loading && !error && <p>Press Go to load the forecast comparison.</p>}
            {!forecast && loading && <p>Loading forecast…</p>}

            {forecast &&
                <>
                    <p className="average">
                        {shownAverages.map(({ measure, average }) => averageText(measure, average))}
                        {shownAverages.length > 0 && <span className="station">{shownAverages[0].average.station}</span>}
                        {forecast.averageMessage && <span>{forecast.averageMessage}</span>}
                    </p>
                    <div className="table-filters">
                        <MeasureFilters shown={shownMeasures} onToggle={handleToggleMeasure} />
                        <ColumnFilters columns={available} hidden={hiddenColumns} onToggle={handleToggleColumn} />
                    </div>
                    <p className="table-note">Each day's low is that morning's, and its high is that afternoon's.</p>
                    <ForecastTable columns={visibleColumns} rows={forecast.rows} measures={shownMeasures} unit={unit} onMoveColumn={handleMoveColumn} />
                    <Legend aboveAverageF={forecast.aboveAverageF} belowAverageF={forecast.belowAverageF} />
                </>}

            <footer className="attribution">
                Weather data by <a href="https://open-meteo.com/">Open-Meteo.com</a> (<a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>)
            </footer>
        </main>
    );
}

export default ForecastPage;
