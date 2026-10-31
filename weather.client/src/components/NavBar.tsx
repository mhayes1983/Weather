import RouteLink from './RouteLink';
import type { Route } from '../useRoute';

interface NavBarProps {
    route: Route;
    onNavigate: (route: Route) => void;
}

const links: { route: Route; label: string }[] = [
    { route: '/', label: 'Station' },
    { route: '/daily', label: 'Daily Forecast' },
    { route: '/forecast', label: 'Forecast Comparison' },
];

function NavBar({ route, onNavigate }: NavBarProps) {
    return (
        <nav className="nav-bar" aria-label="Main">
            {links.map(link =>
                <RouteLink
                    key={link.route}
                    route={link.route}
                    onNavigate={onNavigate}
                    aria-current={route === link.route || route.startsWith(`${link.route}/`) ? 'page' : undefined}
                >
                    {link.label}
                </RouteLink>
            )}
        </nav>
    );
}

export default NavBar;
