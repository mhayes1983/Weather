// Anything below this would display as 0.0 in.
const smallestSnowIn = 0.05;

export function snowAmount(snowIn: number | null): string | null {
    return snowIn !== null && snowIn >= smallestSnowIn ? `${snowIn.toFixed(1)} in` : null;
}

/** The amount is liquid equivalent, so snow depth is shown separately. */
export function precipSummary(chancePercent: number | null, amountIn: number | null, snowIn: number | null): string | null {
    const snow = snowAmount(snowIn);
    const parts = [
        ...(chancePercent === null ? [] : [`${chancePercent}% chance`]),
        ...(amountIn !== null && amountIn > 0 ? [`${amountIn.toFixed(2)} in`] : []),
        ...(snow === null ? [] : [`${snow} snow`]),
    ];

    return parts.length > 0 ? `Precip ${parts.join(', ')}` : null;
}
