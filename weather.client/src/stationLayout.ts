/**
 * The station page's card and chart order and which ones are collapsed, saved in localStorage per grid.
 * Cards are keyed by title, so one layout serves every station. Cards a station doesn't have are skipped,
 * and cards missing from a saved order (such as a newly fitted sensor) go after the saved ones.
 */
export interface StationLayout {
    order: string[];
    collapsed: string[];
}

export const defaultLayout: StationLayout = { order: [], collapsed: [] };

function strings(value: unknown): string[] {
    return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
}

export function loadStationLayout(storageKey: string): StationLayout {
    try {
        const saved = JSON.parse(window.localStorage.getItem(storageKey) ?? 'null') as Partial<StationLayout> | null;
        return { order: strings(saved?.order), collapsed: strings(saved?.collapsed) };
    } catch {
        return defaultLayout;
    }
}

export function saveStationLayout(storageKey: string, layout: StationLayout): StationLayout {
    try {
        if (layout.order.length === 0 && layout.collapsed.length === 0) {
            window.localStorage.removeItem(storageKey);
        } else {
            window.localStorage.setItem(storageKey, JSON.stringify(layout));
        }
    } catch {
        // Without storage the layout still applies until the page reloads.
    }

    return layout;
}

/** Puts items in the saved order, keeping the default order for any the saved order doesn't name. */
export function orderCards<T>(items: T[], key: (item: T) => string, order: string[]): T[] {
    const rank = (item: T) => {
        const index = order.indexOf(key(item));
        return index === -1 ? order.length : index;
    };

    return items
        .map((item, index) => ({ item, index }))
        .sort((a, b) => rank(a.item) - rank(b.item) || a.index - b.index)
        .map(({ item }) => item);
}

/**
 * Moves a card before another, or to the end when `before` is null. `shown` is the order on screen,
 * which becomes the saved order so the cards stay where the user put them.
 */
export function moveCard(layout: StationLayout, shown: string[], id: string, before: string | null): StationLayout {
    const others = shown.filter(title => title !== id);
    const index = before === null ? others.length : others.indexOf(before);
    if (index === -1) {
        return layout;
    }

    others.splice(index, 0, id);
    const hidden = layout.order.filter(title => !shown.includes(title));
    return { ...layout, order: [...others, ...hidden] };
}

/** Moves a card one place earlier or later, for keyboard users. */
export function stepCard(layout: StationLayout, shown: string[], id: string, step: -1 | 1): StationLayout {
    const index = shown.indexOf(id);
    const target = index + step;
    if (index === -1 || target < 0 || target >= shown.length) {
        return layout;
    }

    const before = step === -1 ? shown[target] : (shown[target + 1] ?? null);
    return moveCard(layout, shown, id, before);
}

export function toggleCollapsed(layout: StationLayout, id: string): StationLayout {
    return {
        ...layout,
        collapsed: layout.collapsed.includes(id)
            ? layout.collapsed.filter(title => title !== id)
            : [...layout.collapsed, id],
    };
}
