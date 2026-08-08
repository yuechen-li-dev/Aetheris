export type ApplicationSelection =
	| { kind: "none" }
	| { kind: "body"; occurrenceId: string }
	| { kind: "brep-face"; occurrenceId: string; faceId: number }
	| { kind: "brep-edge"; occurrenceId: string; edgeId: number }
	| { kind: "semantic-feature"; stableId: string; featureKind?: string }
	| { kind: "pmi"; stableId: string }
	| { kind: "template-instance"; stableId: string };

export const NO_SELECTION: ApplicationSelection = { kind: "none" };

export function selectedFaceId(selection: ApplicationSelection): number | null {
	return selection.kind === "brep-face" ? selection.faceId : null;
}

export function selectedEdgeId(selection: ApplicationSelection): number | null {
	return selection.kind === "brep-edge" ? selection.edgeId : null;
}
