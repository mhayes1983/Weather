import type { StationSummary } from '../api/station';

interface StationPickerProps {
    stations: StationSummary[];
    selected: string;
    onChange: (id: string) => void;
}

function StationPicker({ stations, selected, onChange }: StationPickerProps) {
    return (
        <label className="station-picker">
            Station
            <select value={selected} onChange={event => onChange(event.target.value)}>
                {stations.map(station =>
                    <option key={station.id} value={station.id}>{station.name}</option>
                )}
            </select>
        </label>
    );
}

export default StationPicker;
