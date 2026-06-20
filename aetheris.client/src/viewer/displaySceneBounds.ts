import type { DisplayScene } from './displayRenderables';

export interface DisplaySceneBounds {
  minX: number;
  minY: number;
  minZ: number;
  maxX: number;
  maxY: number;
  maxZ: number;
}

function includePoint(bounds: DisplaySceneBounds, x: number, y: number, z: number): void {
  bounds.minX = Math.min(bounds.minX, x);
  bounds.minY = Math.min(bounds.minY, y);
  bounds.minZ = Math.min(bounds.minZ, z);
  bounds.maxX = Math.max(bounds.maxX, x);
  bounds.maxY = Math.max(bounds.maxY, y);
  bounds.maxZ = Math.max(bounds.maxZ, z);
}

function includeArray(bounds: DisplaySceneBounds, points: Float32Array): void {
  for (let index = 0; index + 2 < points.length; index += 3) {
    includePoint(bounds, points[index], points[index + 1], points[index + 2]);
  }
}

export function computeDisplaySceneBounds(scene: DisplayScene | null): DisplaySceneBounds | null {
  if (!scene) return null;

  const bounds: DisplaySceneBounds = {
    minX: Number.POSITIVE_INFINITY,
    minY: Number.POSITIVE_INFINITY,
    minZ: Number.POSITIVE_INFINITY,
    maxX: Number.NEGATIVE_INFINITY,
    maxY: Number.NEGATIVE_INFINITY,
    maxZ: Number.NEGATIVE_INFINITY,
  };

  for (const renderable of scene.renderables) {
    if (renderable.kind === 'AnalyticPatch') {
      includeArray(bounds, renderable.previewMesh.positions);
    } else if (renderable.kind === 'MeshPatch') {
      includeArray(bounds, renderable.mesh.positions);
    } else if (renderable.kind === 'WirePatch') {
      for (const wire of renderable.wires) includeArray(bounds, wire.points);
    }
  }

  return Number.isFinite(bounds.minX) ? bounds : null;
}
