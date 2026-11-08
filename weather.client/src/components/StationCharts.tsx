import type { StationHistoryPoint } from '../api/station';
import { displayStationTemperature, type Unit } from '../temperature';
import { useSortableLayout } from '../useSortableLayout';
import SortablePanel from './SortablePanel';
import StationChart, { type ChartSeries } from './StationChart';

interface StationChartsProps {
    history: StationHistoryPoint[];
    range: string;
    unit: Unit;
    maxGapMinutes: number;
}

interface ChartSpec {
    title: string;
    unit: string;
    series: ChartSeries[];
    zeroBased?: boolean;
    digits?: number;
}

function hasAny(values: (number | null)[]): boolean {
    return values.some(value => value !== null);
}

function StationCharts({ history, range, unit, maxGapMinutes }: StationChartsProps) {
    const times = history.map(point => point.time);

    function temperatures(pick: (point: StationHistoryPoint) => number | null | undefined) {
        return history.map(point => {
            const fahrenheit = pick(point);
            return fahrenheit === null || fahrenheit === undefined ? null : displayStationTemperature(fahrenheit, unit);
        });
    }

    const dewPoints = temperatures(point => point.dewPointF);
    const indoor = temperatures(point => point.indoorTemperatureF);
    const humidity = history.map(point => point.humidityPercent ?? null);
    const uv = history.map(point => point.uvIndex ?? null);
    const solar = history.map(point => point.solarRadiationWm2 ?? null);

    const charts: ChartSpec[] = [
        {
            title: 'Temperature',
            unit: `°${unit}`,
            digits: 1,
            series: [
                { label: 'Temperature', colour: 'var(--chart-1)', values: temperatures(point => point.temperatureF) },
                ...(hasAny(dewPoints) ? [{ label: 'Dew point', colour: 'var(--chart-2)', values: dewPoints }] : []),
            ],
        },
        {
            title: 'Wind',
            unit: 'mph',
            zeroBased: true,
            series: [
                { label: 'Speed', colour: 'var(--chart-1)', values: history.map(point => point.windSpeedMph) },
                { label: 'Gust', colour: 'var(--chart-2)', values: history.map(point => point.windGustMph) },
            ],
        },
        {
            title: 'Pressure',
            unit: 'inHg',
            digits: 2,
            series: [{ label: 'Pressure', colour: 'var(--chart-1)', values: history.map(point => point.pressureInHg) }],
        },
        {
            title: 'Rain rate',
            unit: 'in/hr',
            zeroBased: true,
            digits: 2,
            series: [{ label: 'Rain rate', colour: 'var(--chart-1)', values: history.map(point => point.rainRateInPerHour) }],
        },
        // Charts added for Ambient's extra readings are left out when a station doesn't report them.
        ...(hasAny(humidity) ? [{
            title: 'Humidity',
            unit: '%',
            series: [{ label: 'Humidity', colour: 'var(--chart-1)', values: humidity }],
        }] : []),
        ...(hasAny(uv) ? [{
            title: 'UV index',
            unit: 'UV',
            zeroBased: true,
            series: [{ label: 'UV index', colour: 'var(--chart-1)', values: uv }],
        }] : []),
        ...(hasAny(solar) ? [{
            title: 'Solar radiation',
            unit: 'W/m²',
            zeroBased: true,
            series: [{ label: 'Solar radiation', colour: 'var(--chart-1)', values: solar }],
        }] : []),
        ...(hasAny(indoor) ? [{
            title: 'Indoor temperature',
            unit: `°${unit}`,
            digits: 1,
            series: [{ label: 'Indoor', colour: 'var(--chart-1)', values: indoor }],
        }] : []),
    ];

    const byTitle = new Map(charts.map(chart => [chart.title, chart]));
    const layout = useSortableLayout('stationCharts', [...byTitle.keys()]);

    return (
        <>
            <div className="station-charts">
                {layout.order.map(title => {
                    const chart = byTitle.get(title);
                    return chart &&
                        <SortablePanel
                            key={title}
                            id={title}
                            heading={<>{title} <span className="chart-range">{range}</span></>}
                            className="station-chart"
                            layout={layout}
                        >
                            <StationChart
                                title={title}
                                range={range}
                                unit={chart.unit}
                                times={times}
                                maxGapMinutes={maxGapMinutes}
                                zeroBased={chart.zeroBased}
                                digits={chart.digits}
                                series={chart.series}
                            />
                        </SortablePanel>;
                })}
            </div>

            {layout.customised &&
                <p className="station-layout-reset">
                    <button type="button" onClick={layout.reset}>Reset chart layout</button>
                </p>}
        </>
    );
}

export default StationCharts;
