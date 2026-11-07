import { useEffect, useState, type FormEvent } from 'react';
import { fetchSnow, fetchSnowAlerts, fetchSnowTowns, type SnowAlertReport, type SnowTable as SnowData, type SnowTownSummary } from '../api/snow';
import { isApp } from '../appBridge';
import AlertPanel from '../components/AlertPanel';
import ColumnFilters from '../components/ColumnFilters';
import SnowTable from '../components/SnowTable';
import { addDays, isoDate } from '../snowAverages';
import {
    addManualRow, clearManualValues, hasManualValues, loadManualSnow, removeManualRow, renameManualRow, saveManualSnow, setManualValue,
    type ManualSnow, type ManualValue,
} from '../snowManual';
import { addSnowTown, loadSnowTowns, maxAddedTowns, removeSnowTown, resetSnowTowns, townZips } from '../snowTowns';
import { formatDate } from '../localTime';

const maxDays = 7;
const maxDaysAhead = 15;

const mapLinks = [
    { label: 'NWS Gray winter page', href: 'https://www.weather.gov/gyx/winter' },
    { label: 'NewsCenter Maine', href: 'https://www.newscentermaine.com/weather' },
    { label: 'WMTW', href: 'https://www.wmtw.com/weather' },
    { label: 'WGME', href: 'https://wgme.com/weather' },
];

interface SnowRequest {
    zips: string[];
    start: string;
    end: string;
    // Pressing Go again refreshes the table.
    attempt: number;
}

function rangeError(start: string, end: string, today: string) {
    if (!start || !end) {
        return 'Choose a start and end date.';
    }

    if (end < start) {
        return "The end date can't be before the start date.";
    }

    if (start < addDays(today, -1) || end > addDays(today, maxDaysAhead)) {
        return `Choose dates from yesterday to ${maxDaysAhead} days ahead.`;
    }

    return addDays(start, maxDays - 1) < end ? `Choose a range of ${maxDays} days or fewer.` : undefined;
}

function shortDate(iso: string) {
    return formatDate(iso, { weekday: 'short', month: 'short', day: 'numeric' });
}

