import { describe, expect, it } from "vitest";
import {
	selectedEdgeId,
	selectedFaceId,
	type ApplicationSelection,
} from "../application/selection";

describe("application selection", () => {
	it("keeps renderer IDs behind semantic selection variants", () => {
		const face: ApplicationSelection = { kind: "brep-face", occurrenceId: "occ-1", faceId: 42 };
		const feature: ApplicationSelection = {
			kind: "semantic-feature",
			stableId: "hole:1",
			featureKind: "Hole",
		};
		expect(selectedFaceId(face)).toBe(42);
		expect(selectedEdgeId(face)).toBeNull();
		expect(selectedFaceId(feature)).toBeNull();
	});
});
