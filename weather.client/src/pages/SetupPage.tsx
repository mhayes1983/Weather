import { useEffect, useState, type FormEvent } from 'react';
import {
    checkAmbientWeather,
    checkWeatherUnderground,
    getSetupState,
    saveSetup,
    type KeyCheckResult,
    type SetupState,
} from '../appBridge';
import { homeStation, saveDefaultStation } from '../defaultStation';

const steps = ['Location', 'Weather Underground', 'Ambient Weather', 'Stations', 'Finish'] as const;
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

function SetupPage() {
    const [state, setState] = useState<SetupState>();
    const [loadFailed, setLoadFailed] = useState(false);
    const [step, setStep] = useState(0);
    const [zip, setZip] = useState('');
    const [weatherUndergroundKey, setWeatherUndergroundKey] = useState('');
    const [removeWeatherUnderground, setRemoveWeatherUnderground] = useState(false);
    const [ambientApiKey, setAmbientApiKey] = useState('');
    const [ambientApplicationKey, setAmbientApplicationKey] = useState('');
    const [removeAmbient, setRemoveAmbient] = useState(false);
    const [selected, setSelected] = useState<string[]>([]);
    const [addedStations, setAddedStations] = useState<string[]>([]);
    const [newStation, setNewStation] = useState('');
    const [defaultStation, setDefaultStation] = useState(homeStation);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string>();

    useEffect(() => {
        getSetupState()
            .then(loaded => {
                setState(loaded);
                setZip(loaded.zip ?? '');
                setSelected(loaded.stations.filter(station => station.selected).map(station => station.id));
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
    const usesAmbient = !removeAmbient && (state.hasAmbientWeather || (ambientApiKey.trim() !== '' && ambientApplicationKey.trim() !== ''));
    const ambientHalfEntered = (ambientApiKey.trim() === '') !== (ambientApplicationKey.trim() === '');
    const stations = [...state.stations, ...addedStations.map(id => ({ id, name: id, selected: true }))];
    const chosenStations = usesWeatherUnderground ? stations.filter(station => selected.includes(station.id)) : [];

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
        if (!stations.some(station => station.id === id)) {
            setAddedStations([...addedStations, id]);
        }

        if (!selected.includes(id)) {
            setSelected([...selected, id]);
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
            ambientApiKey: keyToSave(ambientApiKey, removeAmbient),
            ambientApplicationKey: keyToSave(ambientApplicationKey, removeAmbient),
            stationIds: selected,
        })
            .then(result => {
                if (result.ok) {
                    saveDefaultStation(chosenStations.some(station => station.id === defaultStation) ? defaultStation : homeStation);

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
    const skippable = (step === 1 && !usesWeatherUnderground) || (step === 2 && !usesAmbient && !ambientHalfEntered);

    return (
        <div className="prose setup">
            <header>
                <h1>{state.complete ? 'Data & keys' : 'Set up Weather'}</h1>
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
                            Optional. Both keys show your own Ambient Weather station as the home station. They stay encrypted on this
                            phone. Without them, the home station shows Open-Meteo's modelled conditions.
                        </p>
                        {state.hasAmbientWeather && !removeAmbient && <p>Keys are saved. Leave these blank to keep them.</p>}
                        {!removeAmbient &&
                            <>
                                <KeyField label="API key" value={ambientApiKey} onChange={setAmbientApiKey} />
                                <KeyField label="Application key" value={ambientApplicationKey} onChange={setAmbientApplicationKey} />
                                {ambientHalfEntered && <p className="error" role="alert">Ambient Weather needs both keys.</p>}
                                <KeyTest key={`${ambientApiKey}|${ambientApplicationKey}`} disabled={ambientApiKey.trim() === '' || ambientApplicationKey.trim() === ''} run={() => checkAmbientWeather(ambientApiKey, ambientApplicationKey)} />
                            </>}
                        {state.hasAmbientWeather &&
                            <button type="button" onClick={() => setRemoveAmbient(!removeAmbient)}>
                                {removeAmbient ? 'Keep saved keys' : 'Remove saved keys'}
                            </button>}
                    </>}

                {step === 3 && !usesWeatherUnderground &&
                    <p>Other stations need a Weather Underground key. Add one later from Settings to choose stations.</p>}

                {step === 3 && usesWeatherUnderground &&
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

                {step === 4 &&
                    <>
                        <ul>
                            <li>Location: ZIP {zip.trim()}</li>
                            <li>Home station: {usesAmbient ? 'your Ambient Weather station' : "Open-Meteo's modelled conditions"}</li>
                            <li>Weather Underground: {usesWeatherUnderground ? `${chosenStations.length} stations and the forecast column` : 'not used'}</li>
                            <li>Forecasts, alerts and radar work with or without keys</li>
                        </ul>
                        {chosenStations.length > 0 &&
                            <label className="setup-field">
                                Default station
                                <select value={defaultStation} onChange={event => setDefaultStation(event.target.value)}>
                                    <option value={homeStation}>Home station</option>
                                    {chosenStations.map(station => <option key={station.id} value={station.id}>{station.name}</option>)}
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