function SnowPage({ hidden }: { hidden: boolean }) {
    const today = isoDate(new Date());
    const [configured, setConfigured] = useState<SnowTownSummary[]>();
    const [townsError, setTownsError] = useState<string>();
    const [choices, setChoices] = useState(loadSnowTowns);
    const [newZip, setNewZip] = useState('');
    const [start, setStart] = useState(today);
    const [end, setEnd] = useState(() => addDays(today, 1));
    const [request, setRequest] = useState<SnowRequest>();
    const [table, setTable] = useState<SnowData>();
    const [alerts, setAlerts] = useState<SnowAlertReport>();
    const [alertsError, setAlertsError] = useState<string>();
    const [error, setError] = useState<string>();
    const [loading, setLoading] = useState(false);
    const [hiddenRows, setHiddenRows] = useState<string[]>([]);
    const [manual, setManual] = useState(loadManualSnow);
    const [manualGeneration, setManualGeneration] = useState(0);
    const [newRow, setNewRow] = useState('');

    // The configured towns use no forecast quota.
    useEffect(() => {
        const controller = new AbortController();
        fetchSnowTowns(controller.signal)
            .then(setConfigured)
            .catch((reason: Error) => {
                if (!controller.signal.aborted) {
                    setConfigured([]);
                    setTownsError(reason.message);
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
        fetchSnow(request.zips, request.start, request.end, controller.signal)
            .then(data => {
                setTable(data);
                setError(undefined);
                setLoading(false);
            })
            .catch((reason: Error) => {
                if (!controller.signal.aborted) {
                    setError(reason.message);
                    setLoading(false);
                }
            });

        fetchSnowAlerts(request.zips, controller.signal)
            .then(report => {
                setAlerts(report);
                setAlertsError(undefined);
            })
            .catch((reason: Error) => {
                // A failed check must never look like "no alerts".
                if (!controller.signal.aborted) {
                    setAlerts(undefined);
                    setAlertsError(reason.message);
                }
            });

        return () => controller.abort();
    }, [request]);

    const configuredZips = configured?.map(town => town.zip) ?? [];
    const zips = townZips(configuredZips, choices);
    const names = new Map<string, string>([
        ...(table?.towns.map(town => [town.zip, town.name] as [string, string]) ?? []),
        ...(configured?.map(town => [town.zip, town.name] as [string, string]) ?? []),
    ]);
    const dateError = rangeError(start, end, today);

    function updateManual(next: ManualSnow) {
        setManual(saveManualSnow(next));
    }

    function handleSubmit(event: FormEvent) {
        event.preventDefault();
        if (dateError || zips.length === 0) {
            return;
        }

        setLoading(true);
        setRequest(current => ({ zips, start, end, attempt: (current?.attempt ?? 0) + 1 }));
    }

    function handleAddTown(event: FormEvent) {
        event.preventDefault();
        const zip = newZip.trim();
        if (/^\d{5}$/.test(zip)) {
            setChoices(addSnowTown(configuredZips, choices, zip));
            setNewZip('');
        }
    }

    function handleManualChange(rowId: string, zip: string, part: keyof ManualValue, value: number | null) {
        updateManual(setManualValue(manual, rowId, zip, part, value, start, end));
    }

    function handleClearManual() {
        if (window.confirm('Clear every value typed into the manual rows? The rows themselves stay.')) {
            updateManual(clearManualValues(manual));
            setManualGeneration(generation => generation + 1);
        }
    }

    function handleAddRow(event: FormEvent) {
        event.preventDefault();
        if (newRow.trim()) {
            updateManual(addManualRow(manual, newRow.trim()));
            setNewRow('');
        }
    }

    function handleToggleRow(id: string) {
        setHiddenRows(current => current.includes(id) ? current.filter(other => other !== id) : [...current, id]);
    }

    // Skipping a key is a valid choice in the app, so its row isn't shown as a problem.
    const available = table?.sources.filter(source => !isApp || source.status !== 'NeedsKey') ?? [];
    const filterRows = [...available, ...manual.rows.map(row => ({ id: row.id, label: row.label || 'Manual row' }))];
    const shownTowns = table?.towns.filter(town => zips.includes(town.zip)) ?? [];
    const addedFull = choices.added.length >= maxAddedTowns;

    return (
        <main hidden={hidden}>
            <header>
                <h1>Snow Forecast</h1>
                {table && <p className="location">{shortDate(table.start)}{table.end !== table.start && ` through ${shortDate(table.end)}`}</p>}
            </header>

            <form className="snow-form" onSubmit={handleSubmit}>
                <label>
                    Start
                    <input type="date" value={start} min={addDays(today, -1)} max={addDays(today, maxDaysAhead)} onChange={event => setStart(event.target.value)} />
                </label>
                <label>
                    End
                    <input type="date" value={end} min={start || today} max={addDays(today, maxDaysAhead)} onChange={event => setEnd(event.target.value)} />
                </label>
                <button type="submit" disabled={loading || !!dateError || zips.length === 0}>{loading ? 'Loading…' : 'Go'}</button>
            </form>
            {dateError && <p className="table-note">{dateError}</p>}

            <section className="snow-towns" aria-label="Towns">
                <ul>
                    {zips.map(zip =>
                        <li key={zip}>
                            {names.get(zip) ?? zip}
                            <button type="button" className="icon-button" aria-label={`Remove ${names.get(zip) ?? zip}`} onClick={() => setChoices(removeSnowTown(configuredZips, choices, zip))}>×</button>
                        </li>
                    )}
                </ul>
                <form className="zip-form" onSubmit={handleAddTown}>
                    <label htmlFor="snow-zip">Add ZIP</label>
                    <input id="snow-zip" inputMode="numeric" pattern="\d{5}" maxLength={5} value={newZip} onChange={event => setNewZip(event.target.value)} disabled={addedFull} />
                    <button type="submit" disabled={addedFull || !/^\d{5}$/.test(newZip.trim())}>Add</button>
                    {(choices.added.length > 0 || choices.removed.length > 0) &&
                        <button type="button" onClick={() => setChoices(resetSnowTowns())}>Reset towns</button>}
                </form>
                {addedFull && <p className="table-note">You can add up to {maxAddedTowns} towns. Remove one to add another.</p>}
            </section>

            {townsError && <p className="error" role="alert">{townsError}</p>}
            {error && <p className="error" role="alert">{error}</p>}
            {!table && !loading && !error && <p>Choose the storm's dates and press Go to load the snow forecast.</p>}
            {!table && loading && <p>Loading snow forecast…</p>}

            {table &&
                <>
                    <div className="table-filters">
                        <ColumnFilters columns={filterRows} hidden={hiddenRows} onToggle={handleToggleRow} />
                    </div>
                    <p className="table-note">
                        Totals cover midnight to midnight on each date. Where a forecast gives a range, it fills Low and High; otherwise
                        both show the same amount. ≥ marks a forecast that doesn't cover all of these dates, which the averages leave out.
                    </p>
                    <SnowTable
                        towns={shownTowns}
                        sources={available.filter(source => !hiddenRows.includes(source.id))}
                        cells={table.cells}
                        manualRows={manual.rows.filter(row => !hiddenRows.includes(row.id))}
                        manual={manual}
                        manualGeneration={manualGeneration}
                        onManualChange={handleManualChange}
                        onManualRename={(id, label) => updateManual(renameManualRow(manual, id, label))}
                        onManualRemove={id => updateManual(removeManualRow(manual, id))}
                    />
                    <div className="snow-manual">
                        <form className="zip-form" onSubmit={handleAddRow}>
                            <label htmlFor="snow-row">Add a manual row</label>
                            <input id="snow-row" className="snow-row-name" maxLength={40} value={newRow} onChange={event => setNewRow(event.target.value)} />
                            <button type="submit" disabled={!newRow.trim()}>Add</button>
                        </form>
                        <button type="button" onClick={handleClearManual} disabled={!hasManualValues(manual)}>Clear manual values</button>
                    </div>
                    {manual.start && hasManualValues(manual) &&
                        <p className="table-note">
                            Manual values were entered for {shortDate(manual.start)}{manual.end && manual.end !== manual.start && ` through ${shortDate(manual.end)}`}.
                        </p>}
                </>}

            {(alerts || alertsError) &&
                <section className="snow-alerts">
                    <h2>Winter alerts</h2>
                    {alertsError && <p className="error" role="alert">{alertsError}</p>}
                    {alerts?.message && <p className="error" role="alert">{alerts.message}</p>}
                    {alerts && alerts.status === 'Ok' && alerts.alerts.length === 0 && <p>No winter watches, warnings or advisories for these towns.</p>}
                    {alerts?.alerts.map(({ alert, zips: covered }) =>
                        <div key={alert.id} className="snow-alert">
                            <AlertPanel alert={alert} />
                            <p className="table-note">{covered.map(zip => names.get(zip) ?? zip).join(', ')}</p>
                        </div>
                    )}
                </section>}

            <section className="snow-maps">
                <h2>Snow maps</h2>
                <p>Each outlet's own snowfall map:</p>
                <ul>
                    {mapLinks.map(link => <li key={link.href}><a href={link.href} target="_blank" rel="noopener noreferrer">{link.label}</a></li>)}
                </ul>
            </section>

            <footer className="attribution">
                Weather data by <a href="https://open-meteo.com/">Open-Meteo.com</a> (<a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>),
                the National Weather Service, The Weather Company and <a href="https://www.weatherapi.com/" title="Free Weather API">WeatherAPI.com</a>
            </footer>
        </main>
    );
}

export default SnowPage;
