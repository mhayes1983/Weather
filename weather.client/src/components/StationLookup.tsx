import { useEffect, useRef, useState, type FormEvent } from 'react';
import { lookUpStation, type StationMatch } from '../api/station';
import { maxSavedStations } from '../savedStations';

interface StationLookupProps {
    ready: boolean;
    full: boolean;
    onFound: (match: StationMatch) => void;
}

function StationLookup({ ready, full, onFound }: StationLookupProps) {
    const controller = useRef<AbortController>(null);
    const latestOnFound = useRef(onFound);
    const [query, setQuery] = useState('');
    const [loading, setLoading] = useState(false);
    const [message, setMessage] = useState<{ text: string; failed: boolean }>();

    // A lookup can take a while, so its result uses the page as it is when it finishes.
    useEffect(() => {
        latestOnFound.current = onFound;
    });

    // Leaving the page cancels the lookup, so it can't change the station on another page.
    useEffect(() => () => controller.current?.abort(), []);

    function handleSubmit(event: FormEvent) {
        event.preventDefault();

        const trimmed = query.trim();
        if (!trimmed || !ready || full) {
            return;
        }

        controller.current?.abort();
        const request = new AbortController();
        controller.current = request;

        setLoading(true);
        setMessage(undefined);

        lookUpStation(trimmed, request.signal)
            .then(match => {
                if (request.signal.aborted) {
                    return;
                }

                latestOnFound.current(match);
                setQuery('');
                setMessage({
                    text: match.distanceMi === null
                        ? `Found ${match.name}.`
                        : `Found ${match.name}, ${match.distanceMi} miles away.`,
                    failed: false,
                });
            })
            .catch((reason: Error) => {
                if (!request.signal.aborted) {
                    setMessage({ text: reason.message, failed: true });
                }
            })
            .finally(() => setLoading(false));
    }

    return (
        <form className="station-lookup" onSubmit={handleSubmit}>
            <label>
                Find a station
                <input
                    type="text"
                    value={query}
                    placeholder="ZIP or station ID"
                    autoComplete="off"
                    spellCheck={false}
                    disabled={full}
                    onChange={event => setQuery(event.target.value)}
                />
            </label>
            <button type="submit" disabled={loading || !ready || full || !query.trim()}>{loading ? 'Finding…' : 'Add'}</button>
            {message &&
                <p className={`station-lookup-message${message.failed ? ' is-error' : ''}`} role={message.failed ? 'alert' : 'status'}>
                    {message.text}
                </p>}
            {full &&
                <p className="station-lookup-message" role="status">
                    Your list is full at {maxSavedStations} stations. Remove one to add another.
                </p>}
        </form>
    );
}

export default StationLookup;
