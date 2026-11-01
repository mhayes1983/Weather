import { useEffect, useRef, type MouseEvent, type ReactNode } from 'react';
import { CloseIcon } from './icons';
import RouteLink from './RouteLink';
import { isCurrent, legalLinks, navLinks } from '../navLinks';
import type { Route } from '../useRoute';

interface NavDrawerProps {
    open: boolean;
    onClose: () => void;
    route: Route;
    onNavigate: (route: Route) => void;
    children: ReactNode;
}

// One pixel above the max-width in App.css where the top bar's links return.
const wideScreen = '(min-width: 721px)';

function NavDrawer({ open, onClose, route, onNavigate, children }: NavDrawerProps) {
    const dialog = useRef<HTMLDialogElement>(null);

    useEffect(() => {
        const element = dialog.current;
        if (open && !element?.open) {
            element?.showModal();
        } else if (!open && element?.open) {
            element.close();
        }
    }, [open]);

    // CSS hides the drawer on wide screens, where it would leave the page inert.
    useEffect(() => {
        if (!open) {
            return;
        }

        const query = window.matchMedia(wideScreen);
        function handleChange(event: MediaQueryListEvent) {
            if (event.matches) {
                onClose();
            }
        }

        query.addEventListener('change', handleChange);
        return () => query.removeEventListener('change', handleChange);
    }, [open, onClose]);

    function handleNavigate(next: Route) {
        onClose();
        onNavigate(next);
    }

    // Browsers deliver this event later, by which time the drawer may have reopened.
    function handleClosed() {
        if (!dialog.current?.open) {
            onClose();
        }
    }

    // The contents fill the dialog, so only the backdrop reaches it directly.
    function handleClick(event: MouseEvent) {
        if (event.target === event.currentTarget) {
            onClose();
        }
    }

    return (
        <dialog ref={dialog} className="nav-drawer" aria-label="Menu" onClose={handleClosed} onClick={handleClick}>
            <div className="nav-drawer-body">
                <div className="nav-drawer-header">
                    <span className="nav-name">Weather</span>
                    <button type="button" className="icon-button" aria-label="Close menu" onClick={onClose}>
                        <CloseIcon />
                    </button>
                </div>

                <nav className="nav-drawer-links" aria-label="Main">
                    {navLinks.map(link =>
                        <RouteLink
                            key={link.route}
                            route={link.route}
                            onNavigate={handleNavigate}
                            aria-current={isCurrent(link.route, route) ? 'page' : undefined}
                        >
                            {link.label}
                        </RouteLink>
                    )}
                </nav>

                {children}

                <nav className="nav-drawer-legal" aria-label="Legal">
                    {legalLinks.map(link =>
                        <RouteLink key={link.route} route={link.route} onNavigate={handleNavigate}>{link.label}</RouteLink>
                    )}
                </nav>
            </div>
        </dialog>
    );
}

export default NavDrawer;
