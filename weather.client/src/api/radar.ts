export const radarService = 'https://opengeo.ncep.noaa.gov/geoserver/conus/conus_bref_qcd/ows';
export const radarLayer = 'conus_bref_qcd';
export const radarLegend = `${radarService}?service=WMS&version=1.3.0&request=GetLegendGraphic&format=image/png&width=500&height=30&layer=${radarLayer}`;

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
