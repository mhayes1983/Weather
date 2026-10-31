import AlertPanel from './AlertPanel';
import RouteLink from './RouteLink';
import { useAlerts } from '../useAlerts';
import type { Route } from '../useRoute';

const maxShown = 3;

interface StationAlertsProps {
    station: string;
    onNavigate: (route: Route) => void;
}

function StationAlerts({ station, onNavigate }: StationAlertsProps) {
    const { report, failed } = useAlerts(station);
    const alerts = report?.alerts ?? [];

    if (failed) {
        return <p className="alerts-unavailable">Alerts unavailable. There may be active alerts for this location.</p>;
    }

    if (alerts.length === 0) {
        return null;
    }

    return (
        <section className="station-alerts" aria-label="Weather alerts">
            {alerts.slice(0, maxShown).map(alert => <AlertPanel key={alert.id} alert={alert} />)}
            <RouteLink route="/alerts" onNavigate={onNavigate}>
                View all alerts ({alerts.length})
            </RouteLink>
        </section>
    );
}

export default StationAlerts;
