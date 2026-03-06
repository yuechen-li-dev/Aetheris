import { describe, expect, it } from 'vitest';
import { selectLogarithmicGridScales } from '../viewer/logarithmicGrid';

describe('selectLogarithmicGridScales', () => {
    it('selects engineering-friendly powers of ten and adjacent levels', () => {
        const selection = selectLogarithmicGridScales(140, 14);

        expect(selection.primarySpacing).toBe(10);
        expect(selection.secondarySpacing).toBe(100);
        expect(selection.exponent).toBe(1);
        expect(selection.blend).toBeCloseTo(0, 6);
        expect(selection.primaryWeight).toBeCloseTo(1, 6);
        expect(selection.secondaryWeight).toBeCloseTo(0, 6);
    });

    it('blends smoothly between adjacent levels as span changes', () => {
        const selection = selectLogarithmicGridScales(442, 14);

        expect(selection.primarySpacing).toBe(10);
        expect(selection.secondarySpacing).toBe(100);
        expect(selection.blend).toBeGreaterThan(0.49);
        expect(selection.blend).toBeLessThan(0.51);
        expect(selection.primaryWeight + selection.secondaryWeight).toBeCloseTo(1, 6);
    });

    it('remains stable for very small spans', () => {
        const selection = selectLogarithmicGridScales(0, 14);

        expect(selection.primarySpacing).toBeGreaterThan(0);
        expect(selection.secondarySpacing).toBeGreaterThan(selection.primarySpacing);
        expect(Number.isFinite(selection.blend)).toBe(true);
    });
});
