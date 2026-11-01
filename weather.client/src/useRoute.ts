import { useEffect, useState } from 'react';

const fixedRoutes = ['/alerts', '/forecast', '/radar', '/daily', '/help', '/terms', '/privacy'] as const;

export type Route = '/' | (typeof fixedRoutes)[number] | `/daily/${string}`;

function currentRoute(): Route {
    const path = window.location.pathname;
    const fixed = fixedRoutes.find(route => route === path);
    if (fixed) {
        return fixed;
    }

    return /^\/daily\/\d{4}-\d{2}-\d{2}$/.test(path) ? path as Route : '/';
}

export function forecastDate(route: Route): string | undefined {
    return route.startsWith('/daily/') ? route.slice('/daily/'.length) : undefined;
}

export function useRoute(): [Route, (route: Route) => void] {
    const [route, setRoute] = useState(currentRoute);

    useEffect(() => {
        function handlePopState() {
            setRoute(currentRoute());
        }

        window.addEventListener('popstate', handlePopState);
        return () => window.removeEventListener('popstate', handlePopState);
    }, []);

    function navigate(next: Route) {
        if (next !== route) {
            window.history.pushState(null, '', next);
            setRoute(next);
            window.scrollTo(0, 0);
        }
    }

    return [route, navigate];
}
