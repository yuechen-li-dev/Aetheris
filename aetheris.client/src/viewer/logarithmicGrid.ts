export interface LogGridSelection {
    primarySpacing: number;
    secondarySpacing: number;
    primaryWeight: number;
    secondaryWeight: number;
    exponent: number;
    blend: number;
}

const MIN_SPACING = 1e-10;

export function selectLogarithmicGridScales(worldSpan: number, targetCellCount = 14): LogGridSelection {
    const normalizedSpan = Math.max(worldSpan, MIN_SPACING);
    const normalizedTarget = Math.max(targetCellCount, 1);
    const rawExponent = Math.log10(normalizedSpan / normalizedTarget);
    const exponent = Math.floor(rawExponent);
    const blend = rawExponent - exponent;

    const primarySpacing = 10 ** exponent;
    const secondarySpacing = 10 ** (exponent + 1);
    const secondaryWeight = blend;
    const primaryWeight = 1 - secondaryWeight;

    return {
        primarySpacing,
        secondarySpacing,
        primaryWeight,
        secondaryWeight,
        exponent,
        blend,
    };
}
