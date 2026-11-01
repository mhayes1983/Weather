import RouteLink from './RouteLink';
import { legalLinks } from '../navLinks';
import type { Route } from '../useRoute';

interface SiteFooterProps {
    onNavigate: (route: Route) => void;
}

function SiteFooter({ onNavigate }: SiteFooterProps) {
    return (
        <footer className="site-footer">
            {legalLinks.map(link =>
                <RouteLink key={link.route} route={link.route} onNavigate={onNavigate}>{link.label}</RouteLink>
            )}
        </footer>
    );
}

export default SiteFooter;
