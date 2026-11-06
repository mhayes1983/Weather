import { useEffect, useRef, useState, type Dispatch, type SetStateAction } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import {
    arcgisTileUrl, fetchRadarTimes, fetchTemperatureLegend, legendUrl, mapModes,
    type LegendKeyEntry, type LegendScale, type MapMode, type MapModeId, type TemperatureLegendStep,
} from '../api/radar';
import { imageVersion, keepLayer, mapLayerKey, selectFrames } from '../api/radarFrames';
import { displayTemperature, type Unit } from '../temperature';

const frameMs = 700;
const lastFrameMs = 1_500;
const overlayOpacity = 0.45;

const areas: { label: string; bounds: L.LatLngBoundsLiteral }[] = [
    { label: 'Maine', bounds: [[42.9, -71.2], [47.5, -66.8]] },
    { label: 'Northeast', bounds: [[38.5, -80.5], [47.6, -66.8]] },
];

function formatFrame(time: string) {
    return new Date(time).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
}

function frameKey(mode: MapMode, time: string) {
    return mapLayerKey(mode.id, time);
}

function overlayKey(mode: MapMode) {
    return `${mode.id}|overlay`;
}

// A static image's URL carries its version, so a newer image gets a new URL and
// browsers don't show a cached one.
function createLayer(mode: MapMode, time: string | undefined, version: number): L.TileLayer {
    if (mode.kind === 'arcgis') {
        const layer = L.tileLayer('', { opacity: 0, attribution: mode.attribution, zIndex: 2 });
        layer.getTileUrl = coords =>
            arcgisTileUrl(mode.service, mode.layer, coords.x, coords.y, coords.z, version);
        return layer;
    }

    return L.tileLayer.wms(mode.service, {
        layers: mode.layer,
        format: 'image/png',
        transparent: true,
        version: '1.3.0',
        opacity: 0,
        zIndex: 2,
        attribution: mode.attribution,
        ...(time ? { time } : {}),
    });
}

type FailedTile = { key: string; tile: HTMLElement };

function trackTiles(layer: L.TileLayer, layerKey: string, setFailedTiles: Dispatch<SetStateAction<FailedTile[]>>) {
    layer.on('tileerror', ({ tile }) => setFailedTiles(failed => [...failed, { key: layerKey, tile }]));
    layer.on('tileunload', ({ tile }) => setFailedTiles(failed =>
        failed.some(other => other.tile === tile) ? failed.filter(other => other.tile !== tile) : failed));
}

interface RadarPageProps {
    unit: Unit;
}

