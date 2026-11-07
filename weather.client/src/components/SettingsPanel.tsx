import RouteLink from './RouteLink';
import ThemeToggle from './ThemeToggle';
import UnitToggle from './UnitToggle';
import { currentSetup, isApp } from '../appBridge';
import type { Unit } from '../temperature';
import type { Theme } from '../theme';
import type { Route } from '../useRoute';

interface SettingsPanelProps {
    theme: Theme;
    onThemeChange: (theme: Theme) => void;
    unit: Unit;
    onUnitChange: (unit: Unit) => void;
    onNavigate: (route: Route) => void;
}

function SettingsPanel({ theme, onThemeChange, unit, onUnitChange, onNavigate }: SettingsPanelProps) {
    return (
        <section className="settings-panel" aria-label="Settings">
            <h2>Settings</h2>
            <div className="settings-row">
                <span>Theme</span>
                <ThemeToggle theme={theme} onChange={onThemeChange} />
            </div>
            <div className="settings-row">
                <span>Temperature</span>
                <UnitToggle unit={unit} onChange={onUnitChange} />
            </div>
            {isApp && <RouteLink route="/setup" onNavigate={onNavigate}>Data &amp; keys</RouteLink>}
            {isApp && currentSetup()?.appVersion && <p className="settings-version">Version {currentSetup()?.appVersion}</p>}
        </section>
    );
}

export default SettingsPanel;
