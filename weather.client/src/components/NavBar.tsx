import type { MouseEvent } from 'react';
import type { Route } from '../useRoute';

interface NavBarProps {
    route: Route;
    onNavigate: (route: Route) => void;
}

const links: { route: Route; label: string }[] = [
    { route: '/', label: 'Station' },
    { route: '/forecast', label: 'Forecast Comparison' },
];

function NavBar({ route, onNavigate }: NavBarProps) {
    function handleClick(event: MouseEvent, target: Route) {
        // Modified clicks keep their normal behaviour, such as opening a new tab.
        if (!event.ctrlKey && !event.metaKey && !event.shiftKey) {
            event.preventDefault();
            onNavigate(target);
        }
    }

    return (
        <nav className="nav-bar" aria-label="Main">
            {links.map(link =>
                <a
                    key={link.route}
                    href={link.route}
                    aria-current={route === link.route ? 'page' : undefined}
                    onClick={event => handleClick(event, link.route)}
                >
                    {link.label}
                </a>
            )}
        </nav>
    );
}

export default NavBar;
