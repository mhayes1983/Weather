import type { Unit } from '../temperature';

interface UnitToggleProps {
    unit: Unit;
    onChange: (unit: Unit) => void;
}

const units: Unit[] = ['F', 'C'];

function UnitToggle({ unit, onChange }: UnitToggleProps) {
    return (
        <div className="unit-toggle" role="group" aria-label="Temperature unit">
            {units.map(option =>
                <button
                    key={option}
                    type="button"
                    aria-pressed={unit === option}
                    onClick={() => onChange(option)}
                >
                    °{option}
                </button>
            )}
        </div>
    );
}

export default UnitToggle;
