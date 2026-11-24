import { useEffect, useState } from 'react';

const fixedRoutes = ['/alerts', '/forecast', '/storm', '/storm/hourly', '/radar', '/daily', '/help', '/terms', '/privacy', '/setup'] as const;

export type Route = '/' | (typeof fixedRoutes)[number] | `/daily/${string}`;

// The Storm Forecast page was the Snow Forecast page, and links to it are still around.
const renamedRoutes: Record<string, Route> = { '/snow': '/storm', '/snow/hourly': '/storm/hourly' };

function currentRoute(): Route {
    const renamed = renamedRoutes[window.location.pathname];
    if (renamed) {
        window.history.replaceState(window.history.state, '', renamed + window.location.search);
    }

    const path = window.location.pathname;
    const fixed = fixedRoutes.find(route => route === path);

    // Setup only exists in the Android app.
    if (fixed && (fixed !== '/setup' || import.meta.env.MODE === 'app')) {
        return fixed;
    }

    return /^\/daily\/\d{4}-\d{2}-\d{2}$/.test(path) ? path as Route : '/';
}

export function forecastDate(route: Route): string | undefined {
    return route.startsWith('/daily/') ? route.slice('/daily/'.length) : undefined;
}

/** History state for a page opened from another, so its Back link can return to it unchanged. */
export interface RouteState {
    from: Route;
}

export function useRoute(): [Route, (route: Route, search?: string) => void] {
    const [route, setRoute] = useState(currentRoute);

    useEffect(() => {
        function handlePopState() {
            setRoute(currentRoute());
        }

        window.addEventListener('popstate', handlePopState);
        return () => window.removeEventListener('popstate', handlePopState);
    }, []);

    // A search string, such as the hourly storm page's source and town, travels in the address.
    function navigate(next: Route, search = '') {
        if (next !== route) {
            const state: RouteState = { from: route };
            window.history.pushState(state, '', next + search);
            setRoute(next);
            window.scrollTo(0, 0);
        }
    }

    return [route, navigate];
}
