import type { ReactNode } from 'react';

interface CollapsibleCardProps {
    title: string;
    open: boolean;
    onToggle: (open: boolean) => void;
    children: ReactNode;
}

function CollapsibleCard({ title, open, onToggle, children }: CollapsibleCardProps) {
    return (
        <details className="station-card collapsible-card" open={open} onToggle={event => onToggle(event.currentTarget.open)}>
            <summary><h2>{title}</h2></summary>
            {children}
        </details>
    );
}

export default CollapsibleCard;
