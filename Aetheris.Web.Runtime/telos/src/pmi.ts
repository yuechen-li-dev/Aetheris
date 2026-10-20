import { BufferGeometry, Float32BufferAttribute, Group, LineBasicMaterial, LineSegments } from 'three';
import type { TelosLine } from './contracts.js';

/** Shared projection of compiler-supplied PMI. Neither adapter invents values or changes body meshes. */
export interface PmiCurves { readonly id: string; readonly lines: readonly (readonly (readonly number[])[])[] }
export function pmiLines(annotations: readonly PmiCurves[] = []): TelosLine[] {
  return annotations.flatMap(annotation => annotation.lines.map((points, index) => ({
    id: `${annotation.id}:${index}`, points: points.flatMap(p => [...p]), identity: { occurrenceId: annotation.id, overlayId: annotation.id },
    color: [.04, .12, .24, 1], widthPixels: 1.5, overlay: true, depthMode: 'always-on-top' as const, pickable: false, includeInFit: true,
  })));
}
export function pmiGroup(annotations: readonly PmiCurves[] = []): Group {
  const group = new Group(); group.name = 'PMI presentation';
  for (const annotation of annotations) {
    const positions = annotation.lines.flatMap(line => line.slice(1).flatMap((point, i) => [...line[i], ...point]));
    const geometry = new BufferGeometry().setAttribute('position', new Float32BufferAttribute(positions, 3));
    const object = new LineSegments(geometry, new LineBasicMaterial({ color: 0x153356, depthTest: false }));
    object.name = annotation.id; object.renderOrder = 10; object.raycast = () => undefined; group.add(object);
  }
  return group;
}
