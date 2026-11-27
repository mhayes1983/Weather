import type { StationSummary } from './api/station.ts';

/** An Ambient Weather device's settings in the app, by the ID the app gives it. */
export interface AmbientStationSetting {
    deviceId: string;
    name: string | null;
    zip: string | null;
    hidden: boolean;
    home: boolean;
}

export interface AmbientDeviceChoice {
    id: string;
    /** Its name on the Ambient Weather account, if it has one. */
    name: string | null;
}

export const homeStationName = 'Home station';

/** Each device found, with its saved settings or the defaults. */
export function ambientSettings(devices: AmbientDeviceChoice[], saved: AmbientStationSetting[]): AmbientStationSetting[] {
    const settings = devices.map(device =>
        saved.find(setting => setting.deviceId.toLowerCase() === device.id.toLowerCase())
            ?? { deviceId: device.id, name: null, zip: null, hidden: false, home: false });

    // Without a choice, home is the first device, as the app does.
    const home = settings.find(setting => setting.home) ?? settings.at(0);
    return settings.map(setting => ({ ...setting, home: setting === home }));
}

/** The name a device will show: its setup name, else its Ambient name, else the app's. */
export function ambientName(setting: AmbientStationSetting, device: AmbientDeviceChoice | undefined): string {
    const named = setting.name?.trim();
    if (named) {
        return named;
    }

    return setting.home ? homeStationName : device?.name ?? 'Ambient station';
}

/** Stations setup can offer as the default: shown devices other than home, by their station ID. */
export function ambientDefaultChoices(devices: AmbientDeviceChoice[], settings: AmbientStationSetting[]): StationSummary[] {
    return settings
        .filter(setting => !setting.home && !setting.hidden)
        .map(setting => ({ id: setting.deviceId, name: ambientName(setting, devices.find(device => device.id === setting.deviceId)) }));
}

/** The home option's label in the default station list. */
export function homeChoiceName(devices: AmbientDeviceChoice[] | undefined, settings: AmbientStationSetting[]): string {
    const home = settings.find(setting => setting.home);
    return home ? ambientName(home, devices?.find(device => device.id === home.deviceId)) : homeStationName;
}
