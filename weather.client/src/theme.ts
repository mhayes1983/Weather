export type Theme = 'auto' | 'light' | 'dark';

const storageKey = 'theme';

export function loadTheme(): Theme {
    try {
        const saved = window.localStorage.getItem(storageKey);
        return saved === 'light' || saved === 'dark' ? saved : 'auto';
    } catch {
        return 'auto';
    }
}

// Auto sets no attribute, so index.css follows the device's setting.
export function applyTheme(theme: Theme) {
    if (theme === 'auto') {
        delete document.documentElement.dataset.theme;
    } else {
        document.documentElement.dataset.theme = theme;
    }
}

export function saveTheme(theme: Theme) {
    try {
        window.localStorage.setItem(storageKey, theme);
    } catch {
        // Without storage the choice still applies until the page reloads.
    }
}
