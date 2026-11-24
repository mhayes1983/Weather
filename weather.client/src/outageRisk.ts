/**
 * A rough guide to power outage risk for one town, from the averages of the sources shown. It's the usual
 * recipe for outages: heavy wet snow or ice weighing on limbs and lines, made worse by wind, or wind alone at
 * High Wind Warning strength. It isn't a forecast from any utility or the NWS.
 */
export interface OutageInputs {
    /** Average snow, in inches. */
    snowInches: number | null;
    /** Snow-to-liquid ratio from the sources with real snow, such as 8 for 8:1 (see `snowRatio`). */
    ratio: number | null;
    /** The most ice any source shown expects, in inches. */
    iceInches: number | null;
    /** Average strongest gust, in mph. */
    gustMph: number | null;
}

export const outageLimits = {
    wetSnowInches: 4,
    wetRatio: 8,
    iceInches: 0.25,
    windWithSnowMph: 40,
    windWithSnowInches: 2,
    windAloneMph: 58,
} as const;

/** Sources with less snow than this don't count towards the ratio; a dusting in a rainstorm would read as very wet snow. */
export const ratioMinimumSnowInches = 1;

/**
 * The town's snow-to-liquid ratio from the sources with real snow: their total snow over their total water, so a
 * source with more snow counts for more. Null when none has at least an inch with a ratio.
 */
export function snowRatio(cells: { highInches: number | null; ratio: number | null }[]): number | null {
    let snow = 0;
    let water = 0;
    for (const cell of cells) {
        if (cell.highInches !== null && cell.highInches >= ratioMinimumSnowInches && cell.ratio !== null && cell.ratio > 0) {
            snow += cell.highInches;
            water += cell.highInches / cell.ratio;
        }
    }

    return water > 0 ? snow / water : null;
}

/** The reasons a town is at risk, or none. */
export function outageReasons({ snowInches, ratio, iceInches, gustMph }: OutageInputs): string[] {
    const snow = snowInches ?? 0;
    const ice = iceInches ?? 0;
    const gust = gustMph ?? 0;
    const reasons: string[] = [];

    if (snow >= outageLimits.wetSnowInches && ratio !== null && ratio <= outageLimits.wetRatio) {
        reasons.push(`heavy wet snow (${Math.round(snow)} in at ${Math.round(ratio)}:1)`);
    }

    if (ice >= outageLimits.iceInches) {
        reasons.push(`ice of ${ice.toFixed(2)} in`);
    }

    if (gust >= outageLimits.windAloneMph) {
        reasons.push(`gusts near ${Math.round(gust)} mph`);
    } else if (gust >= outageLimits.windWithSnowMph && (snow >= outageLimits.windWithSnowInches || ice > 0)) {
        reasons.push(`gusts near ${Math.round(gust)} mph with ${ice > 0 ? 'ice' : 'snow'}`);
    }

    return reasons;
}
