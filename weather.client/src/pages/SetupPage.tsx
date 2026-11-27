import { useEffect, useState, type FormEvent } from 'react';
import { ambientDefaultChoices, ambientName, ambientSettings, homeChoiceName } from '../ambientSetup';
import {
    appName,
    checkAmbientWeather,
    checkWeatherApi,
    checkWeatherUnderground,
    getSetupState,
    listAmbientStations,
    primaryAmbientAccount,
    saveSetup,
    type AmbientDeviceChoice,
    type AmbientStationSetting,
    type KeyCheckResult,
    type SetupAmbientKeys,
    type SetupState,
} from '../appBridge';
import { homeStation, loadDefaultStation, saveDefaultStation } from '../defaultStation';
import { loadSavedStations } from '../savedStations';
import { defaultAfterSetup, defaultStationChoices } from '../setupDefault';

const steps = ['Location', 'Weather Underground', 'Ambient Weather', 'WeatherAPI', 'Stations', 'Finish'] as const;
const stationIdPattern = /^[A-Za-z0-9]{3,32}$/;

const checkMessages: Record<KeyCheckResult, string> = {
    Valid: 'That key works.',
    Invalid: "That key wasn't accepted.",
    CouldNotCheck: "The key couldn't be checked right now. You can still save it.",
};

interface KeyTestProps {
    disabled: boolean;
    run: () => Promise<KeyCheckResult>;
}

function KeyTest({ disabled, run }: KeyTestProps) {
    const [result, setResult] = useState<KeyCheckResult>();
    const [checking, setChecking] = useState(false);

    function handleTest() {
        setChecking(true);
        setResult(undefined);
        run().then(setResult).catch(() => setResult('CouldNotCheck')).finally(() => setChecking(false));
    }

    return (
        <>
            <button type="button" disabled={disabled || checking} onClick={handleTest}>{checking ? 'Testing…' : 'Test'}</button>
            {result && <p className={result === 'Invalid' ? 'error' : undefined} role="status">{checkMessages[result]}</p>}
        </>
    );
}

interface KeyFieldProps {
    label: string;
    value: string;
    onChange: (value: string) => void;
}

function KeyField({ label, value, onChange }: KeyFieldProps) {
    return (
        <label className="setup-field">
            {label}
            <input type="password" value={value} autoComplete="off" spellCheck={false} onChange={event => onChange(event.target.value)} />
        </label>
    );
}

interface AmbientAccountForm {
    /** Null for an account added in this setup. */
    id: string | null;
    /** Tells React's list apart for new accounts, which have no ID yet. */
    key: number;
    saved: boolean;
    apiKey: string;
    applicationKey: string;
    remove: boolean;
}

function hasBothKeys(account: AmbientAccountForm) {
    return account.apiKey.trim() !== '' && account.applicationKey.trim() !== '';
}

function halfEntered(account: AmbientAccountForm) {
    return !account.remove && (account.apiKey.trim() === '') !== (account.applicationKey.trim() === '');
}

function keysToSend(account: AmbientAccountForm): SetupAmbientKeys {
    return {
        id: account.id,
        apiKey: account.apiKey.trim() === '' ? null : account.apiKey,
        applicationKey: account.applicationKey.trim() === '' ? null : account.applicationKey,
        remove: account.remove,
    };
}

