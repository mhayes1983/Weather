export const radarService = 'https://opengeo.ncep.noaa.gov/geoserver/conus/conus_bref_qcd/ows';
export const radarLayer = 'conus_bref_qcd';
export const radarLegend = `${radarService}?service=WMS&version=1.3.0&request=GetLegendGraphic&format=image/png&width=500&height=30&layer=${radarLayer}`;

const frameCount = 12;
const frameStep = 5;

// NOAA keeps about two hours of scans, roughly two minutes apart, so every fifth
// scan gives a loop that spans the whole range without loading sixty frames.
export function selectFrames(times: string[]): string[] {
    const frames: string[] = [];
    for (let index = times.length - 1; index >= 0 && frames.length < frameCount; index -= frameStep) {
        frames.unshift(times[index]);
    }

    return frames;
}

export async function fetchRadarTimes(signal: AbortSignal): Promise<string[]> {
    const response = await fetch(`${radarService}?service=WMS&version=1.3.0&request=GetCapabilities`, { signal });
    if (!response.ok) {
        throw new Error('The radar times could not be loaded.');
    }

    const capabilities = new DOMParser().parseFromString(await response.text(), 'text/xml');
    const times = capabilities.querySelector('Dimension[name="time"]')?.textContent?.trim();
    if (!times) {
        throw new Error('The radar times could not be read.');
    }

    return times.split(',');
}
