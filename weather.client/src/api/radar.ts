export type MapModeId = 'radar' | 'precipType' | 'temperature' | 'clouds' | 'lightning';

export interface WmsLayer {
    service: string;
    layer: string;
    attribution: string;
}

export interface MapMode {
    id: MapModeId;
    label: string;
    kind: 'wms' | 'arcgis';
    service: string;
    layer: string;
    attribution: string;
    // Animated modes load their scan times and loop; the others show the newest image.
    animated: boolean;
    opacity: number;
    // How often the newest image is checked for.
    refreshMs: number;
    note: string;
    // Shown with the main layer, always at its newest time.
    overlay?: WmsLayer;
    // What each block of a legend image means, left to right. The image must be
    // divided into this many equal blocks.
    legendKey?: LegendKeyEntry[];
}

export interface LegendKeyEntry {
    code: string;
    label: string;
    description: string;
}

// NOAA's legend shows only these codes, in this order, as seven equal blocks. They're
// MRMS precipitation flag values 1, 3, 6, 7, 10, 91 and 96. MRMS has no sleet,
// freezing rain or mixed category.
export const precipTypeKey: LegendKeyEntry[] = [
    { code: 'WS', label: 'Warm stratiform rain', description: 'Steady, widespread rain.' },
    { code: 'S', label: 'Snow', description: 'Snow. Sleet and freezing rain have no colour of their own, so they show as snow or rain.' },
    { code: 'C', label: 'Convective rain', description: 'Showers and thunderstorms, often heavy and short-lived.' },
    { code: 'H', label: 'Hail', description: 'Rain mixed with hail, from strong thunderstorms.' },
    { code: 'CS', label: 'Cool stratiform rain', description: 'Steady rain in colder air, often near the rain-snow line.' },
    { code: 'ST', label: 'Tropical stratiform rain', description: 'Steady rain in very moist, tropical air, which can be heavy.' },
    { code: 'CT', label: 'Tropical convective rain', description: 'Downpours in very moist, tropical air, with very high rain rates.' },
];

const noaa = '<a href="https://www.weather.gov/">NOAA/NWS</a>';
const opengeo = 'https://opengeo.ncep.noaa.gov/geoserver';
const nowCoast = 'https://nowcoast.noaa.gov/geoserver';

export const temperatureService = 'https://mapservices.weather.noaa.gov/raster/rest/services/NDFD/NDFD_temp/MapServer';

// The group of 3-hourly temperature images. ArcGIS draws only the group's earliest
// image that's still valid, which is the forecast for now or the next few hours.
// Its sublayers can't be shown on their own: asking for one returns a blank image.
const temperatureGroup = 0;

// Any image sublayer of the group, for its colour scale. They all share one.
const temperatureLegendLayer = 8;

export const mapModes: MapMode[] = [
    {
        id: 'radar',
        label: 'Radar',
        kind: 'wms',
        service: `${opengeo}/conus/conus_bref_qcd/ows`,
        layer: 'conus_bref_qcd',
        attribution: `Radar: ${noaa}`,
        animated: true,
        opacity: 0.7,
        refreshMs: 120_000,
        note: 'Radar shows precipitation only, and snow can look weaker than it is.',
    },
    {
        id: 'precipType',
        label: 'Precip type',
        kind: 'wms',
        service: `${opengeo}/conus/conus_pcpn_typ/ows`,
        layer: 'conus_pcpn_typ',
        attribution: `Precipitation type: ${noaa}`,
        animated: true,
        opacity: 0.75,
        refreshMs: 120_000,
        note: 'Precipitation type is estimated from radar and model temperatures, so it can be wrong near the rain-snow line, and sleet or freezing rain show as snow or rain.',
        legendKey: precipTypeKey,
    },
    {
        id: 'temperature',
        label: 'Temperature',
        kind: 'arcgis',
        service: temperatureService,
        layer: String(temperatureGroup),
        attribution: `Temperature: ${noaa}`,
        animated: false,
        opacity: 0.6,
        refreshMs: 1_800_000,
        note: 'Temperature is the National Weather Service forecast for the next few hours, not measured readings.',
    },
    {
        id: 'clouds',
        label: 'Clouds',
        kind: 'wms',
        service: `${nowCoast}/satellite/goes_longwave_imagery/ows`,
        layer: 'goes_longwave_imagery',
        attribution: `Satellite: ${noaa} GOES`,
        animated: true,
        opacity: 0.65,
        refreshMs: 300_000,
        note: 'Clouds are infrared satellite images, so they show day and night. Brighter clouds are colder and higher.',
    },
    {
        id: 'lightning',
        label: 'Lightning & warnings',
        kind: 'wms',
        service: `${nowCoast}/lightning_detection/ldn_lightning_strike_density/ows`,
        layer: 'ldn_lightning_strike_density',
        attribution: `Lightning and warnings: ${noaa}`,
        animated: true,
        opacity: 0.85,
        refreshMs: 300_000,
        note: 'Lightning is strikes in each 15 minutes. Shaded areas are current NWS warnings, watches and advisories.',
        overlay: {
            service: `${opengeo}/wwa/hazards/ows`,
            layer: 'hazards',
            attribution: `Warnings: ${noaa}`,
        },
    },
];

