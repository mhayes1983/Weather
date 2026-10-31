import type { StationSummary } from '../api/station';
import { homeStation } from '../defaultStation';

interface StationPickerProps {
    stations: StationSummary[];
    selected: string;
    defaultStation: string;
    onChange: (id: string) => void;
    onDefaultChange: (id: string) => void;
}

function StationPicker({ stations, selected, defaultStation, onChange, onDefaultChange }: StationPickerProps) {
    const isDefault = selected === defaultStation;

    // The home station is the fallback, so it can't be turned off.
    const label = !isDefault
        ? 'Make this my default station'
        : selected === homeStation
            ? 'This is your default station'
            : 'Remove as default station';

    return (
        <div className="station-picker">
            <label>
                Station
                <select value={selected} onChange={event => onChange(event.target.value)}>
                    {stations.map(station =>
                        <option key={station.id} value={station.id}>{station.name}</option>
                    )}
                </select>
            </label>
            <button
                type="button"
                className="station-star"
                aria-pressed={isDefault}
                aria-label={label}
                title={label}
                disabled={isDefault && selected === homeStation}
                onClick={() => onDefaultChange(isDefault ? homeStation : selected)}
            >
                <span aria-hidden="true">{isDefault ? '★' : '☆'}</span>
            </button>
        </div>
    );
}

export default StationPicker;
