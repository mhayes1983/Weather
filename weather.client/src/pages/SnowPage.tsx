import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import {
    fetchSnow, fetchSnowAlerts, fetchSnowHourly, fetchSnowReports, fetchSnowTowns,
    type SnowAlertReport, type SnowReportTable, type SnowTable as SnowData, type SnowTownSummary, type StormAlertKind,
} from '../api/snow';
import { isApp } from '../appBridge';
import AlertPanel from '../components/AlertPanel';
import ColumnFilters from '../components/ColumnFilters';
import DepthTable from '../components/DepthTable';
import SnowReports from '../components/SnowReports';
import SnowTable from '../components/SnowTable';
import SnowHourly from '../components/SnowHourly';
import SnowTiming, { type HourlyState } from '../components/SnowTiming';
import WindTable from '../components/WindTable';
import { outageReasons, snowRatio } from '../outageRisk';
import { addDays, averageSnow, isoDate } from '../snowAverages';
import { hourlyLinkSearch, readHourlyLink, readSnowLink, snowLinkPath, type HourlyLink } from '../snowLink';
import {
    addManualRow, clearManualValues, hasManualValues, loadManualSnow, removeManualRow, renameManualRow, saveManualSnow, setManualValue,
    type ManualSnow, type ManualValue, type ManualView,
} from '../snowManual';
import { addSnowTown, loadSnowTowns, maxAddedTowns, removeSnowTown, resetSnowTowns, townZips } from '../snowTowns';
import { reportTowns, stormViews, viewInfo, type StormView } from '../stormViews';
import { formatDate } from '../localTime';
import type { Route, RouteState } from '../useRoute';

const maxDays = 7;
const maxDaysAhead = 15;

const mapLinks: Record<StormView, { label: string; href: string }[]> = {
    snow: [
        { label: 'NWS Gray winter page', href: 'https://www.weather.gov/gyx/winter' },
        { label: 'NewsCenter Maine', href: 'https://www.newscentermaine.com/weather' },
        { label: 'WMTW', href: 'https://www.wmtw.com/weather' },
        { label: 'WGME', href: 'https://wgme.com/weather' },
    ],
    rain: [
        { label: 'WPC rainfall forecast maps', href: 'https://www.wpc.ncep.noaa.gov/qpf/qpf2.shtml' },
        { label: 'NWS Gray', href: 'https://www.weather.gov/gyx/' },
        { label: 'NewsCenter Maine', href: 'https://www.newscentermaine.com/weather' },
        { label: 'WMTW', href: 'https://www.wmtw.com/weather' },
        { label: 'WGME', href: 'https://wgme.com/weather' },
    ],
    wind: [
        { label: 'NWS Gray', href: 'https://www.weather.gov/gyx/' },
        { label: 'NewsCenter Maine', href: 'https://www.newscentermaine.com/weather' },
        { label: 'WMTW', href: 'https://www.wmtw.com/weather' },
        { label: 'WGME', href: 'https://wgme.com/weather' },
    ],
    depth: [
        { label: 'NOHRSC snow depth analysis', href: 'https://www.nohrsc.noaa.gov/interactive/html/map.html' },
        { label: 'NWS Gray winter page', href: 'https://www.weather.gov/gyx/winter' },
    ],
};

const alertKinds: Record<StormView, StormAlertKind> = { snow: 'Winter', rain: 'Flood', wind: 'Wind', depth: 'Winter' };
const alertWords: Record<StormAlertKind, { heading: string; none: string }> = {
    Winter: { heading: 'Winter alerts', none: 'No winter watches, warnings or advisories for these towns.' },
    Flood: { heading: 'Flood alerts', none: 'No flood watches, warnings or advisories for these towns.' },
    Wind: { heading: 'Wind alerts', none: 'No wind or tropical storm watches, warnings or advisories for these towns.' },
};

function loadAllManual(): Record<ManualView, ManualSnow> {
    return { snow: loadManualSnow('snow'), rain: loadManualSnow('rain'), wind: loadManualSnow('wind') };
}

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

