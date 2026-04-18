import type {
  AnalyticDisplayFaceDto,
  AnalyticDisplayPacketDto,
  Point3Dto,
  Vector3Dto,
} from '../api/aetherisApi';
import type { RenderFacePatch, RenderSceneData } from './tessellationMapper';

const TAU = Math.PI * 2;

function toArray(point: Point3Dto | Vector3Dto): [number, number, number] {
  return [point.x, point.y, point.z];
}

function add(a: [number, number, number], b: [number, number, number]): [number, number, number] {
  return [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
}

function mul(v: [number, number, number], s: number): [number, number, number] {
  return [v[0] * s, v[1] * s, v[2] * s];
}

function normalize(v: [number, number, number]): [number, number, number] {
  const len = Math.hypot(v[0], v[1], v[2]);
  if (len <= 1e-9) {
    return [0, 1, 0];
  }

  return [v[0] / len, v[1] / len, v[2] / len];
}

function signedArea2d(points: [number, number][]): number {
  let area = 0;
  for (let i = 0; i < points.length; i += 1) {
    const [x1, y1] = points[i];
    const [x2, y2] = points[(i + 1) % points.length];
    area += (x1 * y2) - (x2 * y1);
  }

  return area * 0.5;
}

function pointInTriangle(point: [number, number], a: [number, number], b: [number, number], c: [number, number]): boolean {
  const cross1 = ((b[0] - a[0]) * (point[1] - a[1])) - ((b[1] - a[1]) * (point[0] - a[0]));
  const cross2 = ((c[0] - b[0]) * (point[1] - b[1])) - ((c[1] - b[1]) * (point[0] - b[0]));
  const cross3 = ((a[0] - c[0]) * (point[1] - c[1])) - ((a[1] - c[1]) * (point[0] - c[0]));
  const hasNeg = cross1 < -1e-9 || cross2 < -1e-9 || cross3 < -1e-9;
  const hasPos = cross1 > 1e-9 || cross2 > 1e-9 || cross3 > 1e-9;
  return !(hasNeg && hasPos);
}

function triangulateSimplePolygon(points: [number, number][]): number[] | null {
  if (points.length < 3) {
    return null;
  }

  const orientation = Math.sign(signedArea2d(points));
  if (orientation === 0) {
    return null;
  }

  const indices = [...points.keys()];
  const triangles: number[] = [];
  let guard = 0;
  while (indices.length > 3 && guard < points.length * points.length) {
    let earFound = false;
    for (let i = 0; i < indices.length; i += 1) {
      const prev = indices[(i - 1 + indices.length) % indices.length];
      const curr = indices[i];
      const next = indices[(i + 1) % indices.length];
      const a = points[prev];
      const b = points[curr];
      const c = points[next];
      const cross = ((b[0] - a[0]) * (c[1] - a[1])) - ((b[1] - a[1]) * (c[0] - a[0]));
      if (cross * orientation <= 1e-9) {
        continue;
      }

      let hasInteriorPoint = false;
      for (let j = 0; j < indices.length; j += 1) {
        const candidate = indices[j];
        if (candidate === prev || candidate === curr || candidate === next) {
          continue;
        }

        if (pointInTriangle(points[candidate], a, b, c)) {
          hasInteriorPoint = true;
          break;
        }
      }

      if (hasInteriorPoint) {
        continue;
      }

      triangles.push(prev, curr, next);
      indices.splice(i, 1);
      earFound = true;
      break;
    }

    if (!earFound) {
      return null;
    }

    guard += 1;
  }

  if (indices.length === 3) {
    triangles.push(indices[0], indices[1], indices[2]);
    return triangles;
  }

  return null;
}

function buildPlanePatch(face: AnalyticDisplayFaceDto): RenderFacePatch | null {
  if (!face.planeGeometry) {
    return null;
  }

  const outerBoundary = face.planeGeometry.outerBoundary;
  if (!outerBoundary || outerBoundary.length < 3) {
    return null;
  }

  const normal = normalize(toArray(face.planeGeometry.normal));
  const origin = toArray(face.planeGeometry.origin);
  const uAxis = toArray(face.planeGeometry.uAxis);
  const vAxis = toArray(face.planeGeometry.vAxis);
  const points3d = outerBoundary.map(toArray);
  const points2d: [number, number][] = points3d.map((point) => {
    const rel: [number, number, number] = [point[0] - origin[0], point[1] - origin[1], point[2] - origin[2]];
    return [
      (rel[0] * uAxis[0]) + (rel[1] * uAxis[1]) + (rel[2] * uAxis[2]),
      (rel[0] * vAxis[0]) + (rel[1] * vAxis[1]) + (rel[2] * vAxis[2]),
    ];
  });

  const triangles = triangulateSimplePolygon(points2d);
  if (!triangles || triangles.length === 0) {
    return null;
  }

  return {
    faceId: face.faceId,
    positions: new Float32Array(points3d.flat()),
    normals: new Float32Array(points3d.flatMap(() => normal)),
    indices: new Uint32Array(triangles),
  };
}

function buildCylinderPatch(face: AnalyticDisplayFaceDto): RenderFacePatch | null {
  if (!face.cylinderGeometry) {
    return null;
  }

  const origin = toArray(face.cylinderGeometry.origin);
  const axis = toArray(face.cylinderGeometry.axis);
  const xAxis = toArray(face.cylinderGeometry.xAxis);
  const yAxis = toArray(face.cylinderGeometry.yAxis);
  const radius = face.cylinderGeometry.radius;
  const minV = face.domainHint?.minV ?? -0.5;
  const maxV = face.domainHint?.maxV ?? 0.5;
  const angularSegments = 24;

  const positions: number[] = [];
  const normals: number[] = [];
  const indices: number[] = [];

  for (let i = 0; i <= angularSegments; i += 1) {
    const u = (i / angularSegments) * TAU;
    const cosU = Math.cos(u);
    const sinU = Math.sin(u);
    const radial = add(mul(xAxis, radius * cosU), mul(yAxis, radius * sinU));
    const radialNormal = normalize(add(mul(xAxis, cosU), mul(yAxis, sinU)));

    const bottom = add(add(origin, mul(axis, minV)), radial);
    const top = add(add(origin, mul(axis, maxV)), radial);

    positions.push(...bottom, ...top);
    normals.push(...radialNormal, ...radialNormal);

    if (i < angularSegments) {
      const base = i * 2;
      indices.push(
        base,
        base + 1,
        base + 3,
        base,
        base + 3,
        base + 2,
      );
    }
  }

  return {
    faceId: face.faceId,
    positions: new Float32Array(positions),
    normals: new Float32Array(normals),
    indices: new Uint32Array(indices),
  };
}

function buildConePatch(face: AnalyticDisplayFaceDto): RenderFacePatch | null {
  if (!face.coneGeometry) {
    return null;
  }

  const apex = toArray(face.coneGeometry.apex);
  const axis = toArray(face.coneGeometry.axis);
  const xAxis = toArray(face.coneGeometry.xAxis);
  const yAxis = toArray(face.coneGeometry.yAxis);
  const minV = Math.max(face.domainHint?.minV ?? 0.1, 0.001);
  const maxV = Math.max(face.domainHint?.maxV ?? 1, minV + 0.001);
  const tanSemi = Math.tan(face.coneGeometry.semiAngleRadians);
  const angularSegments = 24;

  const positions: number[] = [];
  const normals: number[] = [];
  const indices: number[] = [];

  for (let i = 0; i <= angularSegments; i += 1) {
    const u = (i / angularSegments) * TAU;
    const cosU = Math.cos(u);
    const sinU = Math.sin(u);
    const radialDirection = normalize(add(mul(xAxis, cosU), mul(yAxis, sinU)));
    const lowerRadius = minV * tanSemi;
    const upperRadius = maxV * tanSemi;

    const lower = add(add(apex, mul(axis, minV)), mul(radialDirection, lowerRadius));
    const upper = add(add(apex, mul(axis, maxV)), mul(radialDirection, upperRadius));

    const normal = normalize([
      radialDirection[0] - axis[0] * tanSemi,
      radialDirection[1] - axis[1] * tanSemi,
      radialDirection[2] - axis[2] * tanSemi,
    ]);

    positions.push(...lower, ...upper);
    normals.push(...normal, ...normal);

    if (i < angularSegments) {
      const base = i * 2;
      indices.push(base, base + 1, base + 3, base, base + 3, base + 2);
    }
  }

  return {
    faceId: face.faceId,
    positions: new Float32Array(positions),
    normals: new Float32Array(normals),
    indices: new Uint32Array(indices),
  };
}

function buildSpherePatch(face: AnalyticDisplayFaceDto): RenderFacePatch | null {
  if (!face.sphereGeometry) {
    return null;
  }

  const center = toArray(face.sphereGeometry.center);
  const axis = toArray(face.sphereGeometry.axis);
  const xAxis = toArray(face.sphereGeometry.xAxis);
  const yAxis = toArray(face.sphereGeometry.yAxis);
  const radius = face.sphereGeometry.radius;
  const longitudinalSegments = 32;
  const latitudinalSegments = 16;

  const positions: number[] = [];
  const normals: number[] = [];
  const indices: number[] = [];

  for (let i = 0; i <= longitudinalSegments; i += 1) {
    const u = (i / longitudinalSegments) * TAU;
    const cosU = Math.cos(u);
    const sinU = Math.sin(u);

    for (let j = 0; j <= latitudinalSegments; j += 1) {
      const v = ((j / latitudinalSegments) * Math.PI) - (Math.PI * 0.5);
      const cosV = Math.cos(v);
      const sinV = Math.sin(v);

      const normal = normalize(add(add(mul(xAxis, cosV * cosU), mul(yAxis, cosV * sinU)), mul(axis, sinV)));
      const point = add(center, mul(normal, radius));
      positions.push(...point);
      normals.push(...normal);

      if (i < longitudinalSegments && j < latitudinalSegments) {
        const stride = latitudinalSegments + 1;
        const a = (i * stride) + j;
        const b = a + 1;
        const c = a + stride;
        const d = c + 1;
        indices.push(a, c, d, a, d, b);
      }
    }
  }

  return {
    faceId: face.faceId,
    positions: new Float32Array(positions),
    normals: new Float32Array(normals),
    indices: new Uint32Array(indices),
  };
}

function buildTorusPatch(face: AnalyticDisplayFaceDto): RenderFacePatch | null {
  if (!face.torusGeometry) {
    return null;
  }

  const center = toArray(face.torusGeometry.center);
  const axis = toArray(face.torusGeometry.axis);
  const xAxis = toArray(face.torusGeometry.xAxis);
  const yAxis = toArray(face.torusGeometry.yAxis);
  const majorRadius = face.torusGeometry.majorRadius;
  const minorRadius = face.torusGeometry.minorRadius;
  const majorSegments = 32;
  const minorSegments = 16;

  const positions: number[] = [];
  const normals: number[] = [];
  const indices: number[] = [];

  for (let i = 0; i <= majorSegments; i += 1) {
    const u = (i / majorSegments) * TAU;
    const cosU = Math.cos(u);
    const sinU = Math.sin(u);
    const majorDirection = normalize(add(mul(xAxis, cosU), mul(yAxis, sinU)));

    for (let j = 0; j <= minorSegments; j += 1) {
      const v = (j / minorSegments) * TAU;
      const cosV = Math.cos(v);
      const sinV = Math.sin(v);

      const normal = normalize(add(mul(majorDirection, cosV), mul(axis, sinV)));
      const ringCenter = add(center, mul(majorDirection, majorRadius));
      const point = add(ringCenter, mul(normal, minorRadius));
      positions.push(...point);
      normals.push(...normal);

      if (i < majorSegments && j < minorSegments) {
        const stride = minorSegments + 1;
        const a = (i * stride) + j;
        const b = a + 1;
        const c = a + stride;
        const d = c + 1;
        indices.push(a, c, d, a, d, b);
      }
    }
  }

  return {
    faceId: face.faceId,
    positions: new Float32Array(positions),
    normals: new Float32Array(normals),
    indices: new Uint32Array(indices),
  };
}

function mapFace(face: AnalyticDisplayFaceDto): RenderFacePatch | null {
  if (face.surfaceKind === 'Plane') {
    return buildPlanePatch(face);
  }

  if (face.surfaceKind === 'Cylinder') {
    return buildCylinderPatch(face);
  }

  if (face.surfaceKind === 'Cone') {
    return buildConePatch(face);
  }

  if (face.surfaceKind === 'Sphere') {
    return buildSpherePatch(face);
  }

  if (face.surfaceKind === 'Torus') {
    return buildTorusPatch(face);
  }

  return null;
}

export function mapAnalyticPacketToRenderData(packet: AnalyticDisplayPacketDto): RenderSceneData {
  const faces = packet.analyticFaces
    .map(mapFace)
    .filter((patch): patch is RenderFacePatch => patch !== null);

  return {
    faces,
    edges: [],
  };
}
