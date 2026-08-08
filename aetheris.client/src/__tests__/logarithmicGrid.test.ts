import { describe, expect, it } from "vitest";
import { buildAdaptiveGridPlan, selectLogarithmicGridScales } from "../viewer/logarithmicGrid";

describe("selectLogarithmicGridScales", () => {
	it("selects engineering-friendly powers of ten and adjacent levels", () => {
		const selection = selectLogarithmicGridScales(140, 14);

		expect(selection.primarySpacing).toBe(10);
		expect(selection.secondarySpacing).toBe(100);
		expect(selection.exponent).toBe(1);
		expect(selection.blend).toBeCloseTo(0, 6);
		expect(selection.primaryWeight).toBeCloseTo(1, 6);
		expect(selection.secondaryWeight).toBeCloseTo(0, 6);
	});

	it("blends smoothly between adjacent levels as span changes", () => {
		const selection = selectLogarithmicGridScales(442, 14);

		expect(selection.primarySpacing).toBe(10);
		expect(selection.secondarySpacing).toBe(100);
		expect(selection.blend).toBeGreaterThan(0.49);
		expect(selection.blend).toBeLessThan(0.51);
		expect(selection.primaryWeight + selection.secondaryWeight).toBeCloseTo(1, 6);
	});

	it("remains stable for very small spans", () => {
		const selection = selectLogarithmicGridScales(0, 14);

		expect(selection.primarySpacing).toBeGreaterThan(0);
		expect(selection.secondarySpacing).toBeGreaterThan(selection.primarySpacing);
		expect(Number.isFinite(selection.blend)).toBe(true);
	});
});

describe("buildAdaptiveGridPlan", () => {
	it("caps geometry and batches all lines into a small fixed draw-call budget", () => {
		const plan = buildAdaptiveGridPlan({
			bounds: { minX: -1_000_000, maxX: 1_000_000, minZ: -1_000_000, maxZ: 1_000_000 },
			targetCellCount: 200_000,
			maxLinesPerAxis: 48,
			majorStep: 5,
		});

		expect(plan.layers.every((layer) => layer.lineCount <= 100)).toBe(true);
		expect(plan.drawCallCount).toBeLessThanOrEqual(4);
		expect(plan.allocatedBytes).toBeLessThan(10_000);
	});

	it("keeps adjacent logarithmic bands and stable line counts across camera-scale changes", () => {
		const near = buildAdaptiveGridPlan({ bounds: { minX: -10, maxX: 10, minZ: -10, maxZ: 10 } });
		const far = buildAdaptiveGridPlan({ bounds: { minX: -100, maxX: 100, minZ: -100, maxZ: 100 } });
		expect(near.lineCount).toBe(far.lineCount);
		expect(far.layers[0].spacing / near.layers[0].spacing).toBe(10);
	});
});
