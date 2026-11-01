import type { StationSummary } from './api/station.ts';
import { homeStation } from './defaultStation.ts';

/** Stations setup can offer as the default. Without a key, only Open-Meteo stations still work. */
export function defaultStationChoices(chosen: StationSummary[], saved: StationSummary[], usesWeatherUnderground: boolean): StationSummary[] {
    const usable = saved.filter(station => usesWeatherUnderground || station.id.startsWith('om-'));
    return [...chosen, ...usable.filter(station => !chosen.some(other => other.id === station.id))];
}

/** Saving setup keeps the default, unless the station has just been left out. */
export function defaultAfterSetup(current: string, choices: StationSummary[]): string {
    return choices.some(station => station.id === current) ? current : homeStation;
}
