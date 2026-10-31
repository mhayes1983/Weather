import { useEffect, useState } from 'react';
import { fetchAlerts, type AlertReport } from './api/alerts';

const refreshMs = 60_000;

interface AlertState {
    report?: AlertReport;
    failed: boolean;
}

export function useAlerts(station: string): AlertState {
    const [state, setState] = useState<AlertState & { station?: string }>({ failed: false });

    useEffect(() => {
        const controller = new AbortController();

        function load() {
            fetchAlerts(station, controller.signal)
                .then(report => setState({ station, report, failed: report.status === 'Error' }))
                .catch(() => {
                    if (!controller.signal.aborted) {
                        setState({ station, failed: true });
                    }
                });
        }

        function loadIfVisible() {
            if (!document.hidden) {
                load();
            }
        }

        load();
        const timer = window.setInterval(loadIfVisible, refreshMs);
        document.addEventListener('visibilitychange', loadIfVisible);

        return () => {
            controller.abort();
            window.clearInterval(timer);
            document.removeEventListener('visibilitychange', loadIfVisible);
        };
    }, [station]);

    // Another station's alerts must never show under the selected one.
    return state.station === station ? state : { failed: false };
}
