export interface ChartSeries {
    label: string;
    colour: string;
    values: (number | null)[];
}

interface StationChartProps {
    title: string;
    range: string;
    unit: string;
    times: string[];
    series: ChartSeries[];
    maxGapMinutes: number;
    zeroBased?: boolean;
    digits?: number;
}

const width = 600;
const height = 180;
const left = 44;
const right = 8;
const top = 8;
const bottom = 22;
const minuteMs = 60 * 1000;
const hourMs = 60 * minuteMs;

function StationChart({ title, range, unit, times, series, maxGapMinutes, zeroBased = false, digits = 0 }: StationChartProps) {
    const maxGapMs = maxGapMinutes * minuteMs;
    const timestamps = times.map(time => Date.parse(time));
    const values = series.flatMap(line => line.values).filter(value => value !== null);

    if (values.length === 0) {
        return (
            <section className="station-chart">
                <h2>{title} <span className="chart-range">{range}</span></h2>
                <p>This station does not report this.</p>
            </section>
        );
    }

    const start = timestamps[0];
    const end = timestamps[timestamps.length - 1];
    const smallest = zeroBased ? 0 : Math.min(...values);
    const largest = Math.max(...values);
    const padding = zeroBased ? 0 : (largest - smallest) * 0.1;
    const low = smallest - padding;
    const span = largest + padding - low || 1;

    function x(time: number) {
        return left + (end === start ? 0 : (time - start) / (end - start)) * (width - left - right);
    }

    function y(value: number) {
        return height - bottom - (value - low) / span * (height - top - bottom);
    }

    // A line breaks at missing readings and at gaps in the data.
    function path(line: (number | null)[]) {
        const commands: string[] = [];
        let drawing = false;

        line.forEach((value, index) => {
            if (value === null) {
                drawing = false;
                return;
            }

            const gap = index > 0 && timestamps[index] - timestamps[index - 1] > maxGapMs;
            const point = `${x(timestamps[index]).toFixed(1)} ${y(value).toFixed(1)}`;
            commands.push(`${drawing && !gap ? 'L' : `M${point} L`}${point}`);
            drawing = true;
        });

        return commands.join(' ');
    }

    const hourTicks: number[] = [];
    for (let tick = Math.ceil(start / (6 * hourMs)) * (6 * hourMs); tick <= end; tick += 6 * hourMs) {
        hourTicks.push(tick);
    }

    const valueTicks = [low, low + span / 2, low + span];
    const summary = series
        .map(line => `${line.label}: latest ${line.values.findLast(value => value !== null)?.toFixed(digits) ?? 'none'} ${unit}`)
        .join(', ');

    return (
        <section className="station-chart">
            <h2>{title} <span className="chart-range">{range}</span></h2>
            <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`${title}, ${range}. ${summary}`}>
                {valueTicks.map(tick =>
                    <g key={tick}>
                        <line className="chart-grid" x1={left} x2={width - right} y1={y(tick)} y2={y(tick)} />
                        <text className="chart-label" x={left - 6} y={y(tick) + 4} textAnchor="end">{tick.toFixed(digits)}</text>
                    </g>
                )}
                {hourTicks.map(tick =>
                    <text key={tick} className="chart-label" x={x(tick)} y={height - 6} textAnchor="middle">
                        {new Date(tick).toLocaleTimeString([], { hour: 'numeric' })}
                    </text>
                )}
                {series.map(line =>
                    <path key={line.label} className="chart-line" d={path(line.values)} style={{ stroke: line.colour }} />
                )}
            </svg>
            <p className="chart-legend">
                {series.map(line =>
                    <span key={line.label}><i style={{ background: line.colour }} />{line.label}</span>
                )}
                <span>{unit}</span>
            </p>
        </section>
    );
}

export default StationChart;
