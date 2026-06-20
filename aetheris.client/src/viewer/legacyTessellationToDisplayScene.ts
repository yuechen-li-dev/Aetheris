import type { TessellationResponseDto } from '../api/aetherisApi';
import { mapFacePatchToRenderFacePatch } from './tessellationMapper';
import type { DisplayRenderable, DisplayScene, MeshRenderable } from './displayRenderables';

// Compatibility adapter for pre-DisplayIR /tessellate-style payloads only.
// New viewer code should consume DisplayScene renderables, not treat tessellation
// face patches as scene authority.
export function legacyFacePatchToMeshRenderable(patch: TessellationResponseDto['facePatches'][number]): MeshRenderable {
  return {
    kind: 'MeshPatch',
    faceId: patch.faceId,
    surfaceKind: null,
    status: 'Mesh',
    patchKind: 'MeshPatch',
    materializationLane: 'BoundedMesh',
    diagnostics: [],
    mesh: mapFacePatchToRenderFacePatch(patch),
  };
}

export function legacyTessellationToDisplayScene(tessellation: TessellationResponseDto): DisplayScene {
  const meshRenderables: DisplayRenderable[] = tessellation.facePatches.map(legacyFacePatchToMeshRenderable);

  return {
    status: meshRenderables.length > 0 ? 'Complete' : 'Partial',
    sourceAuthority: 'LegacyTessellation',
    displayAuthority: 'LegacyCompatibilityAdapter',
    lanes: [],
    renderables: meshRenderables,
    diagnostics: [],
    legacyCompatibility: {
      source: 'tessellationFallback',
      facePatchCount: tessellation.facePatches.length,
      edgePolylineCount: tessellation.edgePolylines.length,
    },
  };
}
