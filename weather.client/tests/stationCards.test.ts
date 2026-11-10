import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { StationReading } from '../src/api/station.ts';
import { stationCards, stationWarnings } from '../src/stationCards.ts';

const bare: StationReading = {
    stationName: null,
    observedAt: '2026-11-08T12:00:00Z',
    temperatureF: 50,
    feelsLikeF: null,
    dewPointF: null,
    humidityPercent: null,
    indoorTemperatureF: null,
    indoorHumidityPercent: null,
    windSpeedMph: 3,
    windGustMph: null,
    windDirectionDegrees: null,
    maxDailyGustMph: null,
    pressureInHg: null,
    rainRateInPerHour: null,
    eventRainIn: null,
    dailyRainIn: null,
    weeklyRainIn: null,
    monthlyRainIn: null,
    yearlyRainIn: null,
    lastRain: null,
    uvIndex: null,
    solarRadiationWm2: null,
    batteryLow: null,
};

test('cards a station does not report are left out', () => {
    assert.deepEqual(stationCards(bare, 'F').map(card => card.title), ['Wind']);
});

test('averaged wind shows its direction', () => {
    const wind = stationCards({ ...bare, windSpeedAvg10mMph: 4.25, windDirectionAvg10mDegrees: 90 }, 'F')[0];
    assert.deepEqual(wind.rows.find(([label]) => label === '10-minute average'), ['10-minute average', '4.3 mph E']);
});

test('add-on sensors show only when fitted, in the chosen unit', () => {
    const cards = stationCards({
        ...bare,
        extraSensors: [{ channel: 2, temperatureF: 50, humidityPercent: null, batteryLow: false }],
        soilSensors: [{ channel: 1, temperatureF: null, moisturePercent: 33 }],
        leakSensors: [{ channel: 1, leak: false }],
        airQuality: { pm25: null, pm25Avg24h: null, indoorPm25: null, indoorAqi: 35, co2Ppm: 612, indoorCo2Ppm: null },
        lightning: { strikesToday: 0, strikesLastHour: null, lastStrike: null, lastDistanceMi: null },
    }, 'C');

    const rows = Object.fromEntries(cards.map(card => [card.title, card.rows]));
    assert.deepEqual(rows['Extra sensors'], [['Sensor 2', '10.0°C']]);
    assert.deepEqual(rows['Soil'], [['Sensor 1', '33% moisture']]);
    assert.deepEqual(rows['Leak sensors'], [['Sensor 1', 'Dry']]);
    assert.deepEqual(rows['Air quality'], [['Indoor AQI', '35'], ['CO₂', '612 ppm']]);
    assert.deepEqual(rows['Lightning'], [['Strikes today', '0']]);
});

test('leaks come before low batteries', () => {
    assert.deepEqual(stationWarnings(bare), []);
    assert.deepEqual(stationWarnings({
        ...bare,
        batteryLow: true,
        lowBatteries: ['rain gauge'],
        extraSensors: [{ channel: 3, temperatureF: 40, humidityPercent: null, batteryLow: true }],
        leakSensors: [{ channel: 2, leak: true }],
    }), [
        'Leak sensor 2 has detected water.',
        "The outdoor sensor's battery is low.",
        "The rain gauge's battery is low.",
        "The extra sensor 3's battery is low.",
    ]);
});

test('the high and low say which range they cover', () => {
    const trends = { highF: 62.1, highAt: null, lowF: 41.5, lowAt: '2026-11-08T12:00:00Z', pressureChangeInHg: null, pressureTrend: null };

    const day = stationCards(bare, 'F', trends, 'Today')[0];
    assert.equal(day.title, 'Temperature');
    assert.deepEqual(day.rows[0], ["Today's high", '62.1°F']);
    assert.match(day.rows[1][1], /^41\.5°F, \d{1,2}:\d{2}\s?[AP]M$/);

    const rolling = stationCards(bare, 'C', trends)[0];
    assert.deepEqual(rolling.rows[0], ['24-hour high', '16.7°C']);
});

test('a time from the day before says so', () => {
    const trends = { highF: 62.1, highAt: '2026-11-07T12:30:00Z', lowF: null, lowAt: null, pressureChangeInHg: null, pressureTrend: null };

    assert.match(stationCards(bare, 'F', trends)[0].rows[0][1], /^62\.1°F, .+ yesterday$/);
});

test('the pressure trend shows its change over three hours', () => {
    const row = (pressureChangeInHg: number, pressureTrend: 'Rising' | 'Steady' | 'FallingRapidly') =>
        stationCards({ ...bare, pressureInHg: 30 }, 'F', { highF: null, highAt: null, lowF: null, lowAt: null, pressureChangeInHg, pressureTrend })
            .find(card => card.title === 'Atmosphere')!.rows.find(([label]) => label === '3-hour trend');

    assert.deepEqual(row(0.03, 'Rising'), ['3-hour trend', 'Rising, +0.03 inHg']);
    assert.deepEqual(row(-0.08, 'FallingRapidly'), ['3-hour trend', 'Falling rapidly, −0.08 inHg']);
    assert.deepEqual(row(0, 'Steady'), ['3-hour trend', 'Steady']);
});
