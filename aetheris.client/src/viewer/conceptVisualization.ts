import { M } from "machinalayout/machina";

export type CadmataLayer =
	| "material"
	| "brepEdges"
	| "conceptPoints"
	| "conceptAxes"
	| "conceptRegions"
	| "conceptPlanes"
	| "constructionPlanes"
	| "profileGuides"
	| "profileLoops"
	| "composeRegions"
	| "selections"
	| "diagnostics";
export type CadmataPoint = { x: number; y: number; z: number };
export type CadmataGeometry =
	| { type: "point"; point: CadmataPoint }
	| { type: "polyline"; points: CadmataPoint[]; closed?: boolean }
	| { type: "plane"; origin: CadmataPoint; u: CadmataPoint; v: CadmataPoint }
	| { type: "circle"; center: CadmataPoint; radius: number; normal?: CadmataPoint };

export interface CadmataDiagnostic {
	code: string;
	message: string;
	severity: "info" | "warning" | "error";
}
export interface CadmataEntity {
	stableId: string;
	kind: string;
	label: string;
	role?: string;
	layer: CadmataLayer;
	geometry?: CadmataGeometry;
	sourceSpan?: string;
	parentIds?: string[];
	childIds?: string[];
	constructionDescendantIds?: string[];
	materializedDescendantIds?: string[];
	topology?: {
		faceIds?: number[];
		edgeIds?: number[];
		loopIds?: number[];
		vertexIds?: number[];
		directedEdgeIds?: number[];
	};
	selectionIds?: string[];
	consumer?: string;
	diagnostics?: CadmataDiagnostic[];
	metadata?: Record<string, string | number | boolean | null>;
}
export interface CadmataSelection {
	stableId: string;
	label: string;
	kind: "EdgeSet" | "FaceSet" | "LoopSet" | "Chain" | "VertexSet";
	entityIds: string[];
	orderedEntityIds?: string[];
	closed?: boolean;
	diagnostics?: CadmataDiagnostic[];
}
export interface CadmataVisualizationArtifact {
	schemaVersion: "cadmata-concept-viz-x1";
	fixtureId: string;
	sourcePath: string;
	entities: CadmataEntity[];
	selections: CadmataSelection[];
	diagnostics?: CadmataDiagnostic[];
	metrics?: Record<string, number>;
}
export interface CadmataSelectionState {
	entityIds: Set<string>;
	faceIds: Set<number>;
	edgeIds: Set<number>;
	diagnostics: CadmataDiagnostic[];
}

const layers: CadmataLayer[] = [
	"material",
	"brepEdges",
	"conceptPoints",
	"conceptAxes",
	"conceptRegions",
	"conceptPlanes",
	"constructionPlanes",
	"profileGuides",
	"profileLoops",
	"composeRegions",
	"selections",
	"diagnostics",
];

// The panel is still rendered with the existing Cadmata shell, but this record
// fixes its intended tool/readout composition for future responsive lowering.
export const CADMATA_INSPECTOR_LAYOUT = M.vstack("cadmata-inspector-stack", { gap: 8 }, [
	M.fixed("cadmata-fixture-tools", 44),
	M.fill("cadmata-evidence", 1),
]);

export function parseCadmataVisualizationArtifact(value: unknown): CadmataVisualizationArtifact {
	const artifact = value as Partial<CadmataVisualizationArtifact>;
	if (
		artifact?.schemaVersion !== "cadmata-concept-viz-x1" ||
		!Array.isArray(artifact.entities) ||
		!Array.isArray(artifact.selections)
	) {
		throw new Error(
			"Invalid Cadmata visualization artifact: expected cadmata-concept-viz-x1 entities and selections.",
		);
	}
	const ids = new Set<string>();
	for (const entity of artifact.entities) {
		if (!entity.stableId || !entity.label || !layers.includes(entity.layer))
			throw new Error("Invalid Cadmata visualization entity.");
		if (ids.has(entity.stableId))
			throw new Error(`Duplicate Cadmata visualization stableId '${entity.stableId}'.`);
		ids.add(entity.stableId);
	}
	return artifact as CadmataVisualizationArtifact;
}

export function indexCadmataArtifact(artifact: CadmataVisualizationArtifact) {
	const byId = new Map(artifact.entities.map((entity) => [entity.stableId, entity]));
	const reverse = new Map<string, Set<string>>();
	const ownedChildren = new Map<string, Set<string>>();
	for (const entity of artifact.entities) {
		for (const parentId of entity.parentIds ?? []) {
			const children = ownedChildren.get(parentId) ?? new Set<string>();
			children.add(entity.stableId);
			ownedChildren.set(parentId, children);
		}
		for (const id of [
			...(entity.parentIds ?? []),
			...(entity.childIds ?? []),
			...(entity.constructionDescendantIds ?? []),
			...(entity.materializedDescendantIds ?? []),
		]) {
			const related = reverse.get(id) ?? new Set<string>();
			related.add(entity.stableId);
			reverse.set(id, related);
		}
	}
	return { byId, reverse, ownedChildren };
}

/** Traverses only compiler-published relation fields; it never tries to infer BRep correspondence in the browser. */
export function resolveCadmataSelection(
	artifact: CadmataVisualizationArtifact,
	stableId: string | null,
): CadmataSelectionState {
	if (!stableId)
		return { entityIds: new Set(), faceIds: new Set(), edgeIds: new Set(), diagnostics: [] };
	const { byId, ownedChildren } = indexCadmataArtifact(artifact);
	const pending = [stableId];
	const entityIds = new Set<string>();
	const diagnostics: CadmataDiagnostic[] = [];
	while (pending.length) {
		const id = pending.pop()!;
		if (entityIds.has(id)) continue;
		const entity = byId.get(id);
		if (!entity) {
			diagnostics.push({
				code: "Cadmata.MissingDescendant",
				severity: "warning",
				message: `Compiler correspondence references missing entity '${id}'.`,
			});
			continue;
		}
		entityIds.add(id);
		// A low-level pick highlights exactly the published BRep entity. Semantic
		// ownership remains inspector data and must not broaden the face highlight.
		if (entity.kind.startsWith("BRep")) continue;
		pending.push(
			...(entity.childIds ?? []),
			...(entity.constructionDescendantIds ?? []),
			...(entity.materializedDescendantIds ?? []),
			...(entity.selectionIds ?? []),
			...(ownedChildren.get(id) ?? []),
		);
	}
	const faceIds = new Set<number>();
	const edgeIds = new Set<number>();
	for (const id of entityIds) {
		const topology = byId.get(id)?.topology;
		topology?.faceIds?.forEach((faceId) => faceIds.add(faceId));
		topology?.edgeIds?.forEach((edgeId) => edgeIds.add(edgeId));
		diagnostics.push(...(byId.get(id)?.diagnostics ?? []));
	}
	return { entityIds, faceIds, edgeIds, diagnostics };
}