export function legendUrl(mode: MapMode) {
    return `${mode.service}?service=WMS&version=1.3.0&request=GetLegendGraphic&format=image/png&layer=${mode.layer}`;
}

// The time Dimension of the named layer. A capabilities document can list several
// layers, each with its own times, so the first Dimension in it isn't enough.
export function readLayerTimes(capabilities: string, layer: string): string[] {
    for (const part of capabilities.split('<Layer').slice(1)) {
        const name = /<Name>([^<]*)<\/Name>/.exec(part)?.[1]?.trim();
        if (name !== layer) {
            continue;
        }

        const times = /<Dimension[^>]*name="time"[^>]*>([^<]*)<\/Dimension>/.exec(part)?.[1]?.trim();
        return times ? times.split(',').map(time => time.trim()) : [];
    }

    return [];
}

export async function fetchRadarTimes(mode: MapMode, signal: AbortSignal): Promise<string[]> {
    const response = await fetch(`${mode.service}?service=WMS&version=1.3.0&request=GetCapabilities`, { signal });
    if (!response.ok) {
        throw new Error('The map times could not be loaded.');
    }

    const times = readLayerTimes(await response.text(), mode.layer);
    if (times.length === 0) {
        throw new Error('The map times could not be read.');
    }

    return times;
}

const webMercatorEdge = 20_037_508.342789244;

// An ArcGIS export URL for one 256-pixel Web Mercator tile. `version` changes the URL
// when a newer image should replace one the browser has cached.
export function arcgisTileUrl(service: string, layer: string, x: number, y: number, z: number, version: number) {
    const size = 2 * webMercatorEdge / 2 ** z;
    const xmin = -webMercatorEdge + x * size;
    const ymax = webMercatorEdge - y * size;
    const bbox = [xmin, ymax - size, xmin + size, ymax].map(value => value.toFixed(2)).join(',');

    return `${service}/export?bbox=${bbox}&bboxSR=3857&imageSR=3857&size=256,256&format=png32&transparent=true`
        + `&layers=show:${layer}&f=image&v=${version}`;
}

export interface TemperatureLegendStep {
    // The top of this colour's 1°F band.
    fahrenheit: number;
    image: string;
}

const temperatureLegendRange = { min: -20, max: 110 };

export function readTemperatureLegend(legend: unknown): TemperatureLegendStep[] {
    const layers = (legend as { layers?: { layerId: number; legend?: { label: string; imageData: string; contentType: string }[] }[] }).layers;
    const entries = layers?.find(layer => layer.layerId === temperatureLegendLayer)?.legend ?? [];

    return entries
        .map(entry => ({
            fahrenheit: Math.round(Number(entry.label.split(' - ').at(-1))),
            image: `data:${entry.contentType};base64,${entry.imageData}`,
        }))
        .filter(step => step.fahrenheit > temperatureLegendRange.min && step.fahrenheit <= temperatureLegendRange.max);
}

export async function fetchTemperatureLegend(signal: AbortSignal): Promise<TemperatureLegendStep[]> {
    const response = await fetch(`${temperatureService}/legend?f=json`, { signal });
    if (!response.ok) {
        throw new Error('The temperature scale could not be loaded.');
    }

    const steps = readTemperatureLegend(await response.json());
    if (steps.length === 0) {
        throw new Error('The temperature scale could not be read.');
    }

    return steps;
}
