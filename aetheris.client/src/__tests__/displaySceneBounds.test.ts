import { describe, expect, it } from 'vitest';
import type { DisplayScene } from '../viewer/displayRenderables';
import { computeDisplaySceneBounds, computeOrthographicCameraFit } from '../viewer/displaySceneBounds';

const baseScene: DisplayScene = {
  renderables: [],
  sourceAuthority: 'BRep',
  displayAuthority: 'DisplayIR',
  lanes: [],
  diagnostics: [],
  status: 'Complete',
};

describe('displaySceneBounds', () => {
  it('computeDisplaySceneBounds_IncludesMeshRenderableVertices', () => {
    const displayScene: DisplayScene = {
      ...baseScene,
      renderables: [{
        kind: 'MeshPatch',
        faceId: 1,
        surfaceKind: 'Plane',
        status: 'Mesh',
        patchKind: 'MeshPatch',
        materializationLane: 'BoundedMesh',
        diagnostics: [],
        mesh: {
          faceId: 1,
          positions: new Float32Array([10, 20, 30, 40, 50, 60, 15, 25, 35]),
          normals: new Float32Array([0, 0, 1, 0, 0, 1, 0, 0, 1]),
          indices: new Uint32Array([0, 1, 2]),
        },
      }],
    };

    const bounds = computeDisplaySceneBounds(displayScene);

    expect(bounds.isValid).toBe(true);
    expect(bounds.min).toEqual([10, 20, 30]);
    expect(bounds.max).toEqual([40, 50, 60]);
  });

  it('computeDisplaySceneBounds_IncludesAnalyticPreviewVertices', () => {
    const displayScene: DisplayScene = {
      ...baseScene,
      renderables: [{
        kind: 'AnalyticPatch',
        faceId: 2,
        surfaceKind: 'Cylinder',
        status: 'Analytic',
        patchKind: 'AnalyticPatch',
        materializationLane: 'AnalyticPatch',
        diagnostics: [],
        previewMesh: {
          faceId: 2,
          positions: new Float32Array([-5, -2, -1, 7, 3, 11, 1, 0, 2]),
          normals: new Float32Array([0, 1, 0, 0, 1, 0, 0, 1, 0]),
          indices: new Uint32Array([0, 1, 2]),
        },
      }],
    };

    const bounds = computeDisplaySceneBounds(displayScene);

    expect(bounds.min).toEqual([-5, -2, -1]);
    expect(bounds.max).toEqual([7, 3, 11]);
  });

  it('computeDisplaySceneBounds_IncludesWirePatchPoints', () => {
    const displayScene: DisplayScene = {
      ...baseScene,
      renderables: [{
        kind: 'WirePatch',
        faceId: 3,
        surfaceKind: 'Plane',
        status: 'WireframeOnly',
        patchKind: 'WirePatch',
        materializationLane: 'WirePatch',
        diagnostics: [],
        wires: [{ edgeId: 30, points: new Float32Array([-20, 1, 4, 12, 6, 8, 0, -3, 5]) }],
      }],
    };

    const bounds = computeDisplaySceneBounds(displayScene);

    expect(bounds.min).toEqual([-20, -3, 4]);
    expect(bounds.max).toEqual([12, 6, 8]);
  });

  it('computeDisplaySceneBounds_IgnoresDiagnosticOnlyFacesWithoutProxy', () => {
    const displayScene: DisplayScene = {
      ...baseScene,
      renderables: [{
        kind: 'DiagnosticPatch',
        faceId: 4,
        surfaceKind: 'Plane',
        status: 'DiagnosticOnly',
        patchKind: 'DiagnosticPatch',
        materializationLane: 'DiagnosticOnly',
        diagnostics: [{ code: 'Viewer.Tessellation.Timeout', message: 'timeout', faceId: 4, surfaceKind: 'Plane', phase: 'phase', suggestedNextAction: null }],
      }],
    };

    const bounds = computeDisplaySceneBounds(displayScene);

    expect(bounds.isValid).toBe(false);
  });

  it('computeDisplaySceneBounds_ProducesValidLargeFarFromOriginBounds', () => {
    const displayScene: DisplayScene = {
      ...baseScene,
      renderables: [
        {
          kind: 'MeshPatch',
          faceId: 1,
          surfaceKind: 'Plane',
          status: 'Mesh',
          patchKind: 'MeshPatch',
          materializationLane: 'BoundedMesh',
          diagnostics: [],
          mesh: {
            faceId: 1,
            positions: new Float32Array([10000, -50, 25000, 10120, 60, 25140, 9980, 40, 25080]),
            normals: new Float32Array(9),
            indices: new Uint32Array([0, 1, 2]),
          },
        },
        {
          kind: 'WirePatch',
          faceId: 2,
          surfaceKind: 'Plane',
          status: 'WireframeOnly',
          patchKind: 'WirePatch',
          materializationLane: 'WirePatch',
          diagnostics: [],
          wires: [{ edgeId: 2, points: new Float32Array([9975, -55, 24990, 10130, 65, 25150]) }],
        },
        {
          kind: 'DiagnosticPatch',
          faceId: 3,
          surfaceKind: 'Plane',
          status: 'DiagnosticOnly',
          patchKind: 'DiagnosticPatch',
          materializationLane: 'DiagnosticOnly',
          diagnostics: [],
        },
      ],
    };

    const bounds = computeDisplaySceneBounds(displayScene);

    expect(bounds.isValid).toBe(true);
    expect(bounds.min).toEqual([9975, -55, 24990]);
    expect(bounds.max).toEqual([10130, 65, 25150]);
  });

  it('computeOrthographicCameraFit_FitsLargeFarFromOriginSceneBounds', () => {
    const displayScene: DisplayScene = {
      ...baseScene,
      renderables: [{
        kind: 'MeshPatch',
        faceId: 5,
        surfaceKind: 'Plane',
        status: 'Mesh',
        patchKind: 'MeshPatch',
        materializationLane: 'BoundedMesh',
        diagnostics: [],
        mesh: {
          faceId: 5,
          positions: new Float32Array([1000, 1000, 1000, 1100, 1000, 1000, 1000, 1200, 1000]),
          normals: new Float32Array([0, 0, 1, 0, 0, 1, 0, 0, 1]),
          indices: new Uint32Array([0, 1, 2]),
        },
      }],
    };

    const bounds = computeDisplaySceneBounds(displayScene);
    const fit = computeOrthographicCameraFit(bounds, 20, 20);

    expect(fit).not.toBeNull();
    expect(fit?.target).toEqual([1050, 1100, 1000]);
    expect(fit?.position[0]).toBeGreaterThan(1050);
    expect(fit?.position[1]).toBeGreaterThan(1100);
    expect(fit?.position[2]).toBeGreaterThan(1000);
    expect(fit?.zoom).toBeGreaterThan(0);
    expect(fit?.far).toBeGreaterThan(fit?.near ?? 0);
  });
});
