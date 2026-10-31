import AlertPanel from '../components/AlertPanel';
import RouteLink from '../components/RouteLink';
import { useAlerts } from '../useAlerts';
import type { Route } from '../useRoute';

interface AlertsPageProps {
    station: string;
    onNavigate: (route: Route) => void;
}

function AlertsPage({ station, onNavigate }: AlertsPageProps) {
    const { report, failed } = useAlerts(station);
    const alerts = report?.alerts ?? [];

    return (
        <main>
            <p className="back-link">
                <RouteLink route="/" onNavigate={onNavigate}>← Station</RouteLink>
            </p>

            <header>
                <h1>Weather Alerts</h1>
                {report && <p className="location">{report.stationName}</p>}
            </header>

            {failed && <p className="error" role="alert">Alerts unavailable. There may be active alerts for this location.</p>}
            {!report && !failed && <p>Loading alerts…</p>}
            {report?.status === 'NeedsKey' && <p className="error" role="alert">Add your Weather Underground key to see this station's alerts.</p>}
            {report?.status === 'Ok' && alerts.length === 0 && <p>No active alerts for this location.</p>}

            {alerts.length > 0 &&
                <div className="station-alerts">
                    {alerts.map(alert => <AlertPanel key={alert.id} alert={alert} defaultOpen />)}
                </div>}

            <footer className="attribution">
                Alerts from the <a href="https://www.weather.gov/">National Weather Service</a>
            </footer>
        </main>
    );
}

export default AlertsPage;