function SetupPage() {
    const [state, setState] = useState<SetupState>();
    const [loadFailed, setLoadFailed] = useState(false);
    const [step, setStep] = useState(0);
    const [zip, setZip] = useState('');
    const [weatherUndergroundKey, setWeatherUndergroundKey] = useState('');
    const [removeWeatherUnderground, setRemoveWeatherUnderground] = useState(false);
    const [ambientAccounts, setAmbientAccounts] = useState<AmbientAccountForm[]>([]);
    const [nextAccountKey, setNextAccountKey] = useState(1);
    const [ambientDevices, setAmbientDevices] = useState<AmbientDeviceChoice[]>();
    const [savedAmbientStations, setSavedAmbientStations] = useState<AmbientStationSetting[]>([]);
    const [ambientStations, setAmbientStations] = useState<AmbientStationSetting[]>([]);
    const [findingDevices, setFindingDevices] = useState(false);
    const [findFailed, setFindFailed] = useState(false);
    const [weatherApiKey, setWeatherApiKey] = useState('');
    const [removeWeatherApi, setRemoveWeatherApi] = useState(false);
    const [selected, setSelected] = useState<string[]>([]);
    const [addedStations, setAddedStations] = useState<string[]>([]);
    const [newStation, setNewStation] = useState('');
    const [defaultStation, setDefaultStation] = useState(loadDefaultStation);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string>();

    useEffect(() => {
        getSetupState()
            .then(loaded => {
                setState(loaded);
                setZip(loaded.zip ?? '');
                setSelected(loaded.stations.filter(station => station.selected).map(station => station.id));
                setSavedAmbientStations(loaded.ambientStations);

                // The primary account always comes first, so its keys keep their place.
                const others = loaded.ambientAccounts.filter(account => account.id !== primaryAmbientAccount);
                setAmbientAccounts([
                    { id: primaryAmbientAccount, saved: loaded.ambientAccounts.some(account => account.id === primaryAmbientAccount) },
                    ...others.map(account => ({ id: account.id, saved: true })),
                ].map((account, index) => ({ ...account, key: -index - 1, apiKey: '', applicationKey: '', remove: false })));
            })
            .catch(() => setLoadFailed(true));
    }, []);

    if (loadFailed) {
        return <p className="error" role="alert">Setup couldn't be loaded.</p>;
    }

    if (!state) {
        return <p>Loading setup…</p>;
    }

    const usesWeatherUnderground = !removeWeatherUnderground && (state.hasWeatherUnderground || weatherUndergroundKey.trim() !== '');
    const usesAmbient = ambientAccounts.some(account => !account.remove && (account.saved || hasBothKeys(account)));
    const usesWeatherApi = !removeWeatherApi && (state.hasWeatherApi || weatherApiKey.trim() !== '');
    const ambientHalfEntered = ambientAccounts.some(halfEntered);

    // Until the devices are found again, the saved settings stay as they are.
    const ambientToSave = !usesAmbient ? [] : ambientDevices ? ambientStations : savedAmbientStations;
    const ambientChoices = usesAmbient && ambientDevices ? ambientDefaultChoices(ambientDevices, ambientStations) : [];
    const stations = [...state.stations, ...addedStations.map(id => ({ id, name: id, selected: true }))];
    const chosenStations = usesWeatherUnderground ? stations.filter(station => selected.includes(station.id)) : [];
    const defaultChoices = defaultStationChoices([...ambientChoices, ...chosenStations], loadSavedStations(), usesWeatherUnderground);
    const nextDefault = defaultAfterSetup(defaultStation, defaultChoices);

    function updateAccount(key: number, change: Partial<AmbientAccountForm>) {
        setAmbientAccounts(ambientAccounts.map(account => account.key === key ? { ...account, ...change } : account));
    }

    function handleAddAccount() {
        setAmbientAccounts([...ambientAccounts, { id: null, key: nextAccountKey, saved: false, apiKey: '', applicationKey: '', remove: false }]);
        setNextAccountKey(nextAccountKey + 1);
    }

    function handleRemoveAccount(account: AmbientAccountForm) {
        if (account.saved || account.id === primaryAmbientAccount) {
            updateAccount(account.key, { remove: !account.remove, apiKey: '', applicationKey: '' });
        } else {
            setAmbientAccounts(ambientAccounts.filter(other => other.key !== account.key));
        }
    }

    function handleFindDevices() {
        setFindingDevices(true);
        setFindFailed(false);
        listAmbientStations(ambientAccounts.map(keysToSend))
            .then(devices => {
                if (devices === null) {
                    setFindFailed(true);
                    return;
                }

                setAmbientDevices(devices);
                setAmbientStations(ambientSettings(devices, ambientDevices ? ambientStations : savedAmbientStations));
            })
            .catch(() => setFindFailed(true))
            .finally(() => setFindingDevices(false));
    }

    function updateAmbientStation(deviceId: string, change: Partial<AmbientStationSetting>) {
        setAmbientStations(ambientStations.map(setting => {
            if (change.home) {
                // Only one device can be home.
                return { ...setting, ...(setting.deviceId === deviceId ? change : {}), home: setting.deviceId === deviceId };
            }

            return setting.deviceId === deviceId ? { ...setting, ...change } : setting;
        }));
    }

    function handleToggle(id: string) {
        setSelected(selected.includes(id) ? selected.filter(other => other !== id) : [...selected, id]);
    }

    function handleAddStation() {
        const id = newStation.trim();
        if (!stationIdPattern.test(id)) {
            setError('Enter a Weather Underground station ID, such as KMEBRUNS32.');
            return;
        }

        setError(undefined);
        const existing = stations.find(station => station.id.toLowerCase() === id.toLowerCase());
        if (!existing) {
            setAddedStations([...addedStations, id]);
        }

        const chosenId = existing?.id ?? id;
        if (!selected.includes(chosenId)) {
            setSelected([...selected, chosenId]);
        }

        setNewStation('');
    }

    function keyToSave(key: string, remove: boolean) {
        if (remove) {
            return '';
        }

        // Blank means keep what's saved, and null tells .NET the same.
        return key.trim() === '' ? null : key;
    }

    function handleSave() {
        setSaving(true);
        setError(undefined);

        saveSetup({
            zip,
            weatherUndergroundKey: keyToSave(weatherUndergroundKey, removeWeatherUnderground),
            weatherApiKey: keyToSave(weatherApiKey, removeWeatherApi),
            stationIds: selected,
            ambientAccounts: ambientAccounts.map(keysToSend),
            ambientStations: ambientToSave,
        })
            .then(result => {
                if (result.ok) {
                    saveDefaultStation(nextDefault);

                    // A full reload drops everything loaded under the old keys and stations.
                    window.location.replace('/');
                } else {
                    setError(result.error ?? 'Setup could not be saved.');
                    setSaving(false);
                }
            })
            .catch(() => {
                setError('Setup could not be saved.');
                setSaving(false);
            });
    }

    function handleNext(event: FormEvent) {
        event.preventDefault();
        setError(undefined);
        setStep(step + 1);
    }

    const canContinue = step === 0 ? /^\d{5}$/.test(zip.trim()) : step === 2 ? !ambientHalfEntered : true;
    const skippable = (step === 1 && !usesWeatherUnderground) || (step === 2 && !usesAmbient && !ambientHalfEntered) || (step === 3 && !usesWeatherApi);

    return (
        <div className="prose setup">
            <header>
                <h1>{state.complete ? 'Data & keys' : `Set up ${appName}`}</h1>
                <p className="location">Step {step + 1} of {steps.length}: {steps[step]}</p>
            </header>

            {error && <p className="error" role="alert">{error}</p>}

            <form onSubmit={handleNext}>
                {step === 0 &&
                    <>
                        <p>Forecasts, alerts and modelled conditions use this location.</p>
                        <label className="setup-field">
                            ZIP code
                            <input type="text" inputMode="numeric" maxLength={5} value={zip} autoComplete="off" onChange={event => setZip(event.target.value)} />
                        </label>
                    </>}

                {step === 1 &&
                    <>
                        <p>
                            Optional. A personal weather station key adds Weather Underground stations and forecasts. Your key stays
                            encrypted on this phone. Without one, the app uses Open-Meteo.
                        </p>
                        {state.hasWeatherUnderground && !removeWeatherUnderground && <p>A key is saved. Leave this blank to keep it.</p>}
                        {!removeWeatherUnderground && <KeyField label="API key" value={weatherUndergroundKey} onChange={setWeatherUndergroundKey} />}
                        {!removeWeatherUnderground && <KeyTest key={weatherUndergroundKey} disabled={weatherUndergroundKey.trim() === ''} run={() => checkWeatherUnderground(weatherUndergroundKey)} />}
                        {state.hasWeatherUnderground &&
                            <button type="button" onClick={() => setRemoveWeatherUnderground(!removeWeatherUnderground)}>
                                {removeWeatherUnderground ? 'Keep saved key' : 'Remove saved key'}
                            </button>}
                    </>}

                {step === 2 &&
                    <>
                        <p>
                            Optional. Both keys show your own Ambient Weather stations, with the first as the home station. Every
                            station on each account is shown, and you can name them below. Keys stay encrypted on this phone.
                            Without them, the home station shows Open-Meteo's modelled conditions.
                        </p>
                        {ambientAccounts.map((account, index) =>
                            <fieldset key={account.key} className="setup-account">
                                <legend>Account {index + 1}</legend>
                                {account.saved && !account.remove && <p>Keys are saved. Leave these blank to keep them.</p>}
                                {account.remove && <p>These keys will be removed.</p>}
                                {!account.remove &&
                                    <>
                                        <KeyField label="API key" value={account.apiKey} onChange={apiKey => updateAccount(account.key, { apiKey })} />
                                        <KeyField label="Application key" value={account.applicationKey} onChange={applicationKey => updateAccount(account.key, { applicationKey })} />
                                        {halfEntered(account) && <p className="error" role="alert">Ambient Weather needs both keys.</p>}
                                        <KeyTest key={`${account.apiKey}|${account.applicationKey}`} disabled={!hasBothKeys(account)} run={() => checkAmbientWeather(account.apiKey, account.applicationKey)} />
                                    </>}
                                {(account.saved || index > 0) &&
                                    <button type="button" onClick={() => handleRemoveAccount(account)}>
                                        {account.remove ? 'Keep saved keys' : account.saved ? 'Remove saved keys' : 'Remove account'}
                                    </button>}
                            </fieldset>
                        )}
                        <div className="setup-actions">
                            <button type="button" onClick={handleAddAccount}>Add another account</button>
                            <button type="button" disabled={!usesAmbient || ambientHalfEntered || findingDevices} onClick={handleFindDevices}>
                                {findingDevices ? 'Finding…' : ambientDevices ? 'Find stations again' : 'Find my stations'}
                            </button>
                        </div>
                        {findFailed && <p className="error" role="alert">The stations couldn't be found. Check the keys and your connection.</p>}
                        {ambientDevices?.length === 0 && <p>No stations were found on these accounts.</p>}
                        {ambientDevices && ambientDevices.length > 0 && usesAmbient &&
                            <ul className="setup-ambient">
                                {ambientStations.map(setting => {
                                    const device = ambientDevices.find(other => other.id === setting.deviceId);
                                    return (
                                        <li key={setting.deviceId}>
                                            <label className="setup-field">
                                                Name
                                                <input type="text" value={setting.name ?? ''} placeholder={ambientName({ ...setting, name: null }, device)} maxLength={40} autoComplete="off" onChange={event => updateAmbientStation(setting.deviceId, { name: event.target.value })} />
                                            </label>
                                            <label className="setup-field">
                                                ZIP code
                                                <input type="text" inputMode="numeric" maxLength={5} value={setting.zip ?? ''} placeholder={setting.home ? zip.trim() : 'Needed for forecasts'} autoComplete="off" onChange={event => updateAmbientStation(setting.deviceId, { zip: event.target.value })} />
                                            </label>
                                            <label>
                                                <input type="radio" name="ambient-home" checked={setting.home} onChange={() => updateAmbientStation(setting.deviceId, { home: true, hidden: false })} />
                                                Home station
                                            </label>
                                            {!setting.home &&
                                                <label>
                                                    <input type="checkbox" checked={!setting.hidden} onChange={() => updateAmbientStation(setting.deviceId, { hidden: !setting.hidden })} />
                                                    Show
                                                </label>}
                                        </li>
                                    );
                                })}
                            </ul>}
                    </>}

                {step === 3 &&
                    <>
                        <p>
                            Optional. A WeatherAPI.com key adds its 3-day forecast to the Storm Forecast page. It stays encrypted on
                            this phone. Without one, that row is left out.
                        </p>
                        {state.hasWeatherApi && !removeWeatherApi && <p>A key is saved. Leave this blank to keep it.</p>}
                        {!removeWeatherApi && <KeyField label="API key" value={weatherApiKey} onChange={setWeatherApiKey} />}
                        {!removeWeatherApi && <KeyTest key={weatherApiKey} disabled={weatherApiKey.trim() === ''} run={() => checkWeatherApi(weatherApiKey)} />}
                        {state.hasWeatherApi &&
                            <button type="button" onClick={() => setRemoveWeatherApi(!removeWeatherApi)}>
                                {removeWeatherApi ? 'Keep saved key' : 'Remove saved key'}
                            </button>}
                    </>}

                {step === 4 && !usesWeatherUnderground &&
                    <p>Other stations need a Weather Underground key. Add one later from Settings to choose stations.</p>}

                {step === 4 && usesWeatherUnderground &&
                    <>
                        <p>Choose the stations to include. You can search for more on the Station page afterwards.</p>
                        <div className="setup-actions">
                            <button type="button" onClick={() => setSelected(stations.map(station => station.id))}>Select all</button>
                            <button type="button" onClick={() => setSelected([])}>Clear</button>
                        </div>
                        <ul className="setup-stations">
                            {stations.map(station =>
                                <li key={station.id}>
                                    <label>
                                        <input type="checkbox" checked={selected.includes(station.id)} onChange={() => handleToggle(station.id)} />
                                        {station.name}
                                    </label>
                                </li>
                            )}
                        </ul>
                        <label className="setup-field">
                            Add a station ID
                            <input type="text" value={newStation} autoComplete="off" spellCheck={false} onChange={event => setNewStation(event.target.value)} />
                        </label>
                        <button type="button" disabled={!newStation.trim()} onClick={handleAddStation}>Add</button>
                    </>}

                {step === 5 &&
                    <>
                        <ul>
                            <li>Location: ZIP {zip.trim()}</li>
                            <li>Home station: {usesAmbient ? homeChoiceName(ambientDevices, ambientToSave) : "Open-Meteo's modelled conditions"}</li>
                            {ambientChoices.length > 0 && <li>Other Ambient Weather stations: {ambientChoices.map(station => station.name).join(', ')}</li>}
                            <li>Weather Underground: {usesWeatherUnderground ? `${chosenStations.length} stations and the forecast column` : 'not used'}</li>
                            <li>WeatherAPI.com: {usesWeatherApi ? 'the Storm Forecast row' : 'not used'}</li>
                            <li>Forecasts, alerts and maps work with or without keys</li>
                        </ul>
                        {defaultChoices.length > 0 &&
                            <label className="setup-field">
                                Default station
                                <select value={nextDefault} onChange={event => setDefaultStation(event.target.value)}>
                                    <option value={homeStation}>{usesAmbient ? homeChoiceName(ambientDevices, ambientToSave) : 'Home station'}</option>
                                    {defaultChoices.map(station => <option key={station.id} value={station.id}>{station.name}</option>)}
                                </select>
                            </label>}
                    </>}

                <div className="setup-actions">
                    {step > 0 && <button type="button" onClick={() => setStep(step - 1)}>Back</button>}
                    {step === 0 && state.complete && <button type="button" onClick={() => window.location.replace('/')}>Cancel</button>}
                    {step < steps.length - 1 && <button type="submit" disabled={!canContinue}>{skippable ? 'Skip' : 'Next'}</button>}
                    {step === steps.length - 1 && <button type="button" disabled={saving} onClick={handleSave}>{saving ? 'Saving…' : 'Save'}</button>}
                </div>
            </form>
        </div>
    );
}

export default SetupPage;
