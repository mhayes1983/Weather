/**
 * A rough guide to power outage risk for one town, from the averages of the sources shown. It's the usual
 * recipe for outages: heavy wet snow or ice weighing on limbs and lines, made worse by wind, or wind alone at
 * High Wind Warning strength. It isn't a forecast from any utility or the NWS.
 */
export interface OutageInputs {
    /** Average snow, in inches. */
    snowInches: number | null;
    /** Average snow-to-liquid ratio, such as 8 for 8:1. */
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
