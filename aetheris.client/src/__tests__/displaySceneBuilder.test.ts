import { describe, expect, it } from 'vitest';
import type { DisplayPreparationResponseDto } from '../api/aetherisApi';
import { buildDisplaySceneData } from '../viewer/displaySceneBuilder';

const legacyFallback = {
  facePatches: [{ faceId: 100, positions: [{ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }, { x: 0, y: 1, z: 0 }], normals: [{ x: 0, y: 0, z: 1 }, { x: 0, y: 0, z: 1 }, { x: 0, y: 0, z: 1 }], triangleIndices: [0, 1, 2] }],
  edgePolylines: [],
};

function prep(overrides: Partial<DisplayPreparationResponseDto>): DisplayPreparationResponseDto {
  return {
    lane: 'fallback-only',
    analyticPacket: { bodyId: 1, analyticFaces: [], fallbackFaces: [] },
    tessellationFallback: null,
    status: 'Complete',
    sourceAuthority: 'BRep',
    displayAuthority: 'DisplayIR',
    lanes: [],
    displayLanes: [],
    diagnostics: [],
    faces: [],
    ...overrides,
  };
}

describe('buildDisplaySceneData', () => {
  it('DisplaySceneBuilder_PrefersTypedDisplayIRFaces', () => {
    const result = buildDisplaySceneData(prep({
      lane: 'mixed-fallback',
      analyticPacket: { bodyId: 1, analyticFaces: [], fallbackFaces: [{ faceId: 100, shellId: 1, shellRole: 'Outer', reason: 'UnsupportedSurfaceKind', surfaceKind: 'BSplineSurfaceWithKnots', detail: null }] },
      tessellationFallback: legacyFallback,
      faces: [{ faceId: 7, shellId: 1, surfaceKind: 'Plane', status: 'Mesh', patchKind: 'MeshPatch', materializationLane: 'BoundedMesh', diagnostics: [], analyticPatch: null, wirePatch: null, meshPatch: legacyFallback.facePatches[0] }],
    }));

    expect(result.displayScene?.renderables.map((renderable) => renderable.faceId)).toEqual([7]);
    expect(result.displayScene?.legacyCompatibility).toBeUndefined();
  });

  it('DisplaySceneBuilder_UsesLegacyTessellationOnlyWhenNoTypedFaces', () => {
    const result = buildDisplaySceneData(prep({ tessellationFallback: legacyFallback }));

    expect(result.renderPath).toBe('fallback');
    expect(result.displayScene?.displayAuthority).toBe('LegacyCompatibilityAdapter');
    expect(result.displayScene?.renderables).toHaveLength(1);
  });

  it('DisplaySceneBuilder_LegacyFallbackProducesMeshRenderables', () => {
    const result = buildDisplaySceneData(prep({ tessellationFallback: legacyFallback }));

    expect(result.displayScene?.renderables[0].kind).toBe('MeshPatch');
    expect(result.displayScene?.renderables[0].materializationLane).toBe('BoundedMesh');
    expect(result.displayScene?.legacyCompatibility).toEqual({ source: 'tessellationFallback', facePatchCount: 1, edgePolylineCount: 0 });
  });

  it('reports missing mixed-fallback mesh faces from the primary DisplayScene', () => {
    const result = buildDisplaySceneData(prep({
      lane: 'mixed-fallback',
      analyticPacket: { bodyId: 1, analyticFaces: [], fallbackFaces: [{ faceId: 101, shellId: 1, shellRole: 'Outer', reason: 'UnsupportedSurfaceKind', surfaceKind: 'BSplineSurfaceWithKnots', detail: null }] },
      tessellationFallback: legacyFallback,
    }));

    expect(result.renderPath).toBe('mixed-fallback');
    expect(result.missingFallbackFaceIds).toEqual([101]);
  });
});
