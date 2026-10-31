import { useState } from 'react';
import { applyTheme, loadTheme, saveTheme, type Theme } from '../theme';

const themes: { value: Theme; label: string }[] = [
    { value: 'auto', label: 'Auto' },
    { value: 'light', label: 'Light' },
    { value: 'dark', label: 'Dark' },
];

function ThemeToggle() {
    const [theme, setTheme] = useState(loadTheme);

    function handleChange(next: Theme) {
        setTheme(next);
        applyTheme(next);
        saveTheme(next);
    }

    return (
        <div className="unit-toggle theme-toggle" role="group" aria-label="Colour theme">
            {themes.map(option =>
                <button
                    key={option.value}
                    type="button"
                    aria-pressed={theme === option.value}
                    onClick={() => handleChange(option.value)}
                >
                    {option.label}
                </button>
            )}
        </div>
    );
}

export default ThemeToggle;
