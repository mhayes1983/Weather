interface LegendProps {
    aboveAverageF: number;
    belowAverageF: number;
}

function Legend({ aboveAverageF, belowAverageF }: LegendProps) {
    return (
        <p className="legend">
            <span className="swatch anomaly-above" aria-hidden="true" /> {aboveAverageF}°F or more above average
            <span className="swatch anomaly-below" aria-hidden="true" /> {belowAverageF}°F or more below average
        </p>
    );
}

export default Legend;
