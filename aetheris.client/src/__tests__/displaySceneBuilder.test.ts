import { describe, expect, it, vi } from 'vitest';
import type { DisplayPreparationResponseDto } from '../api/aetherisApi';
import { buildDisplaySceneData } from '../viewer/displaySceneBuilder';
import { mapAnalyticPacketToRenderData } from '../viewer/analyticMapper';

const analyticPreparation: DisplayPreparationResponseDto = {
  lane: 'analytic-only',
  analyticPacket: {
    bodyId: 1,
    analyticFaces: [
      {
        faceId: 10,
        shellId: 1,
        shellRole: 'Outer',
        surfaceGeometryId: 100,
        surfaceKind: 'Cylinder',
        loopCount: 1,
        domainHint: { minV: -1, maxV: 1 },
        planeGeometry: null,
        cylinderGeometry: {
          origin: { x: 0, y: 0, z: 0 },
          axis: { x: 0, y: 1, z: 0 },
          xAxis: { x: 1, y: 0, z: 0 },
          yAxis: { x: 0, y: 0, z: 1 },
          radius: 0.5,
        },
        coneGeometry: null,
      },
    ],
    fallbackFaces: [],
  },
  tessellationFallback: {
    facePatches: [
      {
        faceId: 999,
        positions: [{ x: 0, y: 0, z: 0 }],
        normals: [{ x: 0, y: 1, z: 0 }],
        triangleIndices: [0, 0, 0],
      },
    ],
    edgePolylines: [],
  },
};

const fallbackPreparation: DisplayPreparationResponseDto = {
  lane: 'fallback-only',
  analyticPacket: {
    bodyId: 1,
    analyticFaces: [],
    fallbackFaces: [
      {
        faceId: 3,
        shellId: 1,
        shellRole: 'Outer',
        reason: 'UnsupportedSurfaceKind',
        surfaceKind: 'BSplineSurfaceWithKnots',
        detail: null,
      },
    ],
  },
  tessellationFallback: {
    facePatches: [
      {
        faceId: 100,
        positions: [
          { x: 0, y: 0, z: 0 },
          { x: 1, y: 0, z: 0 },
          { x: 0, y: 1, z: 0 },
        ],
        normals: [
          { x: 0, y: 0, z: 1 },
          { x: 0, y: 0, z: 1 },
          { x: 0, y: 0, z: 1 },
        ],
        triangleIndices: [0, 1, 2],
      },
    ],
    edgePolylines: [],
  },
};

describe('buildDisplaySceneData', () => {
  it('routes analytic-only lane to analytic mapping and does not touch fallback mapping', () => {
    const mapAnalytic = vi.fn().mockReturnValue({ faces: [], edges: [] });
    const mapFallback = vi.fn().mockReturnValue({ faces: [], edges: [] });

    const result = buildDisplaySceneData(analyticPreparation, { mapAnalytic, mapFallback });

    expect(result.renderPath).toBe('analytic-only');
    expect(mapAnalytic).toHaveBeenCalledOnce();
    expect(mapFallback).not.toHaveBeenCalled();
  });

  it('routes fallback-only lane to tessellation fallback mapping', () => {
    const mapAnalytic = vi.fn().mockReturnValue({ faces: [], edges: [] });
    const mapFallback = vi.fn().mockReturnValue({ faces: [], edges: [] });

    const result = buildDisplaySceneData(fallbackPreparation, { mapAnalytic, mapFallback });

    expect(result.renderPath).toBe('fallback');
    expect(mapFallback).toHaveBeenCalledOnce();
    expect(mapAnalytic).not.toHaveBeenCalled();
  });

  it('routes mixed-fallback lane to tessellation fallback mapping', () => {
    const mapAnalytic = vi.fn().mockReturnValue({ faces: [], edges: [] });
    const mapFallback = vi.fn().mockReturnValue({ faces: [], edges: [] });

    const result = buildDisplaySceneData({ ...fallbackPreparation, lane: 'mixed-fallback' }, { mapAnalytic, mapFallback });

    expect(result.renderPath).toBe('fallback');
    expect(mapFallback).toHaveBeenCalledOnce();
    expect(mapAnalytic).not.toHaveBeenCalled();
  });
});

describe('mapAnalyticPacketToRenderData', () => {
  it('is deterministic for the same analytic packet input', () => {
    const first = mapAnalyticPacketToRenderData(analyticPreparation.analyticPacket);
    const second = mapAnalyticPacketToRenderData(analyticPreparation.analyticPacket);

    expect(first.faces).toHaveLength(second.faces.length);
    expect(Array.from(first.faces[0].positions)).toEqual(Array.from(second.faces[0].positions));
    expect(Array.from(first.faces[0].normals)).toEqual(Array.from(second.faces[0].normals));
    expect(Array.from(first.faces[0].indices)).toEqual(Array.from(second.faces[0].indices));
  });
});
