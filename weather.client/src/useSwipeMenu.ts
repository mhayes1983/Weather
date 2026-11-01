import { useEffect, useRef } from 'react';
import { swipeAction } from './swipe';

// The same width as the max-width in App.css where the menu button appears.
const narrowScreen = '(max-width: 720px)';

// These use sideways drags themselves.
const draggable = '.leaflet-container, .column-header, input, select, textarea';

function usesSidewaysDrag(target: EventTarget | null) {
    if (!(target instanceof Element)) {
        return false;
    }

    if (target.closest(draggable)) {
        return true;
    }

    for (let element: Element | null = target; element; element = element.parentElement) {
        const overflow = window.getComputedStyle(element).overflowX;
        if ((overflow === 'auto' || overflow === 'scroll') && element.scrollWidth > element.clientWidth) {
            return true;
        }
    }

    return false;
}

export function useSwipeMenu(open: boolean, onChange: (open: boolean) => void) {
    const start = useRef<{ x: number; y: number }>(null);

    useEffect(() => {
        function handleStart(event: TouchEvent) {
            const touch = event.touches[0];
            start.current = event.touches.length === 1
                && window.matchMedia(narrowScreen).matches
                && !usesSidewaysDrag(event.target)
                ? { x: touch.clientX, y: touch.clientY }
                : null;
        }

        function handleEnd(event: TouchEvent) {
            const touch = event.changedTouches[0];
            if (!start.current) {
                return;
            }

            const action = swipeAction(touch.clientX - start.current.x, touch.clientY - start.current.y, open);
            start.current = null;

            if (action) {
                onChange(action === 'open');
            }
        }

        function handleCancel() {
            start.current = null;
        }

        document.addEventListener('touchstart', handleStart, { passive: true });
        document.addEventListener('touchend', handleEnd, { passive: true });
        document.addEventListener('touchcancel', handleCancel, { passive: true });

        return () => {
            document.removeEventListener('touchstart', handleStart);
            document.removeEventListener('touchend', handleEnd);
            document.removeEventListener('touchcancel', handleCancel);
        };
    }, [open, onChange]);
}
