import type { CadmataEntity, CadmataVisualizationArtifact } from "./conceptVisualization";

export type SemanticTreeNode = { entity: CadmataEntity; children: SemanticTreeNode[] };

const KIND_ORDER = [
	"Body",
	"Profile",
	"Pattern",
	"HoleFeature",
	"Counterbore",
	"EdgeFinish",
	"SlotFeature",
	"ConstructionPlane",
	"Datum",
	"Dimension",
	"Diameter",
	"Position",
	"Annotation",
	"HoleDiameter",
	"PMI",
];

export const SEMANTIC_PMI_KINDS = new Set([
	"Datum",
	"Dimension",
	"Diameter",
	"Position",
	"Annotation",
	"HoleDiameter",
]);

/** Document-scoped index: all correspondence is published by Aetheris; no ownership is inferred here. */
export function indexSemanticInspection(artifact: CadmataVisualizationArtifact) {
	const byId = new Map(artifact.entities.map((entity) => [entity.stableId, entity]));
	const faceOwners = new Map<number, CadmataEntity[]>();
	const edgeOwners = new Map<number, CadmataEntity[]>();
	const pmiByTarget = new Map<string, CadmataEntity[]>();
	for (const entity of artifact.entities) {
		const publishedOwners = entity.kind.startsWith("BRep")
			? (entity.parentIds ?? [])
					.map((id) => byId.get(id))
					.filter((item): item is CadmataEntity => Boolean(item))
			: [entity];
		for (const faceId of entity.topology?.faceIds ?? []) {
			const owners = faceOwners.get(faceId) ?? [];
			for (const owner of publishedOwners)
				if (
					!owner.kind.startsWith("BRep") &&
					!owners.some((item) => item.stableId === owner.stableId)
				)
					owners.push(owner);
			faceOwners.set(faceId, owners);
		}
		for (const edgeId of entity.topology?.edgeIds ?? []) {
			const owners = edgeOwners.get(edgeId) ?? [];
			for (const owner of publishedOwners)
				if (
					!owner.kind.startsWith("BRep") &&
					!owners.some((item) => item.stableId === owner.stableId)
				)
					owners.push(owner);
			edgeOwners.set(edgeId, owners);
		}
		const target = entity.metadata?.targetSemanticId;
		if (SEMANTIC_PMI_KINDS.has(entity.kind) && typeof target === "string")
			(pmiByTarget.get(target) ?? pmiByTarget.set(target, []).get(target)!).push(entity);
	}
	const brepFaceById = new Map(
		artifact.entities.flatMap((entity) =>
			entity.kind === "BRepFace"
				? (entity.topology?.faceIds ?? []).map((id) => [id, entity] as const)
				: [],
		),
	);
	const brepEdgeById = new Map(
		artifact.entities.flatMap((entity) =>
			entity.kind === "BRepEdge"
				? (entity.topology?.edgeIds ?? []).map((id) => [id, entity] as const)
				: [],
		),
	);
	return { byId, faceOwners, edgeOwners, pmiByTarget, brepFaceById, brepEdgeById };
}

export function resolvePublishedBrepEntity(
	artifact: CadmataVisualizationArtifact,
	kind: "Face" | "Edge",
	id: number,
) {
	const index = indexSemanticInspection(artifact);
	return kind === "Face"
		? (index.brepFaceById.get(id) ?? null)
		: (index.brepEdgeById.get(id) ?? null);
}

export function semanticTree(artifact: CadmataVisualizationArtifact): SemanticTreeNode[] {
	const { byId } = indexSemanticInspection(artifact);
	const children = new Map<string, CadmataEntity[]>();
	const roots: CadmataEntity[] = [];
	for (const entity of artifact.entities.filter((item) => !item.kind.startsWith("BRep"))) {
		const parent = entity.parentIds?.find((id) => byId.has(id));
		if (!parent) roots.push(entity);
		else (children.get(parent) ?? children.set(parent, []).get(parent)!).push(entity);
	}
	const sort = (items: CadmataEntity[]) =>
		items.sort(
			(a, b) =>
				KIND_ORDER.indexOf(a.kind) - KIND_ORDER.indexOf(b.kind) || a.label.localeCompare(b.label),
		);
	const build = (entity: CadmataEntity): SemanticTreeNode => ({
		entity,
		children: sort(children.get(entity.stableId) ?? []).map(build),
	});
	return sort(roots).map(build);
}

export function formatPmiLabel(entity: CadmataEntity) {
	const value = entity.metadata?.nominal ?? entity.metadata?.value ?? "";
	const plus = entity.metadata?.tolerancePlus;
	const minus = entity.metadata?.toleranceMinus;
	const tolerance = plus || minus ? ` +${plus ?? "0"}/-${minus ?? "0"}` : "";
	const quantity = Number(entity.metadata?.quantity ?? 0);
	const prefix = quantity > 1 ? `${quantity}× ` : "";
	return entity.kind === "HoleDiameter" || entity.kind === "Diameter"
		? `${prefix}⌀${value}${tolerance}`
		: entity.kind === "Dimension"
			? `${prefix}${entity.label}: ${value}${tolerance}`
			: entity.label;
}
