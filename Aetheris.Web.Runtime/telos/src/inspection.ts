import type { TelosScene, TelosMesh } from "./contracts.js";

export type SurfaceInspectionMode = "normal" | "surfaces" | "wire" | "overlay" | "patches";
export interface SurfaceInspectionFace {
  key: string;
  occurrenceId: string;
  faceId: string | number;
  sourceStepEntityId?: number | null;
  surfaceKind?: string | null;
  /** Omit when the source packet does not transport face-edge adjacency. */
  edgeIds?: readonly (number | string)[];
}
export const inspectionFaceKey = (occurrence: string, face: string | number) => JSON.stringify([occurrence, face]);

/** Enumerate source faces, including packed SDK ranges and retained field proxies. */
export function surfaceInspectionFaces(scene: TelosScene): SurfaceInspectionFace[] {
  const faces = new Map<string, SurfaceInspectionFace>();
  for (const item of [...scene.meshes, ...scene.fields]) {
    if ("overlay" in item && item.overlay) continue;
    const geometry = "definition" in item ? item.definition : item.proxy;
    const ids = item.identity.faceId !== undefined ? [item.identity.faceId] : geometry?.ranges?.map(r => r.faceId) ?? [];
    for (const faceId of ids) {
      const occurrenceId = item.identity.occurrenceId;
      const key = inspectionFaceKey(occurrenceId, faceId);
      faces.set(key, { key, occurrenceId, faceId });
    }
  }
  return [...faces.values()];
}

function faceMeshes(mesh: TelosMesh): TelosMesh[] {
  if (mesh.overlay || mesh.identity.faceId !== undefined || !mesh.definition.ranges?.length) return [mesh];
  return mesh.definition.ranges.map(range => ({ ...mesh, triangleRange: range,
    identity: { ...mesh.identity, faceId: range.faceId } }));
}

/** Diagnostic projection only: immutable buffers, engineering identities and camera stay unchanged. */
export function inspectSurfaces(scene: TelosScene, mode: SurfaceInspectionMode,
  isolated?: SurfaceInspectionFace): TelosScene {
  if (mode === "normal" && !isolated) return scene;
  const included = (identity: { occurrenceId: string; faceId?: string | number }) => !isolated ||
    identity.occurrenceId === isolated.occurrenceId && identity.faceId === isolated.faceId;
  // A field has no per-face shader mask. Face inspection explicitly uses its retained
  // BRep proxy; whole-model surface/overlay views continue to draw the actual field.
  const proxyFields = scene.fields.filter(field => field.proxy && (isolated || mode === "patches"));
  const meshes: TelosMesh[] = [...scene.meshes, ...proxyFields.map(field => ({ definition: field.proxy!,
    identity: field.identity, transform: field.transform, material: field.material, visible: field.visible }))];
  return {
    ...scene,
    meshes: meshes.flatMap(mesh => isolated || mode === "patches" ? faceMeshes(mesh) : [mesh]).map(mesh => {
      if (mesh.overlay) return mesh;
      const key = inspectionFaceKey(mesh.identity.occurrenceId, mesh.identity.faceId ?? mesh.definition.id);
      let hash = 0; for (const c of key) hash = (Math.imul(hash, 31) + c.charCodeAt(0)) | 0;
      const color = [0, 8, 16].map(shift => .25 + ((hash >>> shift) & 255) / 255 * .65);
      return { ...mesh, visible: mesh.visible !== false && included(mesh.identity) && mode !== "wire",
        material: { ...mesh.material, baseColor: mode === "normal" ? mesh.material.baseColor : mode === "patches" ? color : [0.25, 0.55, 0.85],
          opacity: mode === "overlay" ? .2 : mesh.material.opacity }, selected: false, hovered: false };
    }),
    fields: scene.fields.map(field => ({ ...field, visible: !proxyFields.includes(field) && field.visible !== false && included(field.identity) && mode !== "wire",
      material: { ...field.material, opacity: mode === "overlay" ? .2 : field.material.opacity } })),
    lines: scene.lines.map(line => {
      if (line.identity.edgeId === undefined) return line;
      const boundary = !isolated || line.identity.occurrenceId === isolated.occurrenceId &&
        (isolated.edgeIds?.some(id => String(id) === String(line.identity.edgeId)) ??
          (line.identity.faceId === undefined || line.identity.faceId === isolated.faceId));
      return { ...line, visible: line.visible !== false && boundary && mode !== "surfaces",
        color: [1, .18, .08, 1], widthPixels: 1.5, selected: false,
        depthMode: mode === "overlay" || mode === "wire" ? "always-on-top" as const : line.depthMode };
    }),
  };
}
