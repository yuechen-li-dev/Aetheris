import { describe, expect, it } from "vitest";
import { formatPmiLabel, indexSemanticInspection, resolvePublishedBrepEntity, semanticTree } from "../viewer/semanticInspection";
import type { CadmataVisualizationArtifact } from "../viewer/conceptVisualization";

const artifact: CadmataVisualizationArtifact = { schemaVersion: "cadmata-concept-viz-x1", fixtureId: "semantic", sourcePath: "fixture", selections: [], entities: [
	{ stableId: "body", kind: "Body", label: "Bracket", layer: "material", childIds: ["hole"] },
	{ stableId: "hole", kind: "HoleFeature", label: "Mount", layer: "conceptAxes", parentIds: ["body"], topology: { faceIds: [17] } },
	{ stableId: "face-17", kind: "BRepFace", label: "Face 17", layer: "selections", parentIds: ["hole"], topology: { faceIds: [17] } },
	{ stableId: "pmi", kind: "HoleDiameter", label: "MountDiameter", layer: "conceptPoints", geometry: { type: "circle", center: { x: 1, y: 2, z: 3 }, radius: 4 }, metadata: { targetSemanticId: "hole", nominal: "8 mm", tolerancePlus: "0.05 mm", toleranceMinus: "0.02 mm" } },
] };

describe("semantic inspection index", () => {
	it("keeps compiler-published face owners and deterministic hierarchy", () => {
		expect(indexSemanticInspection(artifact).faceOwners.get(17)?.map((entity) => entity.label)).toEqual(["Mount"]);
		expect(semanticTree(artifact)[0].children[0].entity.stableId).toBe("hole");
		expect(resolvePublishedBrepEntity(artifact, "Face", 17)?.stableId).toBe("face-17");
		expect(indexSemanticInspection(artifact).pmiByTarget.get("hole")?.[0].label).toBe("MountDiameter");
	});
	it("formats an asymmetric HoleDiameter callout", () => {
		expect(formatPmiLabel(artifact.entities[3])).toBe("⌀8 mm +0.05 mm/-0.02 mm");
	});
});
