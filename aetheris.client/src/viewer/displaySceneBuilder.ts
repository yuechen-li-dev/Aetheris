import type { DisplayPreparationResponseDto } from '../api/aetherisApi';
import { mapAnalyticPacketToRenderData } from './analyticMapper';
import { mapTessellationToRenderData, type RenderSceneData } from './tessellationMapper';

export type DisplayRenderPath = 'analytic-only' | 'fallback';

export interface DisplaySceneBuildResult {
  renderPath: DisplayRenderPath;
  sceneData: RenderSceneData | null;
}

interface BuildDependencies {
  mapAnalytic: typeof mapAnalyticPacketToRenderData;
  mapFallback: typeof mapTessellationToRenderData;
}

const defaultDependencies: BuildDependencies = {
  mapAnalytic: mapAnalyticPacketToRenderData,
  mapFallback: mapTessellationToRenderData,
};

export function buildDisplaySceneData(
  preparation: DisplayPreparationResponseDto | null,
  deps: BuildDependencies = defaultDependencies,
): DisplaySceneBuildResult {
  if (!preparation) {
    return {
      renderPath: 'fallback',
      sceneData: null,
    };
  }

  if (preparation.lane === 'analytic-only') {
    return {
      renderPath: 'analytic-only',
      sceneData: deps.mapAnalytic(preparation.analyticPacket),
    };
  }

  return {
    renderPath: 'fallback',
    sceneData: preparation.tessellationFallback ? deps.mapFallback(preparation.tessellationFallback) : null,
  };
}
