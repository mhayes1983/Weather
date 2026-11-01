import { useEffect, useId, useState, type ReactNode } from 'react';
import { fetchStations, type StationMatch, type StationSummary } from '../api/station';
import { MapPinIcon } from './icons';
import StationLookup from './StationLookup';
import StationPicker from './StationPicker';
import { homeStation } from '../defaultStation';
import { maxSavedStations } from '../savedStations';

export interface StationControls {
    selected: string;
    onStationChange: (id: string) => void;
    defaultStation: string;
    onDefaultChange: (id: string) => void;
    savedStations: StationSummary[];
    onStationSaved: (station: StationSummary) => void;
    onStationRemoved: (id: string) => void;
}

interface StationHeaderProps extends StationControls {
    children: ReactNode;
}

function StationHeader({ selected, onStationChange, defaultStation, onDefaultChange, savedStations, onStationSaved, onStationRemoved, children }: StationHeaderProps) {
    const panelId = useId();
    const [open, setOpen] = useState(false);
    const [stationList, setStationList] = useState<StationSummary[]>();
    const stations = stationList ?? [];

    useEffect(() => {
        const controller = new AbortController();

        fetchStations(controller.signal)
            .then(setStationList)
            .catch(() => {
                // The page still works for the home station without the list.
                if (!controller.signal.aborted) {
                    setStationList([{ id: homeStation, name: 'Home station' }]);
                }
            });

        return () => controller.abort();
    }, []);

    function handleStationPicked(id: string) {
        setOpen(false);
        onStationChange(id);
    }

    // The panel stays open here so the lookup's result stays on screen.
    function handleStationFound(match: StationMatch) {
        if (!stations.some(station => station.id === match.id)) {
            onStationSaved({ id: match.id, name: match.name });
        }

        if (match.id !== selected) {
            onStationChange(match.id);
        }
    }

    const pickerStations = [
        ...stations,
        ...savedStations
            .filter(saved => !stations.some(station => station.id === saved.id))
            .map(saved => ({ id: saved.id, name: `${saved.name} (${saved.id})` })),
    ];

    // Without the list, a configured default still has to appear as the selected station.
    if (!pickerStations.some(station => station.id === selected)) {
        pickerStations.push({ id: selected, name: selected });
    }

    const canRemove = savedStations.some(saved => saved.id === selected) && !stations.some(station => station.id === selected);

    return (
        <>
            <header className="station-header">
                <div className="station-heading">{children}</div>
                <button type="button" className="station-change" aria-expanded={open} aria-controls={panelId} onClick={() => setOpen(!open)}>
                    <MapPinIcon />
                    Change station
                </button>
            </header>

            {/* Hidden rather than removed, so closing it doesn't cancel a lookup. */}
            <div id={panelId} className="station-panel" hidden={!open}>
                {stationList && pickerStations.length > 1 && <StationPicker stations={pickerStations} selected={selected} defaultStation={defaultStation} canRemove={canRemove} onChange={handleStationPicked} onDefaultChange={onDefaultChange} onRemove={() => onStationRemoved(selected)} />}
                <StationLookup ready={stationList !== undefined} full={savedStations.length >= maxSavedStations} onFound={handleStationFound} />
            </div>
        </>
    );
}

export default StationHeader;
