import { describe, expect, it } from "vitest";
import { buildAdaptiveGridPlan, selectLogarithmicGridScales } from "../viewer/logarithmicGrid";

function profileLegacyGrid(worldSpan: number, targetCellCount = 14) {
	const selection = selectLogarithmicGridScales(worldSpan, targetCellCount);
	let logicalLines = 0;
	for (const spacing of [selection.primarySpacing, selection.secondarySpacing]) {
		const lineCountPerAxis = Math.ceil(worldSpan / spacing) + 1;
		logicalLines += lineCountPerAxis * 2;
	}
	// Preview 1 rendered each logical line above and below the plane, with a
	// separate Drei geometry/draw for both the halo and core stroke.
	return {
		logicalLines,
		reactLineComponents: logicalLines * 4,
		drawCalls: logicalLines * 4,
		geometryCount: logicalLines * 4,
		regenerationPolicy: "camera move > 0.01 world units or zoom > 0.1%",
	};
}

describe("logarithmic grid profile", () => {
	it("records the structural before/after budget at representative scales", () => {
		const samples = [20, 200, 2_000].map((span) => {
			const before = profileLegacyGrid(span);
			const after = buildAdaptiveGridPlan({
				bounds: { minX: -span / 2, maxX: span / 2, minZ: -span / 2, maxZ: span / 2 },
			});
			return {
				span,
				before,
				after: {
					lineCount: after.lineCount,
					drawCalls: after.drawCallCount,
					geometryCount: after.drawCallCount,
					allocatedBytes: after.allocatedBytes,
					regenerationPolicy: "camera move or zoom > 4% of the visible scale",
				},
			};
		});

		console.info("CADMATA_GRID_PROFILE", JSON.stringify(samples));
		expect(samples.every((sample) => sample.after.drawCalls <= 4)).toBe(true);
		expect(samples.every((sample) => sample.after.lineCount <= 100)).toBe(true);
	});
});
