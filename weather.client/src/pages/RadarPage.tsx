import { useEffect, useRef, useState } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { fetchRadarTimes, radarLayer, radarLegend, radarService } from '../api/radar';
import { selectFrames } from '../api/radarFrames';

const refreshMs = 120_000;
const frameMs = 700;
const lastFrameMs = 1_500;
const radarOpacity = 0.7;
const latestKey = 'latest';

const areas: { label: string; bounds: L.LatLngBoundsLiteral }[] = [
    { label: 'Maine', bounds: [[42.9, -71.2], [47.5, -66.8]] },
    { label: 'Northeast', bounds: [[38.5, -80.5], [47.6, -66.8]] },
];

function formatFrame(time: string) {
    return new Date(time).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
}

function RadarPage() {
    const mapElement = useRef<HTMLDivElement>(null);
    const map = useRef<L.Map>(null);
    const layers = useRef(new Map<string, L.TileLayer.WMS>());

    const [frames, setFrames] = useState<string[]>([]);
    const [selected, setSelected] = useState<string>();
    const [shown, setShown] = useState<string>();
    const [playing, setPlaying] = useState(false);
    const [timesFailed, setTimesFailed] = useState(false);
    const [failedTiles, setFailedTiles] = useState<{ key: string; tile: HTMLElement }[]>([]);

    // The newest frame shows unless the user or the loop has picked another.
    const newest = frames.at(-1);
    const current = selected && frames.includes(selected) ? selected : newest;
    const key = current ?? latestKey;
    const tilesFailed = failedTiles.some(failed => failed.key === shown);

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

        function load() {
            fetchRadarTimes(controller.signal)
                .then(times => {
                    setFrames(selectFrames(times));
                    setTimesFailed(false);
                })
                .catch(() => {
                    if (!controller.signal.aborted) {
                        setTimesFailed(true);

                        // Browsers reuse images with an unchanged URL, so this asks for the
                        // current time, which NOAA answers with its nearest scan.
                        const latest = { layers: radarLayer, time: new Date().toISOString() };
                        layers.current.get(latestKey)?.setParams(latest);
                    }
                });
        }

        function loadIfVisible() {
            if (!document.hidden) {
                load();
            }
        }

        load();
        const timer = window.setInterval(loadIfVisible, refreshMs);
        document.addEventListener('visibilitychange', loadIfVisible);

        return () => {
            controller.abort();
            window.clearInterval(timer);
            document.removeEventListener('visibilitychange', loadIfVisible);
        };
    }, []);

    useEffect(() => {
        const radarMap = map.current!;
        const loadedLayers = layers.current;

        let layer = loadedLayers.get(key);
        if (!layer) {
            layer = L.tileLayer.wms(radarService, {
                layers: radarLayer,
                format: 'image/png',
                transparent: true,
                version: '1.3.0',
                opacity: 0,
                attribution: 'Radar: <a href="https://www.weather.gov/">NOAA/NWS</a>',
                ...(current ? { time: current } : {}),
            });
            layer.on('tileerror', ({ tile }) => setFailedTiles(failed => [...failed, { key, tile }]));
            layer.on('tileunload', ({ tile }) => setFailedTiles(failed =>
                failed.some(other => other.tile === tile) ? failed.filter(other => other.tile !== tile) : failed));
            layer.addTo(radarMap);
            loadedLayers.set(key, layer);
        }

        const target = layer;

        // The old frame stays up until the new one has loaded, so the map doesn't flash.
        function reveal() {
            loadedLayers.forEach((other, otherKey) => {
                const keep = otherKey === key || (playing && frames.includes(otherKey));
                if (!keep) {
                    other.remove();
                    loadedLayers.delete(otherKey);
                } else {
                    other.setOpacity(other === target ? radarOpacity : 0);
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
    }, [key, current, frames, playing]);

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

    function handleSlider(index: number) {
        setPlaying(false);
        setSelected(frames[index]);
    }

    function handleShowLatest() {
        setPlaying(false);
        setSelected(undefined);
    }

    return (
        <main>
            <header>
                <h1>Weather Radar</h1>
                <p className="location">
                    {current ? `${formatFrame(current)}${current === newest && !timesFailed ? ' (latest)' : ''}` : 'Latest scan'}
                </p>
            </header>

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
                        ? `Radar times couldn't be refreshed, so the newest scan here is from ${formatFrame(newest)}.`
                        : 'The radar loop is unavailable, so only the latest scan is shown.'}
                </p>}
            {tilesFailed && <p className="error" role="alert">Some radar images couldn't be loaded.</p>}

            <div ref={mapElement} className="radar-map" role="application" aria-label="Zoomable weather radar map" />

            {frames.length > 1 && current &&
                <label className="radar-slider">
                    <span>{formatFrame(frames[0])}</span>
                    <input
                        type="range"
                        aria-label="Radar time"
                        aria-valuetext={formatFrame(current)}
                        min={0}
                        max={frames.length - 1}
                        value={frames.indexOf(current)}
                        onChange={event => handleSlider(Number(event.target.value))}
                    />
                    <span>{formatFrame(frames[frames.length - 1])}</span>
                </label>}

            <img className="radar-legend" src={radarLegend} alt="Radar colour scale" />

            <footer className="attribution">
                Radar from <a href="https://www.weather.gov/">NOAA/NWS</a>. It shows precipitation only, and snow can look weaker than it is.
            </footer>
        </main>
    );
}

export default RadarPage;
