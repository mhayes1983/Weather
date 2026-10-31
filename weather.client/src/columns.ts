import type { ForecastColumn } from './api/forecast';

// Columns missing from the order stay in API order after the ones the user has placed.
export function orderColumns(columns: ForecastColumn[], order: string[]) {
    const position = (column: ForecastColumn) => {
        const index = order.indexOf(column.id);
        return index === -1 ? order.length : index;
    };
    return [...columns].sort((a, b) => position(a) - position(b));
}

// A null target moves the column to the end.
export function moveColumn(order: string[], id: string, before: string | null) {
    const moved = order.filter(other => other !== id);
    moved.splice(before === null ? moved.length : moved.indexOf(before), 0, id);
    return moved;
}
