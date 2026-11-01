import ThemeToggle from './ThemeToggle';
import UnitToggle from './UnitToggle';
import type { Unit } from '../temperature';
import type { Theme } from '../theme';

interface SettingsPanelProps {
    theme: Theme;
    onThemeChange: (theme: Theme) => void;
    unit: Unit;
    onUnitChange: (unit: Unit) => void;
}

function SettingsPanel({ theme, onThemeChange, unit, onUnitChange }: SettingsPanelProps) {
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
        </section>
    );
}

export default SettingsPanel;
