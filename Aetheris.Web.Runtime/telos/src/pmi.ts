import { BufferGeometry, DoubleSide, Float32BufferAttribute, Group, LineBasicMaterial, LineSegments, Mesh, MeshBasicMaterial } from 'three';
import type { TelosLine, TelosMaterial, TelosMesh } from './contracts.js';

/** Shared projection of compiler-supplied PMI. Neither adapter invents values or changes body meshes. */
type Polylines = readonly (readonly (readonly number[])[])[];
export interface PmiCurves {
  readonly id: string; readonly lines: Polylines;
  readonly target?: { readonly kind: string; readonly positions: readonly number[]; readonly normals: readonly number[];
    readonly indices: readonly number[]; readonly lines: Polylines } | null;
}
/** Inspection appearance only. Explicit authored appearances retain their opacity. */
export function pmiBodyMaterial<T extends Pick<TelosMaterial, 'opacity'>>(material: T, enabled: boolean, authored = false): T {
  return enabled && !authored ? { ...material, opacity: Math.min(material.opacity, .78) } : material;
}
export function pmiLines(annotations: readonly PmiCurves[] = []): TelosLine[] {
  return annotations.flatMap(annotation => {
    const identity = { occurrenceId: annotation.id, overlayId: annotation.id };
    const labels: TelosLine[] = annotation.lines.map((points, index) => ({
      id: `${annotation.id}:${index}`, points: points.flatMap(p => [...p]), identity,
      color: [.04, .12, .24, 1], widthPixels: 1.5, overlay: true, depthMode: 'always-on-top', pickable: false, includeInFit: true,
    }));
    const targets = (annotation.target?.lines ?? []).flatMap((points, index): TelosLine[] => {
      const line: TelosLine = { id: `${annotation.id}:target:${index}`, points: points.flatMap(p => [...p]), identity,
        color: [1, .68, .12, 1], widthPixels: 2, overlay: true, depthMode: 'depth-biased', pickable: false, includeInFit: true };
      return [{ ...line, id: line.id + ':halo', widthPixels: 7, color: [1, .62, .05, .12] }, line];
    });
    return [...labels, ...targets];
  });
}
export function pmiMeshes(annotations: readonly PmiCurves[] = []): TelosMesh[] {
  return annotations.flatMap(annotation => annotation.target ? [{
    definition: { id: annotation.id + ':target', positions: annotation.target.positions, normals: annotation.target.normals, indices: annotation.target.indices },
    identity: { occurrenceId: annotation.id, overlayId: annotation.id }, overlay: true, pickable: false, unlit: true, depthMode: 'depth-biased' as const,
    material: { baseColor: [1, .68, .12], metallic: 0, roughness: 1, opacity: .65 },
  }] : []);
}
export function pmiGroup(annotations: readonly PmiCurves[] = []): Group {
  const group = new Group(); group.name = 'PMI presentation';
  for (const line of pmiLines(annotations).filter(line => !line.id.endsWith(':halo'))) {
    const points = Array.from(line.points); const positions: number[] = [];
    for (let i = 3; i < points.length; i += 3) positions.push(...points.slice(i-3,i+3));
    const geometry = new BufferGeometry().setAttribute('position', new Float32BufferAttribute(positions, 3));
    const target = line.id.includes(':target:');
    const object = new LineSegments(geometry, new LineBasicMaterial({ color: target ? 0xffcb45 : 0x153356,
      depthTest: target, depthWrite: false, toneMapped: false }));
    object.name = line.id; object.renderOrder = 10; object.raycast = () => undefined; group.add(object);
  }
  for (const marker of pmiMeshes(annotations)) {
    const geometry = new BufferGeometry().setAttribute('position', new Float32BufferAttribute(Array.from(marker.definition.positions), 3));
    geometry.setIndex(Array.from(marker.definition.indices));
    const object = new Mesh(geometry, new MeshBasicMaterial({ color: 0xffcb45, transparent: true, opacity: .65, side: DoubleSide,
      depthTest: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1, toneMapped: false }));
    object.name = marker.definition.id; object.renderOrder = 9; object.raycast = () => undefined; group.add(object);
  }
  return group;
}
export function disposePmiGroup(group: Group): void {
  group.traverse(object => { if (object instanceof Mesh || object instanceof LineSegments) {
    object.geometry.dispose(); const materials = Array.isArray(object.material) ? object.material : [object.material];
    materials.forEach(material => material.dispose());
  } });
  group.removeFromParent(); group.clear();
}
