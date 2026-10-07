export type Vec3 = readonly [number, number, number];
export type Pixel = readonly [number, number];
export type NumericArray = ArrayLike<number>;
/** Coordinates are world space; line thickness and projected DOM placements use CSS pixels. */
export type TelosDepthMode = "depth-tested" | "depth-biased" | "always-on-top";
export interface TelosIdentity {
  occurrenceId: string;
  bodyId?: string;
  definitionId?: string;
  faceId?: string | number;
  edgeId?: string | number;
  overlayId?: string;
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
  /** Immutable geometry content identity supplied by the compiler/projector. */
  geometryRevision?: string | null;
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
  depthMode?: TelosDepthMode;
  unlit?: boolean;
  /** Draw/pick a source triangle range while sharing the complete immutable geometry. */
  triangleRange?: { startTriangle: number; triangleCount: number };
}
export interface TelosLine {
  /** Fade finite reference-grid endpoints without changing engineering edge coverage. */
  fadeEnds?: boolean;
  id: string;
  points: NumericArray;
  identity: TelosIdentity;
  transform?: NumericArray;
  widthPixels?: number;
  color?: readonly number[];
  selected?: boolean;
  overlay?: boolean;
  visible?: boolean;
  depthMode?: TelosDepthMode;
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
/** Rays/1 uses 72-byte vertices; rays/2 adds metallic at location 6 (76 bytes).
 * Both retain the canonical 32-byte tint/roughness uniform. Rigid occurrences only. */
export interface TelosField {
  artifact: TelosShaderArtifact;
  identity: TelosIdentity;
  bounds: { minimum: Vec3; maximum: Vec3 };
  transform?: NumericArray;
  material: TelosMaterial;
  proxy?: TelosGeometry;
  visible?: boolean;
  /** Normal mesh fallback while executable GPU state is pending or rejected. */
  fallback?: TelosMesh;
  selected?: boolean;
}
export interface TelosScene {
  meshes: readonly TelosMesh[];
  lines: readonly TelosLine[];
  fields: readonly TelosField[];
  projectionDiagnostics?: readonly {
    definitionId: string;
    qualification?: string;
    status: string;
    shaderId?: string;
    reason?: string | null;
  }[];
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
    cir?: {
      qualification: string;
      structuralIdentity?: string | null;
      minimumMm?: readonly number[] | null;
      maximumMm?: readonly number[] | null;
    } | null;
    shader?: {
      artifact?: TelosShaderArtifact | null;
      status: string;
      reason?: string | null;
    } | null;
  })[];
  occurrences: readonly {
    id: string;
    definitionId?: string;
    semanticEntityId: string;
    transform: NumericArray;
    material?: TelosMaterial | null;
  }[];
}
