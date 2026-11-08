import type { ReactNode } from 'react';
import type { SortableLayout } from '../useSortableLayout';

interface SortablePanelProps {
    id: string;
    /** What the title button shows; defaults to the id. */
    heading?: ReactNode;
    className: string;
    layout: SortableLayout;
    children: ReactNode;
}

/** A card with a title that collapses it and a handle that moves it, for grids using `useSortableLayout`. */
function SortablePanel({ id, heading, className, layout, children }: SortablePanelProps) {
    const collapsed = layout.isCollapsed(id);
    const bodyId = `${layout.storageKey}-${id.replace(/\W+/g, '-')}`;

    return (
        <section className={[className, ...layout.classes(id)].join(' ')} data-panel-id={id}>
            <div className="panel-header">
                <h2>
                    <button
                        type="button"
                        className="panel-toggle"
                        aria-expanded={!collapsed}
                        aria-controls={bodyId}
                        onClick={() => layout.toggle(id)}
                    >
                        <span className="panel-chevron" aria-hidden="true">{collapsed ? '▸' : '▾'}</span>
                        <span>{heading ?? id}</span>
                    </button>
                </h2>
                <button
                    type="button"
                    className="panel-handle"
                    aria-label={`Move ${id}. Use the arrow keys or drag.`}
                    title="Drag to move"
                    {...layout.handleProps(id)}
                >
                    <span aria-hidden="true">⠿</span>
                </button>
            </div>
            {!collapsed && <div id={bodyId}>{children}</div>}
        </section>
    );
}

export default SortablePanel;