function RadarPage({ unit }: RadarPageProps) {
    const mapElement = useRef<HTMLDivElement>(null);
    const map = useRef<L.Map>(null);
    const layers = useRef(new Map<string, L.TileLayer>());

    const [modeId, setModeId] = useState<MapModeId>('radar');
    const [frames, setFrames] = useState<string[]>([]);
    const [selected, setSelected] = useState<string>();
    const [shown, setShown] = useState<string>();
    const [playing, setPlaying] = useState(false);
    const [timesFailed, setTimesFailed] = useState(false);
    const [failedTiles, setFailedTiles] = useState<FailedTile[]>([]);
    const [staticVersion, setStaticVersion] = useState(0);

    const mode = mapModes.find(candidate => candidate.id === modeId)!;

    // The newest frame shows unless the user or the loop has picked another.
    const newest = frames.at(-1);
    const current = selected && frames.includes(selected) ? selected : newest;
    const key = mapLayerKey(mode.id, current, mode.animated ? undefined : staticVersion);
    const tilesFailed = failedTiles.some(failed => failed.key === shown || failed.key === overlayKey(mode));

    useEffect(() => {
        const loadedLayers = layers.current;
        const radarMap = L.map(mapElement.current!, { minZoom: 4, maxZoom: 10 });
        radarMap.fitBounds(areas[0].bounds);

        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        }).addTo(radarMap);

        map.current = radarMap;

        return () => {
            radarMap.remove();
            map.current = null;
            loadedLayers.clear();
        };
    }, []);

    useEffect(() => {
        const controller = new AbortController();

        // Asks for the newest scan again when the times can't be loaded. Browsers reuse
        // images with an unchanged URL, so this asks for the current time, which NOAA
        // answers with its nearest scan.
        function refreshLatest() {
            const layer = layers.current.get(mapLayerKey(mode.id, undefined));
            if (layer instanceof L.TileLayer.WMS) {
                const latest = { layers: mode.layer, time: new Date().toISOString() };
                layer.setParams(latest);
            }
        }

        function load() {
            // A static image moves to a new version, and so a new layer, only once its
            // refresh interval has passed. The old one stays up until the new one loads.
            if (!mode.animated) {
                setStaticVersion(imageVersion(mode.refreshMs));
                return;
            }

            fetchRadarTimes(mode, controller.signal)
                .then(times => {
                    setFrames(selectFrames(times));
                    setTimesFailed(false);
                })
                .catch(() => {
                    if (!controller.signal.aborted) {
                        setTimesFailed(true);
                        refreshLatest();
                    }
                });
        }

        function loadIfVisible() {
            if (!document.hidden) {
                load();
            }
        }

        if (mode.animated) {
            load();
        }
        const timer = window.setInterval(loadIfVisible, mode.refreshMs);
        document.addEventListener('visibilitychange', loadIfVisible);

        return () => {
            controller.abort();
            window.clearInterval(timer);
            document.removeEventListener('visibilitychange', loadIfVisible);
        };
    }, [mode]);

    // A mode's overlay sits under its main layer at the newest time.
    useEffect(() => {
        const overlay = mode.overlay;
        if (!overlay) {
            return;
        }

        const layer = L.tileLayer.wms(overlay.service, {
            layers: overlay.layer,
            format: 'image/png',
            transparent: true,
            version: '1.3.0',
            opacity: overlayOpacity,
            zIndex: 1,
            attribution: overlay.attribution,
        });
        trackTiles(layer, overlayKey(mode), setFailedTiles);
        layer.addTo(map.current!);

        function refresh() {
            if (!document.hidden) {
                const latest = { layers: overlay!.layer, time: new Date().toISOString() };
                layer.setParams(latest);
            }
        }

        const timer = window.setInterval(refresh, mode.refreshMs);
        document.addEventListener('visibilitychange', refresh);

        return () => {
            window.clearInterval(timer);
            document.removeEventListener('visibilitychange', refresh);
            layer.remove();
        };
    }, [mode]);

    useEffect(() => {
        const radarMap = map.current!;
        const loadedLayers = layers.current;
        const frameKeys = frames.map(frame => frameKey(mode, frame));

        let layer = loadedLayers.get(key);
        if (!layer) {
            layer = createLayer(mode, current, staticVersion);
            trackTiles(layer, key, setFailedTiles);
            layer.addTo(radarMap);
            loadedLayers.set(key, layer);
        }

        const target = layer;

        // The old frame, even from another mode, stays up until the new one has loaded,
        // so the map doesn't flash.
        function reveal() {
            loadedLayers.forEach((other, otherKey) => {
                const keep = keepLayer(otherKey, key, playing, frameKeys);
                if (!keep) {
                    other.remove();
                    loadedLayers.delete(otherKey);
                } else {
                    other.setOpacity(other === target ? mode.opacity : 0);
                }
            });
            setShown(key);
        }

        if (target.isLoading()) {
            target.once('load', reveal);
        } else {
            queueMicrotask(reveal);
        }

        return () => {
            target.off('load', reveal);
        };
    }, [mode, key, current, frames, playing, staticVersion]);

    useEffect(() => {
        if (!playing || frames.length < 2 || shown !== key || !current) {
            return;
        }

        const index = frames.indexOf(current);
        const timer = window.setTimeout(
            () => setSelected(frames[(index + 1) % frames.length]),
            index === frames.length - 1 ? lastFrameMs : frameMs);

        return () => window.clearTimeout(timer);
    }, [playing, frames, shown, key, current]);

    function handleMode(next: MapModeId) {
        if (next === modeId) {
            return;
        }

        const nextMode = mapModes.find(candidate => candidate.id === next)!;
        if (!nextMode.animated) {
            setStaticVersion(imageVersion(nextMode.refreshMs));
        }

        setModeId(next);
        setFrames([]);
        setSelected(undefined);
        setPlaying(false);
        setTimesFailed(false);
    }

    function handleSlider(index: number) {
        setPlaying(false);
        setSelected(frames[index]);
    }

    function handleShowLatest() {
        setPlaying(false);
        setSelected(undefined);
    }

    function timeLabel() {
        if (!mode.animated) {
            return 'Latest forecast';
        }

        if (!current) {
            return 'Latest image';
        }

        return `${formatFrame(current)}${current === newest && !timesFailed ? ' (latest)' : ''}`;
    }

    return (
        <main>
            <header>
                <h1>Weather Map</h1>
                <p className="location">{mode.label} · {timeLabel()}</p>
            </header>

            <div className="radar-buttons radar-modes" role="group" aria-label="Map type">
                {mapModes.map(candidate =>
                    <button
                        key={candidate.id}
                        type="button"
                        aria-pressed={candidate.id === modeId}
                        onClick={() => handleMode(candidate.id)}>
                        {candidate.label}
                    </button>
                )}
            </div>

            <div className="controls">
                <div className="radar-buttons" role="group" aria-label="Map area">
                    {areas.map(area =>
                        <button key={area.label} type="button" onClick={() => map.current?.fitBounds(area.bounds)}>
                            {area.label}
                        </button>
                    )}
                </div>

                {frames.length > 1 &&
                    <div className="radar-buttons">
                        <button type="button" onClick={() => setPlaying(!playing)}>{playing ? 'Pause' : 'Play'}</button>
                        <button type="button" disabled={current === newest} onClick={handleShowLatest}>Latest</button>
                    </div>}
            </div>

            {timesFailed &&
                <p className="error" role="alert">
                    {newest
                        ? `Map times couldn't be refreshed, so the newest image here is from ${formatFrame(newest)}.`
                        : 'The loop is unavailable, so only the latest image is shown.'}
                </p>}
            {tilesFailed && <p className="error" role="alert">Some map images couldn't be loaded.</p>}

            <div ref={mapElement} className="radar-map" role="application" aria-label={`Zoomable ${mode.label.toLowerCase()} map`} />

            {frames.length > 1 && current &&
                <label className="radar-slider">
                    <span>{formatFrame(frames[0])}</span>
                    <input
                        type="range"
                        aria-label="Map time"
                        aria-valuetext={formatFrame(current)}
                        min={0}
                        max={frames.length - 1}
                        value={frames.indexOf(current)}
                        onChange={event => handleSlider(Number(event.target.value))}
                    />
                    <span>{formatFrame(frames[frames.length - 1])}</span>
                </label>}

            <Legend key={mode.id} mode={mode} unit={unit} />

            <footer className="attribution">
                From <a href="https://www.weather.gov/">NOAA/NWS</a>. {mode.note}
                {mode.noteLink && <> <a href={mode.noteLink.href}>{mode.noteLink.text}</a>.</>}
            </footer>
        </main>
    );
}

