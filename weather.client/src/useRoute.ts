import { useEffect, useState } from 'react';

export type Route = '/' | '/forecast';

function currentRoute(): Route {
    return window.location.pathname === '/forecast' ? '/forecast' : '/';
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
        }
    }

    return [route, navigate];
}
