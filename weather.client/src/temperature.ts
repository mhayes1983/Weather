export type Unit = 'F' | 'C';

export function displayTemperature(fahrenheit: number, unit: Unit): number {
    return unit === 'F' ? fahrenheit : Math.round((fahrenheit - 32) * 5 / 9);
}
