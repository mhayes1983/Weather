export type Unit = 'F' | 'C';

const storageKey = 'unit';

export function loadUnit(): Unit {
    try {
        return window.localStorage.getItem(storageKey) === 'C' ? 'C' : 'F';
    } catch {
        return 'F';
    }
}

export function saveUnit(unit: Unit) {
    try {
        window.localStorage.setItem(storageKey, unit);
    } catch {
        // Without storage the choice still applies until the page reloads.
    }
}

export function displayTemperature(fahrenheit: number, unit: Unit): number {
    return unit === 'F' ? fahrenheit : Math.round((fahrenheit - 32) * 5 / 9);
}

// Station readings keep the sensor's one decimal place.
export function displayStationTemperature(fahrenheit: number, unit: Unit): number {
    const value = unit === 'F' ? fahrenheit : (fahrenheit - 32) * 5 / 9;
    return Math.round(value * 10) / 10;
}
