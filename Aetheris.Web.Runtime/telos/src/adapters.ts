import { Matrix4, Color } from "three";
import type { Object3D, Mesh, MeshStandardMaterial } from "three";
import type {
  TelosIdentity,
  TelosScene,
  TelosMesh,
  TelosLine,
  DisplayPacket,
  TelosMaterial,
} from "./contracts.js";

export const identityTransform = () => new Matrix4().elements;
/** Clear colors are display encoded; mesh material colors are linear lighting inputs. */
export const clearColor = (color: string | number): [number, number, number, number] => {
  const encoded = new Color(color).convertLinearToSRGB();
  return [encoded.r, encoded.g, encoded.b, 1];
};
export function materialFromAppearance(
  appearance: {
    baseColor?: string | number | readonly number[];
    color?: string | number;
    roughness?: number;
    metallic?: number;
    metalness?: number;
    opacity?: number;
  } = {},
): TelosMaterial {
  const color = appearance.baseColor ?? appearance.color ?? "#b8beb3";
  return {
    baseColor:
      typeof color === "string" || typeof color === "number"
        ? new Color(color).toArray()
        : Array.from(color),
    roughness: appearance.roughness ?? 0.62,
    metallic: appearance.metallic ?? appearance.metalness ?? 0.15,
    opacity: appearance.opacity ?? 1,
  };
}
/** SDK display packets retain definition/occurrence/topology authority. No tessellation-derived edges. */
export function fromDisplayMesh(
  packet: DisplayPacket,
  appearance?: Parameters<typeof materialFromAppearance>[0],
): TelosScene {
  const definitions = new Map(
    packet.definitions.map((definition) => [definition.id, definition]),
  );
  const meshes: TelosMesh[] = [],
    lines: TelosLine[] = [];
  for (const occurrence of packet.occurrences) {
    const definition = occurrence.definitionId
      ? definitions.get(occurrence.definitionId)
      : undefined;
    if (!definition) continue;
    const identity = {
      occurrenceId: occurrence.id,
      bodyId: occurrence.semanticEntityId,
      definitionId: definition.id,
    };
    meshes.push({
      definition,
      identity,
      transform: occurrence.transform,
      material: materialFromAppearance(appearance),
    });
    for (const edge of definition.edges ?? []) {
      const points = edge.points.flatMap((point) => Array.from(point));
      if (edge.closed && edge.points.length > 2) points.push(...edge.points[0]);
      lines.push({
        id: definition.id + ":" + edge.edgeId,
        points,
        transform: occurrence.transform,
        widthPixels: 1.2,
        color: [0.15, 0.18, 0.16, 1],
        identity: { ...identity, edgeId: edge.edgeId },
      });
    }
  }
  return { meshes, lines, fields: [] };
}
/** Three hierarchy is an input, never Telos scene storage or renderer state. */
export function fromThree(
  root: Object3D,
  identityFor: (object: Object3D) => TelosIdentity,
): TelosScene {
  root.updateMatrixWorld(true);
  const meshes: TelosMesh[] = [];
  root.traverse((object) => {
    if (!(object as Mesh).isMesh) return;
    const mesh = object as Mesh;
    if (Array.isArray(mesh.material))
      throw new Error("telos-three-multiple-materials-unqualified");
    const material = mesh.material as MeshStandardMaterial;
    const geometry = mesh.geometry;
    if (!geometry.getAttribute("normal")) geometry.computeVertexNormals();
    const positions = geometry.getAttribute("position").array;
    meshes.push({
      definition: {
        id: geometry.uuid,
        positions,
        normals: geometry.getAttribute("normal").array,
        indices:
          geometry.index?.array ??
          Uint32Array.from({ length: positions.length / 3 }, (_, i) => i),
      },
      transform: object.matrixWorld.toArray(),
      identity: identityFor(object),
      material: materialFromAppearance({
        baseColor: material.color?.toArray(),
        roughness: material.roughness,
        metallic: material.metalness,
        opacity: material.opacity,
      }),
    });
  });
  return { meshes, lines: [], fields: [] };
}
