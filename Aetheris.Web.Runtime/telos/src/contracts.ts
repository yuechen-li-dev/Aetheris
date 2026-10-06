export type Vec3 = readonly [number, number, number];
export type Pixel = readonly [number, number];
export type NumericArray = ArrayLike<number>;
export interface TelosIdentity {
  occurrenceId: string;
  bodyId?: string;
  definitionId?: string;
  faceId?: string | number;
  edgeId?: string | number;
}
export interface TelosMaterial {
  baseColor: readonly number[];
  roughness: number;
  metallic: number;
  opacity: number;
}
export interface MeshRange {
  startTriangle: number;
  triangleCount: number;
  faceId: string | number;
}
export interface TelosGeometry {
  id: string;
  positions: NumericArray;
  normals: NumericArray;
  indices: NumericArray;
  ranges?: readonly MeshRange[];
}
export interface TelosMesh {
  definition: TelosGeometry;
  identity: TelosIdentity;
  transform?: NumericArray;
  material: TelosMaterial;
  selected?: boolean;
  hovered?: boolean;
  overlay?: boolean;
  visible?: boolean;
}
export interface TelosLine {
  id: string;
  points: NumericArray;
  identity: TelosIdentity;
  transform?: NumericArray;
  widthPixels?: number;
  color?: readonly number[];
  selected?: boolean;
  overlay?: boolean;
  visible?: boolean;
}
export interface TelosShaderArtifact {
  shaderId: string;
  wgsl: string;
  vertexEntryPoint: string;
  fragmentEntryPoint: string;
  bindings: readonly {
    group: number;
    binding: number;
    kind: "uniform";
    byteSize: number;
  }[];
  capabilities: readonly string[];
  sourceIdentity: string;
}
/** X0 field ABI: clip, local near/delta, depth/w rows, epsilon; canonical 32-byte tint/roughness uniform. Rigid occurrences only. */
export interface TelosField {
  artifact: TelosShaderArtifact;
  identity: TelosIdentity;
  bounds: { minimum: Vec3; maximum: Vec3 };
  transform?: NumericArray;
  material: TelosMaterial;
  proxy?: TelosGeometry;
  selected?: boolean;
}
export interface TelosScene {
  meshes: readonly TelosMesh[];
  lines: readonly TelosLine[];
  fields: readonly TelosField[];
}
export interface TelosHit extends TelosIdentity {
  triangleIndex?: number;
  worldPosition: number[];
  worldNormal: number[] | null;
  distance: number;
}
export interface DisplayPacket {
  definitions: readonly (TelosGeometry & {
    edges?: readonly {
      edgeId: string;
      points: readonly (readonly number[])[];
      closed: boolean;
    }[];
  })[];
  occurrences: readonly {
    id: string;
    definitionId?: string;
    semanticEntityId: string;
    transform: NumericArray;
  }[];
}
