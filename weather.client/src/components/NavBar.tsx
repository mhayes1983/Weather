import RouteLink from './RouteLink';
import ThemeToggle from './ThemeToggle';
import type { Route } from '../useRoute';

interface NavBarProps {
    route: Route;
    onNavigate: (route: Route) => void;
}

function isCurrent(link: Route, route: Route) {
    return route === link || route.startsWith(`${link}/`) || (link === '/' && route === '/alerts');
}

const links: { route: Route; label: string }[] = [
    { route: '/', label: 'Station' },
    { route: '/daily', label: 'Daily Forecast' },
    { route: '/radar', label: 'Radar' },
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
                    aria-current={isCurrent(link.route, route) ? 'page' : undefined}
                >
                    {link.label}
                </RouteLink>
            )}
            <ThemeToggle />
        </nav>
    );
}

export default NavBar;
