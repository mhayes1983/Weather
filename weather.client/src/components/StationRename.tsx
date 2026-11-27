import { useState, type FormEvent } from 'react';
import { maxStationNameLength } from '../stationNames';

interface StationRenameProps {
    /** The name the station has without this browser's, which a reset goes back to. */
    stationName: string;
    renamed: string | undefined;
    onRename: (name: string | null) => void;
}

/** Names a station in this browser only, such as "Camp station". */
function StationRename({ stationName, renamed, onRename }: StationRenameProps) {
    const [editing, setEditing] = useState(false);
    const [name, setName] = useState('');

    function handleEdit() {
        setName(renamed ?? stationName);
        setEditing(true);
    }

    function handleSubmit(event: FormEvent) {
        event.preventDefault();
        onRename(name.trim() === stationName ? null : name);
        setEditing(false);
    }

    function handleReset() {
        onRename(null);
        setEditing(false);
    }

    if (!editing) {
        return (
            <div className="station-rename">
                <button type="button" onClick={handleEdit}>Rename</button>
                {renamed && <span className="station-rename-note">Named in this browser. Station name: {stationName}</span>}
            </div>
        );
    }

    return (
        <form className="station-rename" onSubmit={handleSubmit}>
            <label>
                Name
                <input type="text" value={name} maxLength={maxStationNameLength} autoComplete="off" autoFocus onChange={event => setName(event.target.value)} />
            </label>
            <button type="submit">Save</button>
            {renamed && <button type="button" onClick={handleReset}>Use station name</button>}
            <button type="button" onClick={() => setEditing(false)}>Cancel</button>
        </form>
    );
}

export default StationRename;
