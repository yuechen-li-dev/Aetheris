import { describe, expect, it } from 'vitest';
import { computeDisplaySceneBounds } from '../viewer/displaySceneBounds';
import type { DisplayScene } from '../viewer/displayRenderables';

const baseScene: Omit<DisplayScene, 'renderables'> = { status: 'Partial', sourceAuthority: 'BRep', displayAuthority: 'DisplayIR', lanes: [], diagnostics: [] };

describe('computeDisplaySceneBounds', () => {
  it('DisplaySceneBounds_StillFitsImportedFtc06Shape', () => {
    const scene: DisplayScene = {
      ...baseScene,
      renderables: [
        { kind: 'MeshPatch', faceId: 1, surfaceKind: 'Plane', status: 'Mesh', patchKind: 'MeshPatch', materializationLane: 'BoundedMesh', diagnostics: [], mesh: { faceId: 1, positions: new Float32Array([10000, -50, 25000, 10120, 60, 25140, 9980, 40, 25080]), normals: new Float32Array(9), indices: new Uint32Array([0, 1, 2]) } },
        { kind: 'WirePatch', faceId: 2, surfaceKind: 'Plane', status: 'WireframeOnly', patchKind: 'WirePatch', materializationLane: 'WirePatch', diagnostics: [], wires: [{ edgeId: 2, points: new Float32Array([9975, -55, 24990, 10130, 65, 25150]) }] },
        { kind: 'DiagnosticPatch', faceId: 3, surfaceKind: 'Plane', status: 'DiagnosticOnly', patchKind: 'DiagnosticPatch', materializationLane: 'DiagnosticOnly', diagnostics: [] },
      ],
    };

    expect(computeDisplaySceneBounds(scene)).toEqual({ minX: 9975, minY: -55, minZ: 24990, maxX: 10130, maxY: 65, maxZ: 25150 });
  });
});
