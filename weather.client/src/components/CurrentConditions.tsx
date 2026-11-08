import type { CurrentSky, StationReading } from '../api/station';
import { stationCards, stationTemperature } from '../stationCards';
import type { Unit } from '../temperature';
import { useSortableLayout } from '../useSortableLayout';
import { weatherIcon } from '../weatherIcons';
import SortablePanel from './SortablePanel';

interface CurrentConditionsProps {
    reading: StationReading;
    sky: CurrentSky | null;
    unit: Unit;
}

function CurrentConditions({ reading, sky, unit }: CurrentConditionsProps) {
    // Stations report different readings, so anything a station lacks is left out.
    const cards = new Map(stationCards(reading, unit).map(card => [card.title, card]));
    const layout = useSortableLayout('stationCards', [...cards.keys()]);

    return (
        <>
            <p className="station-temperature">
                <strong>{stationTemperature(reading.temperatureF, unit)}</strong>
                {sky && <span className="station-sky"><span aria-hidden="true">{weatherIcon(sky.icon)}</span> {sky.conditions}</span>}
                <span>Feels like {stationTemperature(reading.feelsLikeF, unit)}</span>
            </p>

            <div className="station-cards">
                {layout.order.map(title =>
                    <SortablePanel key={title} id={title} className="station-card" layout={layout}>
                        <dl>
                            {cards.get(title)?.rows.map(([label, value]) =>
                                <div key={label}>
                                    <dt>{label}</dt>
                                    <dd>{value}</dd>
                                </div>
                            )}
                        </dl>
                    </SortablePanel>
                )}
            </div>

            {layout.customised &&
                <p className="station-layout-reset">
                    <button type="button" onClick={layout.reset}>Reset card layout</button>
                </p>}
        </>
    );
}

export default CurrentConditions;