function Legend({ mode, unit }: { mode: MapMode; unit: Unit }) {
    if (mode.kind === 'arcgis') {
        return <TemperatureLegend unit={unit} />;
    }

    if (mode.legendKey) {
        return <KeyedLegend mode={mode} entries={mode.legendKey} />;
    }

    if (mode.scale) {
        return <ScaleLegend label={mode.label} scale={mode.scale} />;
    }

    return <img className="radar-legend" src={legendUrl(mode)} alt={`${mode.label} colour scale`} />;
}

function ScaleLegend({ label, scale }: { label: string; scale: LegendScale }) {
    return (
        <figure className="scale-legend" aria-label={`${label} colour scale: ${scale.start} to ${scale.end}`}>
            <div className="scale-legend-bar" style={{ background: `linear-gradient(to right, ${scale.colours.join(', ')})` }} />
            <div className="scale-legend-labels" aria-hidden="true">
                <span>← {scale.start}</span>
                <span>{scale.end} →</span>
            </div>
        </figure>
    );
}

// NOAA's legend image with a zone over each equal block, which names its colour on
// hover or keyboard focus, and stays named after a tap so it works on touch screens.
function KeyedLegend({ mode, entries }: { mode: MapMode; entries: LegendKeyEntry[] }) {
    const [hovered, setHovered] = useState<number>();
    const [pinned, setPinned] = useState<number>();
    const active = hovered ?? pinned;
    const entry = active === undefined ? undefined : entries[active];
    const width = 100 / entries.length;

    function tooltipClass(index: number) {
        if (index === 0) {
            return 'legend-key-tooltip start';
        }

        return index === entries.length - 1 ? 'legend-key-tooltip end' : 'legend-key-tooltip';
    }

    return (
        <div className="legend-key">
            <div className="legend-key-image">
                <img className="radar-legend" src={legendUrl(mode)} alt="" />
                {entries.map((candidate, index) =>
                    <button
                        key={candidate.code}
                        type="button"
                        className="legend-key-zone"
                        style={{ left: `${index * width}%`, width: `${width}%` }}
                        aria-label={`${candidate.code}: ${candidate.label}`}
                        aria-pressed={pinned === index}
                        onMouseEnter={() => setHovered(index)}
                        onMouseLeave={() => setHovered(undefined)}
                        onFocus={() => setHovered(index)}
                        onBlur={() => setHovered(undefined)}
                        onClick={() => setPinned(pinned === index ? undefined : index)}>
                        {active === index && <span className={tooltipClass(index)} aria-hidden="true">{candidate.label}</span>}
                    </button>
                )}
            </div>
            <p className="legend-key-caption" aria-live="polite">
                {entry
                    ? <><strong>{entry.code}, {entry.label}:</strong> {entry.description}</>
                    : 'Point at or tap a colour to see what it means.'}
            </p>
        </div>
    );
}

