import { BufferGeometry, Float32BufferAttribute, Group, Line, LineBasicMaterial, Matrix4 } from "three";
import type { DisplayPacket, TelosLine } from "./contracts.js";
import { fromDisplayMesh } from "./adapters.js";

function gold(line: TelosLine): TelosLine[] {
  const guide = { ...line, overlay: true, pickable: false, depthMode: "always-on-top" as const,
    widthPixels: 1.6, color: [1, .68, .12, 1] };
  return [{ ...guide, id: line.id + ":halo", widthPixels: 7, color: [1, .62, .05, .12] }, guide];
}

/** Gold construction guides share the material occurrence's local-to-world transform.
 * A low-opacity wide stroke is a static halo, not an extra bloom/render pipeline. */
export function conceptWireframe(packet: DisplayPacket): TelosLine[] {
  const definitions = new Map(packet.definitions.map(d => [d.id, d]));
  return packet.occurrences.flatMap(occurrence =>
    [...(definitions.get(occurrence.definitionId ?? "")?.concepts ?? []), ...(occurrence.concepts ?? [])]
      .flatMap((wire, index): TelosLine[] => {
        if (wire.points.length < 6 || wire.points.length % 3 || !wire.points.every(Number.isFinite)) return [];
        const line: TelosLine = {
          id: `concept:${occurrence.id}:${index}:${wire.id}`, points: wire.points,
          transform: occurrence.transform, identity: { occurrenceId: occurrence.id, overlayId: `concept:${wire.owner}` },
          overlay: true, pickable: false, depthMode: "always-on-top", widthPixels: 1.6, color: [1, .68, .12, 1],
        };
        return gold(line);
      }));
}

/** Compiler-projected BRep boundaries of an operation candidate. These are
 * presentation guides, not authored Concept declarations or selectable edges. */
export function operationPreviewWireframe(packet: DisplayPacket): TelosLine[] {
  return fromDisplayMesh(packet).lines.flatMap(line => gold({ ...line, id: `preview:${line.id}`,
    identity: { ...line.identity, overlayId: "operation-preview" } }));
}

export function operationPreviewWireframeGroup(packet: DisplayPacket): Group {
  return wireframeGroup(operationPreviewWireframe(packet), "Operation preview");
}

/** Transitional WebGL consumes the same projected wires; no independent parsing. */
export function conceptWireframeGroup(packet: DisplayPacket): Group {
  return wireframeGroup(conceptWireframe(packet), "Concept Structs");
}

function wireframeGroup(wires: TelosLine[], name: string): Group {
  const group = new Group();
  group.name = name;
  for (const wire of wires.filter(w => !w.id.endsWith(":halo"))) {
    const geometry = new BufferGeometry().setAttribute("position", new Float32BufferAttribute(Array.from(wire.points), 3));
    const material = new LineBasicMaterial({ color: "#ffcb45", depthTest: false, depthWrite: false, toneMapped: false });
    const line = new Line(geometry, material);
    line.name = wire.id;
    line.matrix.copy(new Matrix4().fromArray(wire.transform!));
    line.matrixAutoUpdate = false;
    line.renderOrder = 20;
    line.raycast = () => undefined;
    group.add(line);
  }
  return group;
}

export function disposeConceptWireframe(group: Group): void {
  group.traverse(object => { if (object instanceof Line) { object.geometry.dispose(); (object.material as LineBasicMaterial).dispose(); } });
  group.removeFromParent();
  group.clear();
}
