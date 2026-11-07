/** A sentence about each data source, shown when hovering over its name in the forecast tables. */
const descriptions: Record<string, string> = {
    // Open-Meteo models
    ecmwf: "ECMWF IFS: the European Centre's global model, often rated the most accurate. About 15 days.",
    'ecmwf-aifs': "ECMWF AIFS: the European Centre's AI-based global model. About 15 days.",
    gfs: "GFS: the US National Weather Service's global model. 16 days.",
    nbm: 'NBM: the National Blend of Models, a statistical blend of many models that NWS forecasters start from. US only, about 11 days.',
    hrrr: "HRRR: NOAA's High-Resolution Rapid Refresh, updated hourly at 3 km. Best for storm timing over the next 2 days. US only.",
    gem: "GEM: Environment Canada's global model. About 10 days.",
    hrdps: "HRDPS: Environment Canada's 2.5 km high-resolution model. About 2 days, covering Canada and the northern US.",
    icon: "ICON: the German Weather Service's (DWD) global model. About 7 days.",
    ukmo: "UKMO: the UK Met Office's global model. About 7 days.",
    jma: "JMA: the Japan Meteorological Agency's global model. About 10 days.",
    meteofrance: "Météo-France ARPEGE: France's global model. About 4 days.",
    cma: "CMA GRAPES: the China Meteorological Administration's global model. About 5 days.",
    weathernext: "WeatherNext: Google DeepMind's AI ensemble. This is the average of its members, which smooths out extremes. About 14 days.",

    // Forecasters and services
    nws: 'National Weather Service: forecasts written by the local NWS office (Gray, ME for most of Maine). About 7 days.',
    wu: "The Weather Company's forecast, used by The Weather Channel and Weather Underground. 6 days.",
    weatherapi: "WeatherAPI.com's forecast. The free plan covers 3 days.",
    'ncm-coastal': "NewsCenter Maine (WCSH 6 and WLBZ 2) meteorologists' 10-day forecast for Portland.",
    'ncm-inland': "NewsCenter Maine (WCSH 6 and WLBZ 2) meteorologists' 10-day forecast for Bangor.",

    // Snow Forecast manual rows
    ncm: "NewsCenter Maine (WCSH 6 and WLBZ 2) meteorologists' snow map, typed in by hand.",
    wmtw: "WMTW News 8 meteorologists' snow map, typed in by hand.",
    wgme: "WGME CBS 13 meteorologists' snow map, typed in by hand.",
};

export function sourceDescription(id: string): string | undefined {
    return descriptions[id] ?? (id.startsWith('manual-') ? 'Typed in by hand.' : undefined);
}

/** Joins the description with any status text, such as an error or when the data was updated. */
export function sourceTitle(id: string, ...details: (string | null | undefined)[]): string | undefined {
    const parts = [sourceDescription(id), ...details].filter((part): part is string => !!part);
    return parts.length ? parts.join('\n') : undefined;
}
