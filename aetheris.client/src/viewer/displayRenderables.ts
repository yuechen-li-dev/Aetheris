import type {
	DisplayDiagnosticDto,
	DisplayFaceDto,
	DisplayLaneDto,
	DisplayPreparationResponseDto,
} from "../api/aetherisApi";
import { analyticPatchToPreviewMesh } from "./analyticMapper";
import {
	mapFacePatchToRenderFacePatch,
	type RenderEdgePolyline,
	type RenderFacePatch,
} from "./tessellationMapper";

export type DisplayRenderableKind = "AnalyticPatch" | "MeshPatch" | "WirePatch" | "DiagnosticPatch";

export interface DisplayRenderableBase {
	kind: DisplayRenderableKind;
	faceId: number;
	surfaceKind: string | null;
	status: string;
	patchKind: string;
	materializationLane: string | null;
	diagnostics: DisplayDiagnosticDto[];
}

export interface AnalyticRenderable extends DisplayRenderableBase {
	kind: "AnalyticPatch";
	previewMesh: RenderFacePatch;
}

export interface MeshRenderable extends DisplayRenderableBase {
	kind: "MeshPatch";
	mesh: RenderFacePatch;
}

export interface WireRenderable extends DisplayRenderableBase {
	kind: "WirePatch";
	wires: RenderEdgePolyline[];
}

export interface DiagnosticRenderable extends DisplayRenderableBase {
	kind: "DiagnosticPatch";
}

export type DisplayRenderable =
	| AnalyticRenderable
	| MeshRenderable
	| WireRenderable
	| DiagnosticRenderable;

export interface DisplayScene {
	status: string | null;
	sourceAuthority: string | null;
	displayAuthority: string | null;
	lanes: DisplayLaneDto[];
	renderables: DisplayRenderable[];
	diagnostics: DisplayDiagnosticDto[];
	legacyCompatibility?: {
		source: "tessellationFallback";
		facePatchCount: number;
		edgePolylineCount: number;
	};
}

function base(face: DisplayFaceDto): Omit<DisplayRenderableBase, "kind"> {
	return {
		faceId: face.faceId,
		surfaceKind: face.surfaceKind,
		status: face.status,
		patchKind: face.patchKind,
		materializationLane: face.materializationLane ?? null,
		diagnostics: face.diagnostics ?? [],
	};
}

function wireEdges(face: DisplayFaceDto): RenderEdgePolyline[] {
	return (
		face.wirePatch?.loops
			.flatMap((loop) => loop.edges)
			.filter((edge) => edge.points.length >= 2)
			.map((edge) => ({
				edgeId: edge.edgeId,
				points: new Float32Array(edge.points.flatMap((point) => [point.x, point.y, point.z])),
			})) ?? []
	);
}

function analyticPreviewNeedsFallbackMesh(face: DisplayFaceDto): boolean {
	if (!face.analyticPatch) {
		return false;
	}

	if (face.analyticPatch.surfaceKind === "Plane") {
		return face.analyticPatch.loopCount > 1;
	}

	return face.analyticPatch.surfaceKind === "Cylinder" || face.analyticPatch.surfaceKind === "Cone";
}

function analyticFallbackMeshRenderable(
	face: DisplayFaceDto,
	fallbackMesh: RenderFacePatch,
): MeshRenderable {
	return {
		...base(face),
		kind: "MeshPatch",
		status: "Mesh",
		patchKind: "MeshPatch",
		materializationLane: "BoundedMesh",
		mesh: fallbackMesh,
	};
}

export function mapDisplayFaceToRenderable(
	face: DisplayFaceDto,
	fallbackMesh: RenderFacePatch | null = null,
): DisplayRenderable {
	if (face.patchKind === "AnalyticPatch" && face.analyticPatch) {
		if (fallbackMesh && analyticPreviewNeedsFallbackMesh(face)) {
			return analyticFallbackMeshRenderable(face, fallbackMesh);
		}

		const previewMesh = analyticPatchToPreviewMesh(face.analyticPatch);
		if (previewMesh) {
			return { ...base(face), kind: "AnalyticPatch", previewMesh };
		}

		if (fallbackMesh) {
			return analyticFallbackMeshRenderable(face, fallbackMesh);
		}
	}

	if (
		(face.patchKind === "MeshPatch" || face.materializationLane === "BoundedMesh") &&
		face.meshPatch
	) {
		return {
			...base(face),
			kind: "MeshPatch",
			mesh: mapFacePatchToRenderFacePatch(face.meshPatch),
		};
	}

	if (
		face.patchKind === "WirePatch" ||
		face.status === "WireframeOnly" ||
		face.materializationLane === "WirePatch"
	) {
		return { ...base(face), kind: "WirePatch", wires: wireEdges(face) };
	}

	return { ...base(face), kind: "DiagnosticPatch" };
}

export function mapDisplayPreparationToDisplayScene(
	preparation: DisplayPreparationResponseDto | null,
): DisplayScene | null {
	if (!preparation) return null;
	const fallbackMeshByFaceId = new Map(
		(preparation.tessellationFallback?.facePatches ?? []).map((patch) => [
			patch.faceId,
			mapFacePatchToRenderFacePatch(patch),
		]),
	);

	return {
		status: preparation.status ?? null,
		sourceAuthority: preparation.sourceAuthority ?? null,
		displayAuthority: preparation.displayAuthority ?? null,
		lanes: preparation.displayLanes ?? [],
		renderables: (preparation.faces ?? []).map((face) =>
			mapDisplayFaceToRenderable(face, fallbackMeshByFaceId.get(face.faceId) ?? null),
		),
		diagnostics: preparation.diagnostics ?? [],
	};
}
