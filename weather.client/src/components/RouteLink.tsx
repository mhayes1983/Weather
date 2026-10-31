import type { ComponentProps, MouseEvent } from 'react';
import type { Route } from '../useRoute';

interface RouteLinkProps extends Omit<ComponentProps<'a'>, 'href' | 'onClick'> {
    route: Route;
    onNavigate: (route: Route) => void;
}

function RouteLink({ route, onNavigate, ...props }: RouteLinkProps) {
    function handleClick(event: MouseEvent) {
        // Modified clicks keep their normal behaviour, such as opening a new tab.
        if (!event.ctrlKey && !event.metaKey && !event.shiftKey) {
            event.preventDefault();
            onNavigate(route);
        }
    }

    return <a {...props} href={route} onClick={handleClick} />;
}

export default RouteLink;
