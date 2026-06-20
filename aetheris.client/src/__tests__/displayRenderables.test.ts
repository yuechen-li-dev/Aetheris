import { describe, expect, it } from 'vitest';
import type { DisplayPreparationResponseDto } from '../api/aetherisApi';
import { mapDisplayFaceToRenderable, mapDisplayPreparationToDisplayScene } from '../viewer/displayRenderables';

const diagnostic = { code: 'Viewer.Test', message: 'test', faceId: 4, surfaceKind: 'Plane', phase: 'test', suggestedNextAction: null };

function prepWithFaces(faces: DisplayPreparationResponseDto['faces']): DisplayPreparationResponseDto {
  return {
    lane: 'fallback-only',
    analyticPacket: { bodyId: 1, analyticFaces: [], fallbackFaces: [] },
    tessellationFallback: null,
    status: 'Partial',
    sourceAuthority: 'BRep',
    displayAuthority: 'DisplayIR',
    lanes: ['AnalyticPatch', 'BoundedMesh', 'WirePatch', 'DiagnosticOnly'],
    displayLanes: [],
    diagnostics: [],
    faces,
  };
}

describe('DisplayIRMapper', () => {
  it('DoesNotFlattenDiagnosticsIntoMesh', () => {
    const renderable = mapDisplayFaceToRenderable({
      faceId: 4,
      shellId: 1,
      surfaceKind: 'Plane',
      status: 'DiagnosticOnly',
      patchKind: 'DiagnosticPatch',
      meshPatch: null,
      analyticPatch: null,
      wirePatch: null,
      materializationLane: 'DiagnosticOnly',
      diagnostics: [diagnostic],
    });

    expect(renderable.kind).toBe('DiagnosticPatch');
    expect('mesh' in renderable).toBe(false);
    expect(renderable.diagnostics).toEqual([diagnostic]);
  });

  it('PreservesMaterializationLane', () => {
    const scene = mapDisplayPreparationToDisplayScene(prepWithFaces([
      {
        faceId: 1, shellId: 1, surfaceKind: 'Plane', status: 'Analytic', patchKind: 'AnalyticPatch', materializationLane: 'AnalyticPatch', diagnostics: [], meshPatch: null, wirePatch: null,
        analyticPatch: { faceId: 1, shellId: 1, shellRole: 'Outer', surfaceGeometryId: 1, surfaceKind: 'Plane', loopCount: 1, domainHint: null, cylinderGeometry: null, coneGeometry: null, sphereGeometry: null, torusGeometry: null, planeGeometry: { origin: { x: 0, y: 0, z: 0 }, normal: { x: 0, y: 0, z: 1 }, uAxis: { x: 1, y: 0, z: 0 }, vAxis: { x: 0, y: 1, z: 0 }, outerBoundary: [{ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }, { x: 0, y: 1, z: 0 }] } },
      },
      {
        faceId: 2, shellId: 1, surfaceKind: 'Plane', status: 'Mesh', patchKind: 'MeshPatch', materializationLane: 'BoundedMesh', diagnostics: [], analyticPatch: null, wirePatch: null,
        meshPatch: { faceId: 2, positions: [{ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }, { x: 0, y: 1, z: 0 }], normals: [{ x: 0, y: 0, z: 1 }, { x: 0, y: 0, z: 1 }, { x: 0, y: 0, z: 1 }], triangleIndices: [0, 1, 2] },
      },
      {
        faceId: 3, shellId: 1, surfaceKind: 'Plane', status: 'WireframeOnly', patchKind: 'WirePatch', materializationLane: 'WirePatch', diagnostics: [], meshPatch: null, analyticPatch: null,
        wirePatch: { kind: 'WirePatch', source: 'BRepEdges', quality: 'PreviewPolyline', loops: [{ loopId: 1, role: 'Outer', edges: [{ edgeId: 30, points: [{ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }], sourceCurveKind: 'Line3', sampleCount: 2, diagnostics: [] }] }] },
      },
      { faceId: 4, shellId: 1, surfaceKind: 'Plane', status: 'DiagnosticOnly', patchKind: 'DiagnosticPatch', materializationLane: 'DiagnosticOnly', diagnostics: [diagnostic], meshPatch: null, analyticPatch: null, wirePatch: null },
    ]));

    expect(scene?.renderables.map((r) => [r.kind, r.materializationLane])).toEqual([
      ['AnalyticPatch', 'AnalyticPatch'],
      ['MeshPatch', 'BoundedMesh'],
      ['WirePatch', 'WirePatch'],
      ['DiagnosticPatch', 'DiagnosticOnly'],
    ]);
    expect(scene?.sourceAuthority).toBe('BRep');
    expect(scene?.displayAuthority).toBe('DisplayIR');
    expect(scene?.status).toBe('Partial');
  });

  it('PrefersFallbackMeshForAnalyticPlanesWithInnerLoops', () => {
    const scene = mapDisplayPreparationToDisplayScene({
      ...prepWithFaces([
        {
          faceId: 8,
          shellId: 1,
          surfaceKind: 'Plane',
          status: 'Analytic',
          patchKind: 'AnalyticPatch',
          materializationLane: 'AnalyticPatch',
          diagnostics: [],
          wirePatch: null,
          meshPatch: null,
          analyticPatch: {
            faceId: 8,
            shellId: 1,
            shellRole: 'Outer',
            surfaceGeometryId: 10,
            surfaceKind: 'Plane',
            loopCount: 3,
            domainHint: null,
            cylinderGeometry: null,
            coneGeometry: null,
            sphereGeometry: null,
            torusGeometry: null,
            planeGeometry: {
              origin: { x: 0, y: 0, z: 0 },
              normal: { x: 0, y: 0, z: 1 },
              uAxis: { x: 1, y: 0, z: 0 },
              vAxis: { x: 0, y: 1, z: 0 },
              outerBoundary: [{ x: 0, y: 0, z: 0 }, { x: 4, y: 0, z: 0 }, { x: 4, y: 4, z: 0 }, { x: 0, y: 4, z: 0 }],
            },
          },
        },
      ]),
      tessellationFallback: {
        facePatches: [
          {
            faceId: 8,
            positions: [{ x: 0, y: 0, z: 0 }, { x: 4, y: 0, z: 0 }, { x: 4, y: 4, z: 0 }],
            normals: [{ x: 0, y: 0, z: 1 }, { x: 0, y: 0, z: 1 }, { x: 0, y: 0, z: 1 }],
            triangleIndices: [0, 1, 2],
          },
        ],
        edgePolylines: [],
      },
    });

    expect(scene?.renderables).toHaveLength(1);
    expect(scene?.renderables[0].kind).toBe('MeshPatch');
    expect(scene?.renderables[0].patchKind).toBe('MeshPatch');
    expect(scene?.renderables[0].materializationLane).toBe('BoundedMesh');
    expect(scene?.renderables[0].status).toBe('Mesh');
  });

  it('PrefersFallbackMeshForAnalyticCylindersWhenAvailable', () => {
    const scene = mapDisplayPreparationToDisplayScene({
      ...prepWithFaces([
        {
          faceId: 12,
          shellId: 1,
          surfaceKind: 'Cylinder',
          status: 'Analytic',
          patchKind: 'AnalyticPatch',
          materializationLane: 'AnalyticPatch',
          diagnostics: [],
          wirePatch: null,
          meshPatch: null,
          analyticPatch: {
            faceId: 12,
            shellId: 1,
            shellRole: 'Outer',
            surfaceGeometryId: 14,
            surfaceKind: 'Cylinder',
            loopCount: 1,
            domainHint: { minV: 0, maxV: 2 },
            planeGeometry: null,
            coneGeometry: null,
            sphereGeometry: null,
            torusGeometry: null,
            cylinderGeometry: {
              origin: { x: 0, y: 0, z: 0 },
              axis: { x: 0, y: 0, z: 1 },
              xAxis: { x: 1, y: 0, z: 0 },
              yAxis: { x: 0, y: 1, z: 0 },
              radius: 1,
            },
          },
        },
      ]),
      tessellationFallback: {
        facePatches: [
          {
            faceId: 12,
            positions: [{ x: 1, y: 0, z: 0 }, { x: 0, y: 1, z: 0 }, { x: 1, y: 0, z: 2 }],
            normals: [{ x: 1, y: 0, z: 0 }, { x: 0, y: 1, z: 0 }, { x: 1, y: 0, z: 0 }],
            triangleIndices: [0, 1, 2],
          },
        ],
        edgePolylines: [],
      },
    });

    expect(scene?.renderables).toHaveLength(1);
    expect(scene?.renderables[0].kind).toBe('MeshPatch');
    expect(scene?.renderables[0].patchKind).toBe('MeshPatch');
    expect(scene?.renderables[0].materializationLane).toBe('BoundedMesh');
    expect(scene?.renderables[0].status).toBe('Mesh');
  });
});
