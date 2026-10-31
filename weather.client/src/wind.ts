const compassPoints = ['N', 'NNE', 'NE', 'ENE', 'E', 'ESE', 'SE', 'SSE', 'S', 'SSW', 'SW', 'WSW', 'W', 'WNW', 'NW', 'NNW'];

export function compass(degrees: number | null): string | undefined {
    return degrees === null ? undefined : compassPoints[Math.round(degrees / 22.5) % 16];
}
