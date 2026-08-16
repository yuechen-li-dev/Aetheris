import { describe, expect, it } from "vitest";
import {
	DEFAULT_PMI_VISIBILITY,
	layoutPmiCallouts,
	semanticPmiItems,
} from "../viewer/PmiAnnotationLayer";
import type { CadmataVisualizationArtifact } from "../viewer/conceptVisualization";

const artifact: CadmataVisualizationArtifact = {
	schemaVersion: "cadmata-concept-viz-x1",
	fixtureId: "pmi",
	sourcePath: "part.step",
	selections: [],
	entities: [
		{ stableId: "datum-a", kind: "Datum", label: "Datum A", layer: "selections", geometry: { type: "polyline", points: [{ x: 0, y: 0, z: 0 }] } },
		{ stableId: "position", kind: "Position", label: "Mount position", layer: "selections", geometry: { type: "polyline", points: [{ x: 0, y: 0, z: 0 }] }, metadata: { datumRefs: "A | B | C" } },
		{ stableId: "note", kind: "Annotation", label: "Deburr", layer: "selections", geometry: { type: "polyline", points: [{ x: 0, y: 0, z: 0 }] } },
	],
};

describe("semantic PMI presentation", () => {
	it("filters independent categories without changing semantic entities", () => {
		const allVisible = { datums: true, dimensions: true, geometricTolerances: true, engineeringAnnotations: true };
		expect(semanticPmiItems(artifact, allVisible).map((item) => item.entity.stableId)).toEqual(["datum-a", "position", "note"]);
		expect(semanticPmiItems(artifact, { ...allVisible, engineeringAnnotations: false }).map((item) => item.entity.stableId)).toEqual(["datum-a", "position"]);
		expect(artifact.entities).toHaveLength(3);
	});

	it("places coincident anchors deterministically without collisions", () => {
		const items = semanticPmiItems(artifact, { ...DEFAULT_PMI_VISIBILITY, engineeringAnnotations: true }).map((item) => ({ ...item, screenAnchor: { x: 400, y: 300, z: 0 } }));
		const first = layoutPmiCallouts(items, 800, 600, new Map());
		const second = layoutPmiCallouts(items, 800, 600, new Map());
		expect(second).toEqual(first);
		expect(new Set(first.filter((item) => !item.hidden).map((item) => `${item.x.toFixed(2)},${item.y.toFixed(2)}`)).size).toBe(first.filter((item) => !item.hidden).length);
	});

	it("honors presentation-only manual offsets", () => {
		const [item] = semanticPmiItems(artifact, DEFAULT_PMI_VISIBILITY);
		const [placed] = layoutPmiCallouts([{ ...item, screenAnchor: { x: 400, y: 300, z: 0 } }], 800, 600, new Map([["datum-a", { x: 90, y: -40 }]]));
		expect(placed).toMatchObject({ x: 490, y: 260, hidden: false });
		expect(item.anchor).toEqual([0, 0, 0]);
	});
});
