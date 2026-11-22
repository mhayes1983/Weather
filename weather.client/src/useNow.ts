import { useEffect, useState } from 'react';

/** The current time in milliseconds, updated every `intervalMs` so anything drawn from it keeps moving. */
export function useNow(intervalMs = 60_000): number {
    const [now, setNow] = useState(() => Date.now());

    useEffect(() => {
        const timer = window.setInterval(() => setNow(Date.now()), intervalMs);
        return () => window.clearInterval(timer);
    }, [intervalMs]);

    return now;
}
