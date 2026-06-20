import type { DisplayDiagnosticDto, DisplayFaceDto, DisplayLaneDto, DisplayPreparationResponseDto } from '../api/aetherisApi';
import { analyticPatchToPreviewMesh } from './analyticMapper';
import { mapFacePatchToRenderFacePatch, type RenderEdgePolyline, type RenderFacePatch } from './tessellationMapper';

export type DisplayRenderableKind = 'AnalyticPatch' | 'MeshPatch' | 'WirePatch' | 'DiagnosticPatch';

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
  kind: 'AnalyticPatch';
  previewMesh: RenderFacePatch;
}

export interface MeshRenderable extends DisplayRenderableBase {
  kind: 'MeshPatch';
  mesh: RenderFacePatch;
}

export interface WireRenderable extends DisplayRenderableBase {
  kind: 'WirePatch';
  wires: RenderEdgePolyline[];
}

export interface DiagnosticRenderable extends DisplayRenderableBase {
  kind: 'DiagnosticPatch';
}

export type DisplayRenderable = AnalyticRenderable | MeshRenderable | WireRenderable | DiagnosticRenderable;

export interface DisplayScene {
  renderables: DisplayRenderable[];
  sourceAuthority: string | null;
  displayAuthority: string | null;
  displayLanes: DisplayLaneDto[];
  displayStatus: string | null;
}

function base(face: DisplayFaceDto): Omit<DisplayRenderableBase, 'kind'> {
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
  return face.wirePatch?.loops.flatMap((loop) => loop.edges)
    .filter((edge) => edge.points.length >= 2)
    .map((edge) => ({
      edgeId: edge.edgeId,
      points: new Float32Array(edge.points.flatMap((point) => [point.x, point.y, point.z])),
    })) ?? [];
}

export function mapDisplayFaceToRenderable(face: DisplayFaceDto): DisplayRenderable {
  if (face.patchKind === 'AnalyticPatch' && face.analyticPatch) {
    const previewMesh = analyticPatchToPreviewMesh(face.analyticPatch);
    if (previewMesh) {
      return { ...base(face), kind: 'AnalyticPatch', previewMesh };
    }
  }

  if ((face.patchKind === 'MeshPatch' || face.materializationLane === 'BoundedMesh') && face.meshPatch) {
    return { ...base(face), kind: 'MeshPatch', mesh: mapFacePatchToRenderFacePatch(face.meshPatch) };
  }

  if (face.patchKind === 'WirePatch' || face.status === 'WireframeOnly' || face.materializationLane === 'WirePatch') {
    return { ...base(face), kind: 'WirePatch', wires: wireEdges(face) };
  }

  return { ...base(face), kind: 'DiagnosticPatch' };
}

export function mapDisplayPreparationToDisplayScene(preparation: DisplayPreparationResponseDto | null): DisplayScene | null {
  if (!preparation) return null;
  return {
    renderables: (preparation.faces ?? []).map(mapDisplayFaceToRenderable),
    sourceAuthority: preparation.sourceAuthority ?? null,
    displayAuthority: preparation.displayAuthority ?? null,
    displayLanes: preparation.displayLanes ?? [],
    displayStatus: preparation.status ?? null,
  };
}
