import { useCallback, useState, type ReactNode } from 'react';
import { GearIcon, MenuIcon } from './icons';
import NavDrawer from './NavDrawer';
import RouteLink from './RouteLink';
import { appName } from '../appBridge';
import { isCurrent, navLinks } from '../navLinks';
import type { Route } from '../useRoute';
import { useSwipeMenu } from '../useSwipeMenu';

interface NavBarProps {
    route: Route;
    onNavigate: (route: Route) => void;
    settings: ReactNode;
}

function NavBar({ route, onNavigate, settings }: NavBarProps) {
    const [menuOpen, setMenuOpen] = useState(false);
    const [menuRoute, setMenuRoute] = useState(route);
    const closeMenu = useCallback(() => setMenuOpen(false), []);
    useSwipeMenu(menuOpen, setMenuOpen);

    // Going back doesn't pass through the menu's links, but it shouldn't leave the menu open.
    if (route !== menuRoute) {
        setMenuRoute(route);
        setMenuOpen(false);
    }

    return (
        <>
            <nav className="nav-bar" aria-label="Main">
                <button type="button" className="icon-button nav-menu-button" aria-label="Open menu" onClick={() => setMenuOpen(true)}>
                    <MenuIcon />
                </button>
                <span className="nav-name">{appName}</span>

                <div className="nav-links">
                    {navLinks.map(link =>
                        <RouteLink
                            key={link.route}
                            route={link.route}
                            onNavigate={onNavigate}
                            aria-current={isCurrent(link.route, route) ? 'page' : undefined}
                        >
                            {link.label}
                        </RouteLink>
                    )}
                </div>

                <button type="button" className="icon-button nav-settings-button" aria-label="Settings" popoverTarget="settings-popover">
                    <GearIcon />
                </button>
                <div id="settings-popover" className="settings-popover" popover="auto">
                    {settings}
                </div>
            </nav>

            <NavDrawer open={menuOpen} onClose={closeMenu} route={route} onNavigate={onNavigate}>
                {settings}
            </NavDrawer>
        </>
    );
}

export default NavBar;
