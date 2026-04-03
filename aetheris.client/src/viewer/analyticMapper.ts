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

function buildPlanePatch(face: AnalyticDisplayFaceDto): RenderFacePatch | null {
  if (!face.planeGeometry) {
    return null;
  }

  const origin = toArray(face.planeGeometry.origin);
  const normal = normalize(toArray(face.planeGeometry.normal));
  const uAxis = normalize(toArray(face.planeGeometry.uAxis));
  const vAxis = normalize(toArray(face.planeGeometry.vAxis));
  const halfSize = 0.5;

  const corners: [number, number, number][] = [
    add(add(origin, mul(uAxis, -halfSize)), mul(vAxis, -halfSize)),
    add(add(origin, mul(uAxis, halfSize)), mul(vAxis, -halfSize)),
    add(add(origin, mul(uAxis, halfSize)), mul(vAxis, halfSize)),
    add(add(origin, mul(uAxis, -halfSize)), mul(vAxis, halfSize)),
  ];

  return {
    faceId: face.faceId,
    positions: new Float32Array(corners.flat()),
    normals: new Float32Array([normal, normal, normal, normal].flat()),
    indices: new Uint32Array([0, 1, 2, 0, 2, 3]),
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
