import type { StationReading } from '../api/station';
import { displayStationTemperature, type Unit } from '../temperature';
import { compass } from '../wind';

interface CurrentConditionsProps {
    reading: StationReading;
    unit: Unit;
}

interface Card {
    title: string;
    rows: [string, string][];
}

const missing = '—';

function measure(value: number | null, suffix: string, digits = 0): string {
    return value === null ? missing : `${value.toFixed(digits)}${suffix}`;
}

function CurrentConditions({ reading, unit }: CurrentConditionsProps) {
    function temperature(fahrenheit: number | null): string {
        return fahrenheit === null ? missing : `${displayStationTemperature(fahrenheit, unit).toFixed(1)}°${unit}`;
    }

    const cards: Card[] = [
        {
            title: 'Wind',
            rows: [
                ['Speed', measure(reading.windSpeedMph, ' mph', 1)],
                ['Gust', measure(reading.windGustMph, ' mph', 1)],
                ['Direction', compass(reading.windDirectionDegrees) ?? missing],
                ['Max gust today', measure(reading.maxDailyGustMph, ' mph', 1)],
            ],
        },
        {
            title: 'Rain',
            rows: [
                ['Rate', measure(reading.rainRateInPerHour, ' in/hr', 2)],
                ['Today', measure(reading.dailyRainIn, ' in', 2)],
                ['Event', measure(reading.eventRainIn, ' in', 2)],
                ['Week', measure(reading.weeklyRainIn, ' in', 2)],
                ['Month', measure(reading.monthlyRainIn, ' in', 2)],
                ['Year', measure(reading.yearlyRainIn, ' in', 2)],
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
    ];

    // Stations report different readings, so anything a station lacks is left out.
    const visibleCards = cards
        .map(card => ({ ...card, rows: card.rows.filter(([, value]) => value !== missing) }))
        .filter(card => card.rows.length > 0);

    return (
        <>
            <p className="station-temperature">
                <strong>{temperature(reading.temperatureF)}</strong>
                <span>Feels like {temperature(reading.feelsLikeF)}</span>
            </p>

            <div className="station-cards">
                {visibleCards.map(card =>
                    <section key={card.title} className="station-card">
                        <h2>{card.title}</h2>
                        <dl>
                            {card.rows.map(([label, value]) =>
                                <div key={label}>
                                    <dt>{label}</dt>
                                    <dd>{value}</dd>
                                </div>
                            )}
                        </dl>
                    </section>
                )}
            </div>
        </>
    );
}

export default CurrentConditions;
