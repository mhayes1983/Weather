import type { StationReading } from './api/station.ts';
import { displayStationTemperature, type Unit } from './temperature.ts';
import { compass } from './wind.ts';

export interface StationCard {
    title: string;
    rows: [string, string][];
}

const missing = '—';

function measure(value: number | null | undefined, suffix: string, digits = 0): string {
    return value === null || value === undefined ? missing : `${value.toFixed(digits)}${suffix}`;
}

export function stationTemperature(fahrenheit: number | null | undefined, unit: Unit): string {
    return fahrenheit === null || fahrenheit === undefined
        ? missing
        : `${displayStationTemperature(fahrenheit, unit).toFixed(1)}°${unit}`;
}

function windWith(speed: number | null | undefined, direction: number | null | undefined): string {
    if (speed === null || speed === undefined) {
        return missing;
    }

    const point = compass(direction ?? null);
    return point ? `${speed.toFixed(1)} mph ${point}` : `${speed.toFixed(1)} mph`;
}

function joined(...parts: string[]): string {
    const present = parts.filter(part => part !== missing);
    return present.length > 0 ? present.join(', ') : missing;
}

/** The station's reading as cards, leaving out anything the station doesn't report. */
export function stationCards(reading: StationReading, unit: Unit): StationCard[] {
    const temperature = (fahrenheit: number | null | undefined) => stationTemperature(fahrenheit, unit);
    const lightning = reading.lightning;
    const air = reading.airQuality;

    const cards: StationCard[] = [
        {
            title: 'Wind',
            rows: [
                ['Speed', measure(reading.windSpeedMph, ' mph', 1)],
                ['Gust', measure(reading.windGustMph, ' mph', 1)],
                ['Direction', compass(reading.windDirectionDegrees) ?? missing],
                ['Gust direction', compass(reading.windGustDirectionDegrees ?? null) ?? missing],
                ['2-minute average', windWith(reading.windSpeedAvg2mMph, reading.windDirectionAvg2mDegrees)],
                ['10-minute average', windWith(reading.windSpeedAvg10mMph, reading.windDirectionAvg10mDegrees)],
                ['Max gust today', measure(reading.maxDailyGustMph, ' mph', 1)],
            ],
        },
        {
            title: 'Rain',
            rows: [
                ['Rate', measure(reading.rainRateInPerHour, ' in/hr', 2)],
                ['Today', measure(reading.dailyRainIn, ' in', 2)],
                ['Last 24 hours', measure(reading.rain24HourIn, ' in', 2)],
                ['Event', measure(reading.eventRainIn, ' in', 2)],
                ['Week', measure(reading.weeklyRainIn, ' in', 2)],
                ['Month', measure(reading.monthlyRainIn, ' in', 2)],
                ['Year', measure(reading.yearlyRainIn, ' in', 2)],
                ['Since installed', measure(reading.totalRainIn, ' in', 2)],
                ['Last rain', reading.lastRain ? new Date(reading.lastRain).toLocaleDateString() : missing],
            ],
        },
        {
            title: 'Atmosphere',
            rows: [
                ['Humidity', measure(reading.humidityPercent, '%')],
                ['Dew point', temperature(reading.dewPointF)],
                ['Pressure', measure(reading.pressureInHg, ' inHg', 2)],
            ],
        },
        {
            title: 'Sun',
            rows: [
                ['UV index', measure(reading.uvIndex, '')],
                ['Solar radiation', measure(reading.solarRadiationWm2, ' W/m²', 1)],
            ],
        },
        {
            title: 'Indoor',
            rows: [
                ['Temperature', temperature(reading.indoorTemperatureF)],
                ['Humidity', measure(reading.indoorHumidityPercent, '%')],
            ],
        },
        {
            title: 'Air quality',
            rows: [
                ['PM2.5', measure(air?.pm25, ' µg/m³', 1)],
                ['PM2.5, 24-hour average', measure(air?.pm25Avg24h, ' µg/m³', 1)],
                ['AQI', measure(air?.aqi, '')],
                ['Indoor PM2.5', measure(air?.indoorPm25, ' µg/m³', 1)],
                ['Indoor CO₂', measure(air?.indoorCo2Ppm, ' ppm')],
            ],
        },
        {
            title: 'Lightning',
            rows: [
                ['Strikes today', measure(lightning?.strikesToday, '')],
                ['Strikes in the last hour', measure(lightning?.strikesLastHour, '')],
                ['Last strike', lightning?.lastStrike
                    ? new Date(lightning.lastStrike).toLocaleString([], { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })
                    : missing],
                ['Last strike distance', measure(lightning?.lastDistanceMi, ' mi', 1)],
            ],
        },
        {
            title: 'Extra sensors',
            rows: (reading.extraSensors ?? []).map(sensor => [
                `Sensor ${sensor.channel}`,
                joined(temperature(sensor.temperatureF), measure(sensor.humidityPercent, '%')),
            ]),
        },
        {
            title: 'Soil',
            rows: (reading.soilSensors ?? []).map(sensor => [
                `Sensor ${sensor.channel}`,
                joined(temperature(sensor.temperatureF), measure(sensor.moisturePercent, '% moisture')),
            ]),
        },
        {
            title: 'Leak sensors',
            rows: (reading.leakSensors ?? []).map(sensor => [`Sensor ${sensor.channel}`, sensor.leak ? 'Leak' : 'Dry']),
        },
    ];

    return cards
        .map(card => ({ ...card, rows: card.rows.filter(([, value]) => value !== missing) }))
        .filter(card => card.rows.length > 0);
}

/** Warnings shown above the readings: leaks first, then low batteries. */
export function stationWarnings(reading: StationReading): string[] {
    const warnings = (reading.leakSensors ?? [])
        .filter(sensor => sensor.leak)
        .map(sensor => `Leak sensor ${sensor.channel} has detected water.`);

    const batteries = [
        ...(reading.batteryLow ? ['outdoor sensor'] : []),
        ...(reading.lowBatteries ?? []),
        ...(reading.extraSensors ?? []).filter(sensor => sensor.batteryLow).map(sensor => `extra sensor ${sensor.channel}`),
    ];

    for (const battery of batteries) {
        warnings.push(`The ${battery}'s battery is low.`);
    }

    return warnings;
}
