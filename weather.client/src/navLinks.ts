import type { Route } from './useRoute';

export const navLinks: { route: Route; label: string }[] = [
    { route: '/', label: 'Station' },
    { route: '/daily', label: 'Daily Forecast' },
    { route: '/radar', label: 'Radar' },
    { route: '/forecast', label: 'Forecast Comparison' },
    { route: '/help', label: 'Help' },
];

export const legalLinks: { route: Route; label: string }[] = [
    { route: '/terms', label: 'Terms of Use' },
    { route: '/privacy', label: 'Privacy Policy' },
];

export function isCurrent(link: Route, route: Route) {
    return route === link || route.startsWith(`${link}/`) || (link === '/' && route === '/alerts');
}
