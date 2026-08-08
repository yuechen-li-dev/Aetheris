import type { DisplayPreparationResponseDto } from "../api/aetherisApi";
import { mapDisplayPreparationToDisplayScene, type DisplayScene } from "./displayRenderables";
import { legacyTessellationToDisplayScene } from "./legacyTessellationToDisplayScene";

export type DisplayRenderPath = "analytic-only" | "mixed-fallback" | "fallback";

export interface DisplaySceneBuildResult {
	renderPath: DisplayRenderPath;
	displayScene: DisplayScene | null;
	missingFallbackFaceIds: number[];
}

function getMissingFallbackFaceIds(
	requiredFallbackFaceIds: Set<number>,
	renderedFallbackFaceIds: Set<number>,
): number[] {
	return Array.from(requiredFallbackFaceIds)
		.filter((faceId) => !renderedFallbackFaceIds.has(faceId))
		.sort((left, right) => left - right);
}

function sceneHasTypedRenderable(scene: DisplayScene | null): boolean {
	return (scene?.renderables.length ?? 0) > 0;
}

export function buildDisplaySceneData(
	preparation: DisplayPreparationResponseDto | null,
): DisplaySceneBuildResult {
	if (!preparation) {
		return {
			renderPath: "fallback",
			displayScene: null,
			missingFallbackFaceIds: [],
		};
	}

	const typedDisplayScene = mapDisplayPreparationToDisplayScene(preparation);
	const fallbackDisplayScene =
		!sceneHasTypedRenderable(typedDisplayScene) && preparation.tessellationFallback
			? legacyTessellationToDisplayScene(preparation.tessellationFallback)
			: null;
	const primaryDisplayScene = sceneHasTypedRenderable(typedDisplayScene)
		? typedDisplayScene
		: fallbackDisplayScene;

	if (preparation.lane === "analytic-only") {
		return {
			renderPath: "analytic-only",
			displayScene: primaryDisplayScene,
			missingFallbackFaceIds: [],
		};
	}

	if (preparation.lane === "mixed-fallback") {
		const fallbackFaceIds = new Set(
			preparation.analyticPacket.fallbackFaces.map((face) => face.faceId),
		);
		const renderedFallbackFaceIds = new Set(
			(primaryDisplayScene?.renderables ?? [])
				.filter(
					(renderable) => renderable.kind === "MeshPatch" && fallbackFaceIds.has(renderable.faceId),
				)
				.map((renderable) => renderable.faceId),
		);

		return {
			renderPath: "mixed-fallback",
			displayScene: primaryDisplayScene,
			missingFallbackFaceIds: getMissingFallbackFaceIds(fallbackFaceIds, renderedFallbackFaceIds),
		};
	}

	return {
		renderPath: "fallback",
		displayScene: primaryDisplayScene,
		missingFallbackFaceIds: [],
	};
}
