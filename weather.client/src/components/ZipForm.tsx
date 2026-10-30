import { useState, type FormEvent } from 'react';

interface ZipFormProps {
    initialZip: string;
    loading: boolean;
    onSubmit: (zip: string) => void;
}

function ZipForm({ initialZip, loading, onSubmit }: ZipFormProps) {
    const [value, setValue] = useState(initialZip);

    function handleSubmit(event: FormEvent) {
        event.preventDefault();
        onSubmit(value.trim());
    }

    return (
        <form className="zip-form" onSubmit={handleSubmit}>
            <label htmlFor="zip">ZIP code</label>
            <input
                id="zip"
                value={value}
                onChange={event => setValue(event.target.value)}
                inputMode="numeric"
                autoComplete="postal-code"
                maxLength={5}
            />
            <button type="submit" disabled={loading}>{loading ? 'Loading…' : 'Go'}</button>
        </form>
    );
}

export default ZipForm;
