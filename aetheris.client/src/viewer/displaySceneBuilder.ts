import type { DisplayPreparationResponseDto } from '../api/aetherisApi';
import { mapAnalyticPacketToRenderData } from './analyticMapper';
import { mapDisplayPreparationToDisplayScene, type DisplayScene } from './displayRenderables';
import { mapTessellationToRenderData, type RenderSceneData } from './tessellationMapper';

export type DisplayRenderPath = 'analytic-only' | 'mixed-fallback' | 'fallback';

export interface DisplaySceneBuildResult {
  renderPath: DisplayRenderPath;
  sceneData: RenderSceneData | null;
  displayScene: DisplayScene | null;
  missingFallbackFaceIds: number[];
}

interface BuildDependencies {
  mapAnalytic: typeof mapAnalyticPacketToRenderData;
  mapFallback: typeof mapTessellationToRenderData;
}

const defaultDependencies: BuildDependencies = {
  mapAnalytic: mapAnalyticPacketToRenderData,
  mapFallback: mapTessellationToRenderData,
};

function composeSceneData(primary: RenderSceneData, secondary: RenderSceneData): RenderSceneData {
  return {
    faces: [...primary.faces, ...secondary.faces],
    edges: [...primary.edges, ...secondary.edges],
  };
}

function filterFallbackFaces(
  fallbackScene: RenderSceneData,
  analyticFaceIds: Set<number>,
  fallbackFaceIds: Set<number>,
): RenderSceneData {
  return {
    faces: fallbackScene.faces.filter((face) => fallbackFaceIds.has(face.faceId) && !analyticFaceIds.has(face.faceId)),
    edges: fallbackScene.edges,
  };
}

function getMissingFallbackFaceIds(requiredFallbackFaceIds: Set<number>, renderedFallbackFaceIds: Set<number>): number[] {
  return Array.from(requiredFallbackFaceIds)
    .filter((faceId) => !renderedFallbackFaceIds.has(faceId))
    .sort((left, right) => left - right);
}

export function buildDisplaySceneData(
  preparation: DisplayPreparationResponseDto | null,
  deps: BuildDependencies = defaultDependencies,
): DisplaySceneBuildResult {
  if (!preparation) {
    return {
      renderPath: 'fallback',
      sceneData: null,
      displayScene: null,
      missingFallbackFaceIds: [],
    };
  }

  const typedDisplayScene = mapDisplayPreparationToDisplayScene(preparation);

  if (preparation.lane === 'analytic-only') {
    return {
      renderPath: 'analytic-only',
      sceneData: typedDisplayScene?.renderables.length ? null : deps.mapAnalytic(preparation.analyticPacket),
      displayScene: typedDisplayScene,
      missingFallbackFaceIds: [],
    };
  }

  if (preparation.lane === 'mixed-fallback') {
    const analyticScene = deps.mapAnalytic(preparation.analyticPacket);
    const analyticFaceIds = new Set(preparation.analyticPacket.analyticFaces.map((face) => face.faceId));
    const fallbackFaceIds = new Set(preparation.analyticPacket.fallbackFaces.map((face) => face.faceId));

    if (!preparation.tessellationFallback) {
      return {
        renderPath: 'mixed-fallback',
        sceneData: typedDisplayScene?.renderables.length ? null : analyticScene,
        displayScene: typedDisplayScene,
        missingFallbackFaceIds: Array.from(fallbackFaceIds).sort((left, right) => left - right),
      };
    }

    const fallbackScene = deps.mapFallback(preparation.tessellationFallback);
    const filteredFallbackScene = filterFallbackFaces(fallbackScene, analyticFaceIds, fallbackFaceIds);
    const renderedFallbackFaceIds = new Set(filteredFallbackScene.faces.map((face) => face.faceId));

    return {
      renderPath: 'mixed-fallback',
      sceneData: typedDisplayScene?.renderables.length ? null : composeSceneData(analyticScene, filteredFallbackScene),
      displayScene: typedDisplayScene,
      missingFallbackFaceIds: getMissingFallbackFaceIds(fallbackFaceIds, renderedFallbackFaceIds),
    };
  }

  return {
    renderPath: 'fallback',
    sceneData: typedDisplayScene?.renderables.length ? null : (preparation.tessellationFallback ? deps.mapFallback(preparation.tessellationFallback) : null),
    displayScene: typedDisplayScene,
    missingFallbackFaceIds: [],
  };
}
