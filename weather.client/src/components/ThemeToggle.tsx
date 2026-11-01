import type { Theme } from '../theme';

interface ThemeToggleProps {
    theme: Theme;
    onChange: (theme: Theme) => void;
}

const themes: { value: Theme; label: string }[] = [
    { value: 'auto', label: 'Auto' },
    { value: 'light', label: 'Light' },
    { value: 'dark', label: 'Dark' },
];

function ThemeToggle({ theme, onChange }: ThemeToggleProps) {
    return (
        <div className="unit-toggle theme-toggle" role="group" aria-label="Colour theme">
            {themes.map(option =>
                <button
                    key={option.value}
                    type="button"
                    aria-pressed={theme === option.value}
                    onClick={() => onChange(option.value)}
                >
                    {option.label}
                </button>
            )}
        </div>
    );
}

export default ThemeToggle;
