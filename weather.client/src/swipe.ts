export type SwipeAction = 'open' | 'close';

const minDistance = 60;

// A swipe must be mostly sideways, so scrolling the page never moves the menu.
const minSlope = 1.5;

export function swipeAction(deltaX: number, deltaY: number, menuOpen: boolean): SwipeAction | undefined {
    if (Math.abs(deltaX) < minDistance || Math.abs(deltaX) < Math.abs(deltaY) * minSlope) {
        return undefined;
    }

    if (menuOpen) {
        return deltaX < 0 ? 'close' : undefined;
    }

    return deltaX > 0 ? 'open' : undefined;
}