function hourlyKey(zip: string, start: string, end: string) {
    return `${zip}|${start}|${end}`;
}

interface SnowPageProps {
    route: Route;
    onNavigate: (route: Route, search?: string) => void;
}

/**
 * The Storm Forecast page: each source's snow, rain, strongest gust and snow depth for a storm's dates and towns.
 * Go loads every view at once, so switching views never calls the server.
 */
function SnowPage({ route, onNavigate }: SnowPageProps) {
    const hidden = route !== '/storm';
    const today = isoDate(new Date());
    // A shared link fills in its storm's dates, towns and view. Go still has to be pressed, so opening it uses no quota.
    const [link] = useState(() => readSnowLink(window.location.search));
    const [linkZips, setLinkZips] = useState(link.zips);
    const [view, setView] = useState<StormView>(link.view);
    const [copied, setCopied] = useState(false);
    const [configured, setConfigured] = useState<SnowTownSummary[]>();
    const [townsError, setTownsError] = useState<string>();
    const [choices, setChoices] = useState(loadSnowTowns);
    const [newZip, setNewZip] = useState('');
    const [start, setStart] = useState(link.start ?? today);
    const [end, setEnd] = useState(() => link.end ?? addDays(link.start ?? today, 1));
    const [request, setRequest] = useState<SnowRequest>();
    const [table, setTable] = useState<SnowData>();
    const [alerts, setAlerts] = useState<SnowAlertReport>();
    const [alertsError, setAlertsError] = useState<string>();
    const [reports, setReports] = useState<SnowReportTable>();
    const [reportsError, setReportsError] = useState<string>();
    const [error, setError] = useState<string>();
    const [loading, setLoading] = useState(false);
    const [hiddenRows, setHiddenRows] = useState<string[]>([]);
    const [manualByView, setManualByView] = useState(loadAllManual);
    const [manualGeneration, setManualGeneration] = useState(0);
    const [newRow, setNewRow] = useState('');
    // Each town's hourly data, by town and dates. It comes from the same cached results as the table.
    const [hourly, setHourly] = useState<Record<string, HourlyState>>({});
    const [hourlyLoading, setHourlyLoading] = useState<string[]>([]);
    // Requests in flight or done, by key. Go aborts them all, so a late answer can't overwrite newer data.
    const requested = useRef(new Map<string, AbortController>());
    const [hourlyLink, setHourlyLink] = useState<HourlyLink>();
    const [hourlyRoute, setHourlyRoute] = useState<Route>();

    // The hourly page's source and town come from its address, which changes on each visit.
    if (route !== hourlyRoute) {
        setHourlyRoute(route);
        setHourlyLink(route === '/storm/hourly' ? readHourlyLink(window.location.search) : undefined);
    }

    const loadHourly = useCallback((zip: string, from: string, to: string) => {
        const key = hourlyKey(zip, from, to);
        if (requested.current.has(key)) {
            return;
        }

        const controller = new AbortController();
        requested.current.set(key, controller);
        setHourlyLoading(current => [...current, key]);
        fetchSnowHourly(zip, from, to, controller.signal)
            .then(data => {
                if (!controller.signal.aborted) {
                    setHourly(current => ({ ...current, [key]: { data } }));
                }
            })
            .catch((reason: Error) => {
                if (!controller.signal.aborted) {
                    // A failure can be tried again.
                    requested.current.delete(key);
                    setHourly(current => ({ ...current, [key]: { error: reason.message } }));
                }
            })
            .finally(() => {
                if (!controller.signal.aborted) {
                    setHourlyLoading(current => current.filter(other => other !== key));
                }
            });
    }, []);

    // Leaving the page for good cancels anything still loading.
    useEffect(() => {
        const pending = requested.current;
        return () => pending.forEach(controller => controller.abort());
    }, []);

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

        // Reports only add detail, so a failure says so and leaves the table alone.
        fetchSnowReports(request.zips, request.start, request.end, controller.signal)
            .then(data => {
                setReports(data);
                setReportsError(undefined);
            })
            .catch((reason: Error) => {
                if (!controller.signal.aborted) {
                    setReports(undefined);
                    setReportsError(reason.message);
                }
            });

        return () => controller.abort();
    }, [request]);

    const configuredZips = configured?.map(town => town.zip) ?? [];
    const zips = linkZips ?? townZips(configuredZips, choices);
    const names = new Map<string, string>([
        ...(table?.towns.map(town => [town.zip, town.name] as [string, string]) ?? []),
        ...(configured?.map(town => [town.zip, town.name] as [string, string]) ?? []),
    ]);
    const dateError = rangeError(start, end, today);
    const manualView = view === 'depth' ? undefined : view;
    const manual = manualView ? manualByView[manualView] : undefined;

    function updateManual(next: ManualSnow) {
        if (manualView) {
            setManualByView(current => ({ ...current, [manualView]: saveManualSnow(next, manualView) }));
        }
    }

    function handleSubmit(event: FormEvent) {
        event.preventDefault();
        if (dateError || zips.length === 0) {
            return;
        }

        setLoading(true);
        setCopied(false);
        // Go refreshes the table, so the hourly data loads again with it.
        requested.current.forEach(controller => controller.abort());
        requested.current.clear();
        setHourly({});
        setHourlyLoading([]);
        setRequest(current => ({ zips, start, end, attempt: (current?.attempt ?? 0) + 1 }));

        // The address then names this storm, so it can be bookmarked or shared. The app has no address to share.
        if (!isApp) {
            window.history.replaceState(null, '', snowLinkPath(start, end, zips, view));
        }
    }

    // Switching views uses the data already loaded. A loaded storm's address follows the view, so a copied link opens on it.
    function handleView(next: StormView) {
        setView(next);
        setCopied(false);
        if (!isApp && request) {
            window.history.replaceState(window.history.state, '', snowLinkPath(request.start, request.end, request.zips, next));
        }
    }

    function handleCopyLink() {
        if (request) {
            const url = window.location.origin + snowLinkPath(request.start, request.end, request.zips, view);
            navigator.clipboard.writeText(url).then(() => setCopied(true), () => setCopied(false));
        }
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
        if (manual) {
            updateManual(setManualValue(manual, rowId, zip, part, value, start, end));
        }
    }

    function handleClearManual() {
        if (manual && window.confirm(`Clear every value typed into the ${viewInfo[view].label.toLowerCase()} manual rows? The rows themselves stay.`)) {
            updateManual(clearManualValues(manual));
            setManualGeneration(generation => generation + 1);
        }
    }

    function handleAddRow(event: FormEvent) {
        event.preventDefault();
        if (manual && newRow.trim()) {
            updateManual(addManualRow(manual, newRow.trim()));
            setNewRow('');
        }
    }

    function handleToggleRow(id: string) {
        setHiddenRows(current => current.includes(id) ? current.filter(other => other !== id) : [...current, id]);
    }

    // Skipping a key is a valid choice in the app, so its row isn't shown as a problem.
    const available = table?.sources.filter(source => !isApp || source.status !== 'NeedsKey') ?? [];
    // A source without data for a view, such as a model without gusts, is left out of that view.
    const inView = (forView: StormView) => available.filter(source => (source.views ?? ['snow', 'rain']).includes(forView));
    const viewSources = inView(view);
    // Each view has its own manual rows under the same ids (ncm, wmtw, wgme), so hiding one is kept per view.
    // Sources are the same in every view, so hiding a source hides it everywhere.
    const manualHideId = (forView: StormView, rowId: string) => `${forView}:${rowId}`;
    const filterRows = [
        ...viewSources,
        ...(manual?.rows.map(row => ({ id: manualHideId(view, row.id), label: row.label || 'Manual row' })) ?? []),
    ];
    const shownTowns = table?.towns.filter(town => zips.includes(town.zip)) ?? [];
    const addedFull = choices.added.length >= maxAddedTowns;
    const shown = (forView: StormView) => inView(forView).filter(source => !hiddenRows.includes(source.id));
    const shownSources = shown(view);
    const manualRows = manual?.rows.filter(row => !hiddenRows.includes(manualHideId(view, row.id))) ?? [];
    const rainCells = table?.rainCells ?? [];
    const windCells = table?.windCells ?? [];
    const depthCells = table?.depthCells ?? [];

    // Outage risk reads every view's averages for the sources shown, so it's the same whichever view is open.
    const risks = table
        ? shownTowns.flatMap(town => {
            const visible = (forView: StormView) => new Set(shown(forView).map(source => source.id));
            const snowSources = visible('snow');
            const snowCells = table.cells.filter(cell => cell.zip === town.zip && snowSources.has(cell.sourceId));
            const typed = (forView: ManualView) => manualByView[forView].rows
                .filter(row => !hiddenRows.includes(manualHideId(forView, row.id)))
                .map(row => manualByView[forView].values[row.id]?.[town.zip] ?? { low: null, high: null });
            const snow = averageSnow([
                ...snowCells.map(cell => ({ low: cell.lowInches, high: cell.highInches, partial: cell.partial })),
                ...typed('snow'),
            ]).middle;
            const ratio = snowRatio(snowCells);
            const ice = Math.max(0, ...snowCells.map(cell => cell.iceInches ?? 0));
            const windSources = visible('wind');
            const gust = averageSnow([
                ...windCells
                    .filter(cell => cell.zip === town.zip && windSources.has(cell.sourceId))
                    .map(cell => ({ low: cell.maxMph, high: cell.maxMph, partial: cell.partial })),
                ...typed('wind').map(value => ({ low: value.high, high: value.high })),
            ]).middle;
            const reasons = outageReasons({ snowInches: snow, ratio, iceInches: ice, gustMph: gust });
            return reasons.length > 0 ? [{ town, reasons }] : [];
        })
        : [];

    const handleTownShown = useCallback((zip: string) => {
        if (table) {
            loadHourly(zip, table.start, table.end);
        }
    }, [table, loadHourly]);

    function handleOpenSource(sourceId: string, zip: string) {
        if (table) {
            onNavigate('/storm/hourly', hourlyLinkSearch({ zip, source: sourceId, start: table.start, end: table.end, view }));
        }
    }

    function handleHourlySource(sourceId: string) {
        if (hourlyLink) {
            const next = { ...hourlyLink, source: sourceId };
            window.history.replaceState(window.history.state, '', `/storm/hourly${hourlyLinkSearch(next)}`);
            setHourlyLink(next);
        }
    }

    // Back returns to the table as it was when the page came from it.
    function handleHourlyBack() {
        if ((window.history.state as RouteState | null)?.from === '/storm') {
            window.history.back();
        } else {
            onNavigate('/storm');
        }
    }

    // Browsers only offer the clipboard on HTTPS and localhost. Without it, the address bar still has the link.
    const canCopy = !isApp && typeof navigator.clipboard?.writeText === 'function';

    const hourlyLinkKey = hourlyLink && hourlyKey(hourlyLink.zip, hourlyLink.start, hourlyLink.end);
    const hourlyView = route === '/storm/hourly' && (hourlyLink && hourlyLinkKey
        ? <SnowHourly
            link={hourlyLink}
            state={hourly[hourlyLinkKey]}
            loading={hourlyLoading.includes(hourlyLinkKey)}
            onLoad={() => loadHourly(hourlyLink.zip, hourlyLink.start, hourlyLink.end)}
            onSourceChange={handleHourlySource}
            onBack={handleHourlyBack}
        />
        : <main>
            <p className="back-link"><a href="/storm" onClick={event => { event.preventDefault(); onNavigate('/storm'); }}>← Storm Forecast</a></p>
            <p className="error" role="alert">This link doesn't name a source, town and dates.</p>
        </main>);

    const alertKind = alertKinds[view];
    const viewAlerts = alerts?.alerts.filter(alert => (alert.kind ?? 'Winter') === alertKind) ?? [];
    const reportList = reportTowns(reports, view);

    return (
        <>
        {hourlyView}
        <main hidden={hidden}>
            <header>
                <h1>Storm Forecast</h1>
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
                            {!linkZips &&
                                <button type="button" className="icon-button" aria-label={`Remove ${names.get(zip) ?? zip}`} onClick={() => setChoices(removeSnowTown(configuredZips, choices, zip))}>×</button>}
                        </li>
                    )}
                </ul>
                {linkZips &&
                    <p className="table-note snow-link-note">
                        These towns came from a link. <button type="button" onClick={() => setLinkZips(undefined)}>Use my towns</button>
                    </p>}
                <form className="zip-form" hidden={!!linkZips} onSubmit={handleAddTown}>
                    <label htmlFor="snow-zip">Add ZIP</label>
                    <input id="snow-zip" inputMode="numeric" pattern="\d{5}" maxLength={5} value={newZip} onChange={event => setNewZip(event.target.value)} disabled={addedFull} />
                    <button type="submit" disabled={addedFull || !/^\d{5}$/.test(newZip.trim())}>Add</button>
                    {(choices.added.length > 0 || choices.removed.length > 0) &&
                        <button type="button" onClick={() => setChoices(resetSnowTowns())}>Reset towns</button>}
                </form>
                {addedFull && <p className="table-note">You can add up to {maxAddedTowns} towns. Remove one to add another.</p>}
            </section>

            <div className="storm-views" role="group" aria-label="Show">
                {stormViews.map(option =>
                    <button
                        key={option}
                        type="button"
                        className={option === view ? 'active' : undefined}
                        aria-pressed={option === view}
                        onClick={() => handleView(option)}
                    >
                        {viewInfo[option].label}
                    </button>
                )}
            </div>

            {townsError && <p className="error" role="alert">{townsError}</p>}
            {error && <p className="error" role="alert">{error}</p>}
            {!table && !loading && !error && <p>Choose the storm's dates and press Go to load its snow, rain, wind and snow depth forecasts.</p>}
            {!table && loading && <p>Loading storm forecast…</p>}

            {table &&
                <>
                    {risks.length > 0 &&
                        <ul className="outage-risk" aria-label="Outage risk">
                            {risks.map(({ town, reasons }) =>
                                <li key={town.zip} title="A rough guide from the averages of the sources shown, not a utility or NWS forecast">
                                    <strong>{town.name}: outage risk</strong> from {reasons.join(' and ')}
                                </li>
                            )}
                        </ul>}
                    <div className="table-filters">
                        <ColumnFilters columns={filterRows} hidden={hiddenRows} onToggle={handleToggleRow} />
                    </div>
                    <p className="table-note">
                        {view === 'snow' &&
                            <>Totals cover midnight to midnight on each date. Where a forecast gives a range, it fills Low and High; otherwise
                            both show the same amount. ≥ marks a forecast that doesn't cover all of these dates, which the averages leave out.
                            The small ratio is inches of snow for each inch of water, where about 10:1 is typical and lower is wetter and
                            heavier; mix marks a source whose water is well beyond what its snow accounts for.</>}
                        {view === 'rain' &&
                            <>Rain here is all precipitation as water, counting melted snow, because only Open-Meteo forecasts rain on its own.
                            Totals cover midnight to midnight on each date. ≥ marks a forecast that doesn't cover all of these dates, which the
                            averages leave out, and mix marks a source that expects snow as well.</>}
                        {view === 'wind' &&
                            <>Each source's strongest gust on these dates. ≥ marks a forecast that doesn't cover all of them, which the
                            averages leave out. Weather Underground's forecast has no gusts, and some Open-Meteo models don't give them.</>}
                        {view === 'depth' &&
                            <>Snow on the ground as Open-Meteo's models work it out from their own snow and melt. It's modelled, never measured,
                            and only some models give it. Select a model for its hour-by-hour depth in {shownTowns[0]?.name ?? 'the first town'}.</>}
                    </p>
                    {(view === 'snow' || view === 'rain') && manual &&
                        <SnowTable
                            // Snow and Rain share this table, and its manual inputs are uncontrolled with the same row
                            // ids, so it's rebuilt for each view or the other view's typed values would stay on screen.
                            key={view}
                            view={view}
                            towns={shownTowns}
                            sources={shownSources}
                            cells={view === 'rain' ? rainCells : table.cells}
                            // A week of past rain isn't this storm's total, so the Rain table has no Reported row.
                            reports={view === 'rain' ? [] : reportList}
                            manualRows={manualRows}
                            manual={manual}
                            manualGeneration={manualGeneration}
                            onManualChange={handleManualChange}
                            onManualRename={(id, label) => updateManual(renameManualRow(manual, id, label))}
                            onManualRemove={id => updateManual(removeManualRow(manual, id))}
                        />}
                    {view === 'wind' && manual &&
                        <WindTable
                            towns={shownTowns}
                            sources={shownSources}
                            cells={windCells}
                            reports={reportList}
                            manualRows={manualRows}
                            manual={manual}
                            manualGeneration={manualGeneration}
                            onManualChange={handleManualChange}
                            onManualRename={(id, label) => updateManual(renameManualRow(manual, id, label))}
                            onManualRemove={id => updateManual(removeManualRow(manual, id))}
                        />}
                    {view === 'depth' &&
                        (shownSources.length > 0
                            ? <DepthTable towns={shownTowns} sources={shownSources} cells={depthCells} onOpenSource={handleOpenSource} />
                            : <p>No model shown gives snow depth for these dates.</p>)}
                    {manual &&
                        <>
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
                                    Manual {viewInfo[view].label.toLowerCase()} values were entered for {shortDate(manual.start)}
                                    {manual.end && manual.end !== manual.start && ` through ${shortDate(manual.end)}`}.
                                </p>}
                        </>}
                    {canCopy &&
                        <p className="snow-share">
                            <button type="button" onClick={handleCopyLink}>Copy link to this storm</button>
                            {copied && <span role="status">Copied</span>}
                        </p>}
                    {view !== 'depth' &&
                        <SnowTiming
                            view={view}
                            towns={shownTowns}
                            sources={shownSources}
                            cells={view === 'rain' ? rainCells : table.cells}
                            windCells={windCells}
                            start={table.start}
                            end={table.end}
                            hourly={zip => hourly[hourlyKey(zip, table.start, table.end)]}
                            onTownShown={handleTownShown}
                            onOpenSource={handleOpenSource}
                        />}
                    <SnowReports view={view} reports={reports} error={reportsError} names={names} />
                </>}

            {(alerts || alertsError) &&
                <section className="snow-alerts">
                    <h2>{alertWords[alertKind].heading}</h2>
                    {alertsError && <p className="error" role="alert">{alertsError}</p>}
                    {alerts?.message && <p className="error" role="alert">{alerts.message}</p>}
                    {alerts && alerts.status === 'Ok' && viewAlerts.length === 0 && <p>{alertWords[alertKind].none}</p>}
                    {viewAlerts.map(({ alert, zips: covered }) =>
                        <div key={alert.id} className="snow-alert">
                            <AlertPanel alert={alert} />
                            <p className="table-note">{covered.map(zip => names.get(zip) ?? zip).join(', ')}</p>
                        </div>
                    )}
                </section>}

            <section className="snow-maps">
                <h2>{view === 'depth' ? 'Snow depth maps' : `${viewInfo[view].label} maps`}</h2>
                <p>Each outlet's own forecast:</p>
                <ul>
                    {mapLinks[view].map(mapLink => <li key={mapLink.href}><a href={mapLink.href} target="_blank" rel="noopener noreferrer">{mapLink.label}</a></li>)}
                </ul>
            </section>

            <footer className="attribution">
                Weather data by <a href="https://open-meteo.com/">Open-Meteo.com</a> (<a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>),
                the National Weather Service, The Weather Company and <a href="https://www.weatherapi.com/" title="Free Weather API">WeatherAPI.com</a>.
                Storm reports from the <a href="https://mesonet.agron.iastate.edu/">Iowa Environmental Mesonet</a>
            </footer>
        </main>
        </>
    );
}

export default SnowPage;
