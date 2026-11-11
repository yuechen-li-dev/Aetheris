import { BufferGeometry, Color, DoubleSide, Float32BufferAttribute, Group, LineBasicMaterial, LineSegments, Mesh, MeshBasicMaterial } from 'three';
import type { TelosLine, TelosMaterial, TelosMesh } from './contracts.js';

/** Shared projection of compiler-supplied PMI. Neither adapter invents values or changes body meshes. */
type Polylines = readonly (readonly (readonly number[])[])[];
type Triangles = { readonly positions: readonly number[]; readonly indices: readonly number[] };
export interface PmiCurves {
  readonly id: string; readonly lines: Polylines;
  readonly target?: { readonly kind: string; readonly positions: readonly number[]; readonly normals: readonly number[];
    readonly indices: readonly number[]; readonly lines: Polylines } | null;
  readonly drawing?: { readonly lines: Polylines; readonly fill: Triangles | null; readonly panel: Triangles | null } | null;
}
/**
 * Two presentations of the same records. `markers` tints each record's target in its own colour and leaves labels to
 * the host (numbered tags laid out in screen space, which stay legible in screenshots); `drawing` is the ASME
 * presentation exported to annotated GLB: feature control frames, datum symbols and dimensions with filled text.
 */
export type PmiStyle = 'markers' | 'drawing';
/** Okabe–Ito: distinguishable with the common colour-vision deficiencies. Linear-ish sRGB triples. */
export const PMI_MARKER_COLORS: readonly (readonly [number, number, number])[] = [
  [.90, .62, 0], [.34, .71, .91], [0, .62, .45], [.80, .47, .65], [0, .45, .70], [.84, .37, 0], [.94, .89, .26],
];
export const pmiMarkerColor = (index: number) => PMI_MARKER_COLORS[index % PMI_MARKER_COLORS.length]!;
/** Inspection appearance only. Explicit authored appearances retain their opacity. */
export function pmiBodyMaterial<T extends Pick<TelosMaterial, 'opacity'>>(material: T, enabled: boolean, authored = false): T {
  return enabled && !authored ? { ...material, opacity: Math.min(material.opacity, .78) } : material;
}
export function pmiLines(annotations: readonly PmiCurves[] = [], style: PmiStyle = 'markers'): TelosLine[] {
  if (style === 'drawing') return annotations.flatMap(annotation => (annotation.drawing?.lines ?? annotation.lines).map((points, index): TelosLine => ({
    id: `${annotation.id}:drawing:${index}`, points: points.flatMap(p => [...p]), identity: { occurrenceId: annotation.id, overlayId: annotation.id },
    color: [.04, .07, .13, 1], widthPixels: 1.25, overlay: true, depthMode: 'depth-tested', pickable: false, includeInFit: true })));
  return annotations.flatMap((annotation, n) => {
    const identity = { occurrenceId: annotation.id, overlayId: annotation.id };
    const [r, g, b] = pmiMarkerColor(n);
    const targets = (annotation.target?.lines ?? []).flatMap((points, index): TelosLine[] => {
      const line: TelosLine = { id: `${annotation.id}:target:${index}`, points: points.flatMap(p => [...p]), identity,
        color: [r, g, b, 1], widthPixels: 2.5, overlay: true, depthMode: 'depth-biased', pickable: false, includeInFit: true };
      return [{ ...line, id: line.id + ':halo', widthPixels: 7, color: [r, g, b, .16] }, line];
    });
    return targets;
  });
}

/** The label polylines of the projection, for line-only consumers. */
export function pmiLabelLines(annotations: readonly PmiCurves[] = []): TelosLine[] {
  return annotations.flatMap(annotation => {
    const identity = { occurrenceId: annotation.id, overlayId: annotation.id };
    const labels: TelosLine[] = annotation.lines.map((points, index) => ({
      id: `${annotation.id}:${index}`, points: points.flatMap(p => [...p]), identity,
      color: [.04, .12, .24, 1], widthPixels: 1.5, overlay: true, depthMode: 'always-on-top', pickable: false, includeInFit: true,
    }));
    return labels;
  });
}
export function pmiMeshes(annotations: readonly PmiCurves[] = [], style: PmiStyle = 'markers'): TelosMesh[] {
  if (style === 'drawing') return annotations.flatMap(annotation => {
    const flat = (id: string, t: Triangles | null | undefined, color: readonly number[], opacity: number): TelosMesh[] => t && t.indices.length ? [{
      definition: { id: `${annotation.id}:${id}`, positions: t.positions, normals: t.positions.map((_, i) => i % 3 === 2 ? 1 : 0), indices: t.indices },
      identity: { occurrenceId: annotation.id, overlayId: annotation.id }, overlay: true, pickable: false, unlit: true, depthMode: 'depth-tested' as const,
      material: { baseColor: color, metallic: 0, roughness: 1, opacity } }] : [];
    return [...flat('panel', annotation.drawing?.panel, [.97, .97, .95], 1), ...flat('fill', annotation.drawing?.fill, [.04, .07, .13], 1)];
  });
  return annotations.flatMap((annotation, n) => annotation.target ? [{
    definition: { id: annotation.id + ':target', positions: annotation.target.positions, normals: annotation.target.normals, indices: annotation.target.indices },
    identity: { occurrenceId: annotation.id, overlayId: annotation.id }, overlay: true, pickable: false, unlit: true, depthMode: 'depth-biased' as const,
    material: { baseColor: [...pmiMarkerColor(n)], metallic: 0, roughness: 1, opacity: .7 },
  }] : []);
}
export function pmiGroup(annotations: readonly PmiCurves[] = [], style: PmiStyle = 'markers'): Group {
  const group = new Group(); group.name = 'PMI presentation';
  for (const line of pmiLines(annotations, style).filter(line => !line.id.endsWith(':halo'))) {
    const points = Array.from(line.points); const positions: number[] = [];
    for (let i = 3; i < points.length; i += 3) positions.push(...points.slice(i-3,i+3));
    const geometry = new BufferGeometry().setAttribute('position', new Float32BufferAttribute(positions, 3));
    const object = new LineSegments(geometry, new LineBasicMaterial({ color: new Color(line.color?.[0] ?? 0, line.color?.[1] ?? 0, line.color?.[2] ?? 0),
      depthTest: true, depthWrite: false, toneMapped: false }));
    object.name = line.id; object.renderOrder = 10; object.raycast = () => undefined; group.add(object);
  }
  for (const marker of pmiMeshes(annotations, style)) {
    const geometry = new BufferGeometry().setAttribute('position', new Float32BufferAttribute(Array.from(marker.definition.positions), 3));
    geometry.setIndex(Array.from(marker.definition.indices));
    const [r, g, b] = marker.material.baseColor;
    const fill = marker.definition.id.endsWith(':fill');
    const object = new Mesh(geometry, new MeshBasicMaterial({ color: new Color(r, g, b), transparent: marker.material.opacity < 1, opacity: marker.material.opacity, side: DoubleSide,
      depthTest: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1, toneMapped: false }));
    object.name = marker.definition.id; object.renderOrder = fill ? 11 : 9; object.raycast = () => undefined; group.add(object);
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
