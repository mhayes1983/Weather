import { useRef, useState, type FormEvent } from 'react';
import { lookUpStation, type StationMatch } from '../api/station';

interface StationLookupProps {
    onFound: (match: StationMatch) => void;
}

function StationLookup({ onFound }: StationLookupProps) {
    const controller = useRef<AbortController>(null);
    const [query, setQuery] = useState('');
    const [loading, setLoading] = useState(false);
    const [message, setMessage] = useState<{ text: string; failed: boolean }>();

    function handleSubmit(event: FormEvent) {
        event.preventDefault();

        const trimmed = query.trim();
        if (!trimmed) {
            return;
        }

        controller.current?.abort();
        const request = new AbortController();
        controller.current = request;

        setLoading(true);
        setMessage(undefined);

        lookUpStation(trimmed, request.signal)
            .then(match => {
                onFound(match);
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
                    onChange={event => setQuery(event.target.value)}
                />
            </label>
            <button type="submit" disabled={loading || !query.trim()}>{loading ? 'Finding…' : 'Add'}</button>
            {message &&
                <p className={`station-lookup-message${message.failed ? ' is-error' : ''}`} role={message.failed ? 'alert' : 'status'}>
                    {message.text}
                </p>}
        </form>
    );
}

export default StationLookup;
