import type { WeatherAlert } from '../api/alerts';
import { formatDate, formatTime } from '../localTime';

interface AlertPanelProps {
    alert: WeatherAlert;
    defaultOpen?: boolean;
}

// Alert times are shown as issued, in the local time of the alert's area.
function alertTime(time: string) {
    const date = formatDate(time.slice(0, 10), { weekday: 'short', month: 'numeric', day: 'numeric' });
    return `${date}, ${formatTime(time.slice(0, 16))}`;
}

function AlertPanel({ alert, defaultOpen = false }: AlertPanelProps) {
    return (
        <details className={`alert-panel alert-${alert.severity.toLowerCase()}`} open={defaultOpen}>
            <summary>
                <strong>{alert.event}</strong>
                {alert.ends && <span className="alert-until"> until {alertTime(alert.ends)}</span>}
            </summary>
            <div className="alert-body">
                {alert.headline && <p>{alert.headline}</p>}
                {alert.starts && <p>Starts {alertTime(alert.starts)}</p>}
                {alert.description && <p className="alert-text">{alert.description}</p>}
                {alert.instruction && <p className="alert-text"><strong>What to do:</strong> {alert.instruction}</p>}
            </div>
        </details>
    );
}

export default AlertPanel;
