import { describe, expect, it } from "vitest";
import {
	parseCadmataVisualizationArtifact,
	resolveCadmataSelection,
	type CadmataVisualizationArtifact,
} from "../viewer/conceptVisualization";

const artifact: CadmataVisualizationArtifact = {
	schemaVersion: "cadmata-concept-viz-x1",
	fixtureId: "shaft",
	sourcePath: "fixture.firmament",
	diagnostics: [],
	metrics: {},
	selections: [],
	entities: [
		{
			stableId: "hole:mount",
			kind: "HoleFeature",
			label: "mount",
			layer: "conceptAxes",
			materializedDescendantIds: ["wall", "entry"],
		},
		{
			stableId: "wall",
			kind: "BRepFace",
			label: "wall",
			layer: "selections",
			topology: { faceIds: [7] },
		},
		{
			stableId: "entry",
			kind: "BRepEdge",
			label: "entry",
			layer: "brepEdges",
			topology: { edgeIds: [14] },
		},
	],
};

describe("Cadmata concept visualization artifact", () => {
	it("resolves only compiler-published source-to-descendant topology", () => {
		const resolved = resolveCadmataSelection(artifact, "hole:mount");
		expect([...resolved.faceIds]).toEqual([7]);
		expect([...resolved.edgeIds]).toEqual([14]);
	});
	it("reports a missing correspondence instead of inventing a highlight", () => {
		const invalid = {
			...artifact,
			entities: [{ ...artifact.entities[0], materializedDescendantIds: ["missing"] }],
		};
		expect(resolveCadmataSelection(invalid, "hole:mount").diagnostics[0].code).toBe(
			"Cadmata.MissingDescendant",
		);
	});
	it("rejects an artifact without the X1 contract", () =>
		expect(() => parseCadmataVisualizationArtifact({ entities: [], selections: [] })).toThrow(
			"cadmata-concept-viz-x1",
		));
	it.each(["conceptPlanes", "constructionPlanes"] as const)(
		"accepts the server-published %s layer",
		(layer) => {
			expect(() =>
				parseCadmataVisualizationArtifact({
					...artifact,
					entities: [{ ...artifact.entities[0], layer }],
				}),
			).not.toThrow();
		},
	);
});