function TemperatureLegend({ unit }: { unit: Unit }) {
    const [steps, setSteps] = useState<TemperatureLegendStep[]>();
    const [failed, setFailed] = useState(false);

    useEffect(() => {
        const controller = new AbortController();
        fetchTemperatureLegend(controller.signal)
            .then(setSteps)
            .catch(() => {
                if (!controller.signal.aborted) {
                    setFailed(true);
                }
            });

        return () => controller.abort();
    }, []);

    if (failed) {
        return <p className="error">The temperature colour scale couldn't be loaded.</p>;
    }

    if (!steps) {
        return null;
    }

    // Ticks fall on round numbers in the chosen unit, placed by their °F value along
    // the bands actually shown, each 1°F wide and labelled by its top.
    const min = steps[0].fahrenheit - 1;
    const max = steps[steps.length - 1].fahrenheit;
    const ticks = unit === 'F'
        ? [0, 20, 40, 60, 80, 100]
        : [-20, -10, 0, 10, 20, 30, 40].map(celsius => celsius * 9 / 5 + 32)

    return (
        <figure className="temperature-legend" aria-label="Temperature colour scale">
            <TemperatureBar steps={steps} />
            <div className="temperature-legend-ticks" aria-hidden="true">
                {ticks.filter(fahrenheit => fahrenheit > min && fahrenheit <= max).map(fahrenheit =>
                    <span key={fahrenheit} style={{ left: `${(fahrenheit - min) / (max - min) * 100}%` }}>
                        {displayTemperature(fahrenheit, unit)}°
                    </span>
                )}
            </div>
        </figure>
    );
}

// One pixel column per 1°F swatch, stretched by the browser into a smooth bar. A row
// of swatch images leaves hairline gaps between them on high-density screens.
function TemperatureBar({ steps }: { steps: TemperatureLegendStep[] }) {
    const canvas = useRef<HTMLCanvasElement>(null);

    useEffect(() => {
        const context = canvas.current?.getContext('2d');
        if (!context) {
            return;
        }

        let cancelled = false;
        const images = steps.map(step => {
            const image = new Image();
            image.src = step.image;
            return image;
        });

        Promise.all(images.map(image => image.decode()))
            .then(() => {
                if (!cancelled) {
                    // Each swatch is a solid square, so its centre is its colour.
                    images.forEach((image, index) => {
                        const size = Math.min(image.naturalWidth, image.naturalHeight);
                        context.drawImage(image, size / 2 - 1, size / 2 - 1, 2, 2, index, 0, 1, 1);
                    });
                }
            })
            .catch(() => {
                // The ticks and note still explain the map without the bar.
            });

        return () => {
            cancelled = true;
        };
    }, [steps]);

    return <canvas ref={canvas} className="temperature-legend-bar" width={steps.length} height={1} aria-hidden="true" />;
}

export default RadarPage;
