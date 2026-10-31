export const homeStation = 'home';

const storageKey = 'defaultStation';

export function loadDefaultStation(): string {
    try {
        return window.localStorage.getItem(storageKey) ?? homeStation;
    } catch {
        return homeStation;
    }
}

export function saveDefaultStation(id: string) {
    try {
        if (id === homeStation) {
            window.localStorage.removeItem(storageKey);
        } else {
            window.localStorage.setItem(storageKey, id);
        }
    } catch {
        // Without storage the choice still applies until the page reloads.
    }
}
