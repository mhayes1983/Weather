import { useRef, useState, type KeyboardEvent, type PointerEvent } from 'react';
import {
    defaultLayout,
    loadStationLayout,
    moveCard,
    orderCards,
    saveStationLayout,
    stepCard,
    toggleCollapsed,
    type StationLayout,
} from './stationLayout';

interface PanelDrag {
    id: string;
    startX: number;
    startY: number;
    moved: boolean;
    /** The panel it would land before, null for the end, or undefined where it wouldn't move. */
    dropBefore: string | null | undefined;
}

const dragThreshold = 5;

// Panels fill rows left to right, so a point is before a panel when it's above that panel's row,
// or in its row and left of its middle.
function findDropTarget(container: HTMLElement, id: string, x: number, y: number): string | null | undefined {
    const panels = [...container.querySelectorAll<HTMLElement>(':scope > [data-panel-id]')];
    const index = panels.findIndex(panel => panel.dataset.panelId === id);
    const target = panels
        .filter((_, other) => other !== index)
        .find(panel => {
            const { left, top, bottom, width } = panel.getBoundingClientRect();
            return y < top || (y <= bottom && x < left + width / 2);
        });

    const before = target?.dataset.panelId ?? null;
    const next = panels[index + 1]?.dataset.panelId ?? null;
    return before === next ? undefined : before;
}

/** Order, collapse and drag state for a grid of panels, saved in localStorage under `storageKey`. */
export function useSortableLayout(storageKey: string, ids: string[]) {
    const [layout, setLayout] = useState<StationLayout>(() => loadStationLayout(storageKey));
    const [drag, setDragState] = useState<PanelDrag>();
    // Pointer events can arrive faster than React re-renders, so handlers read the latest drag from here.
    const dragRef = useRef<PanelDrag>(undefined);

    const order = orderCards(ids, id => id, layout.order);

    function setDrag(next: PanelDrag | undefined) {
        dragRef.current = next;
        setDragState(next);
    }

    function update(next: StationLayout) {
        setLayout(saveStationLayout(storageKey, next));
    }

    function handleKeyDown(event: KeyboardEvent<HTMLElement>, id: string) {
        const step = event.key === 'ArrowLeft' || event.key === 'ArrowUp' ? -1
            : event.key === 'ArrowRight' || event.key === 'ArrowDown' ? 1
                : 0;
        if (step !== 0) {
            event.preventDefault();
            update(stepCard(layout, order, id, step));
        }
    }

    return {
        storageKey,
        order,
        customised: layout.order.length > 0 || layout.collapsed.length > 0,
        reset: () => update(defaultLayout),
        isCollapsed: (id: string) => layout.collapsed.includes(id),
        toggle: (id: string) => update(toggleCollapsed(layout, id)),

        classes(id: string) {
            const classes = layout.collapsed.includes(id) ? ['panel-collapsed'] : [];
            if (drag?.moved) {
                if (id === drag.id) {
                    classes.push('panel-dragging');
                } else if (id === drag.dropBefore) {
                    classes.push('panel-drop-before');
                } else if (drag.dropBefore === null && id === order[order.length - 1]) {
                    classes.push('panel-drop-after');
                }
            }
            return classes;
        },

        handleProps(id: string) {
            return {
                onPointerDown(event: PointerEvent<HTMLElement>) {
                    if (event.button === 0) {
                        event.currentTarget.setPointerCapture(event.pointerId);
                        setDrag({ id, startX: event.clientX, startY: event.clientY, moved: false, dropBefore: undefined });
                    }
                },
                onPointerMove(event: PointerEvent<HTMLElement>) {
                    const current = dragRef.current;
                    // The grid is the dragged panel's parent, so callers need no ref of their own.
                    const container = event.currentTarget.closest<HTMLElement>('[data-panel-id]')?.parentElement;
                    if (!current || !container) {
                        return;
                    }

                    const distance = Math.hypot(event.clientX - current.startX, event.clientY - current.startY);
                    if (current.moved || distance >= dragThreshold) {
                        setDrag({ ...current, moved: true, dropBefore: findDropTarget(container, current.id, event.clientX, event.clientY) });
                    }
                },
                onPointerUp() {
                    const current = dragRef.current;
                    if (current && current.dropBefore !== undefined) {
                        update(moveCard(layout, order, current.id, current.dropBefore));
                    }
                    setDrag(undefined);
                },
                onPointerCancel: () => setDrag(undefined),
                onKeyDown: (event: KeyboardEvent<HTMLElement>) => handleKeyDown(event, id),
            };
        },
    };
}

export type SortableLayout = ReturnType<typeof useSortableLayout>;
