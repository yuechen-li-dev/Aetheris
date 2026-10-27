import test from "node:test";
import assert from "node:assert/strict";
import { Matrix4, Vector3 } from "three";
import { conceptWireframe, conceptWireframeGroup, disposeConceptWireframe, operationPreviewWireframe, operationPreviewWireframeGroup } from "../dist/concepts.js";
import { TelosPick } from "../dist/pick.js";
import { TelosCamera } from "../dist/camera.js";
import type { DisplayPacket } from "../dist/contracts.js";

test("gold guides follow occurrences, remain separate from material and never steal a pick", () => {
  const wire = { id: "Design.Axis", owner: "Design", kind: "Axis", points: [-1, 0, 0, 1, 0, 0], provenance: "compiler" };
  const packet: DisplayPacket = {
    definitions: [{ id: "part", positions: [], normals: [], indices: [], concepts: [wire] }],
    occurrences: [{ id: "first", definitionId: "part", semanticEntityId: "part", transform: new Matrix4().elements },
      { id: "second", definitionId: "part", semanticEntityId: "part", transform: new Matrix4().makeTranslation(60, 0, 0).elements }],
  };
  const lines = conceptWireframe(packet);
  assert.equal(lines.length, 4);
  assert.ok(lines.every(l => l.overlay && l.pickable === false && l.depthMode === "always-on-top"));
  assert.equal(new Vector3(-1, 0, 0).applyMatrix4(new Matrix4().fromArray(lines[2].transform!)).x, 59);
  assert.equal(lines[0].points, lines[2].points);
  const camera = new TelosCamera();
  camera.resize(200, 200); camera.position.set(0, 0, 10); camera.target.set(0, 0, 0);
  const picker = new TelosPick(camera);
  picker.setScene({ meshes: [], fields: [], lines });
  assert.equal(picker.pick([100, 100], "edge"), null);
  const group = conceptWireframeGroup(packet);
  assert.equal(group.children.length, 2);
  assert.equal(group.children[1].matrix.elements[12], 60);
  disposeConceptWireframe(group);
  assert.equal(group.children.length, 0);
  assert.equal(packet.definitions[0].indices.length, 0);
});

test("operation preview uses retained edge polylines and does not replace material or concepts", () => {
  const packet: DisplayPacket = {
    definitions: [{ id: "stock", positions: [0,0,0, 1,0,0, 0,1,0], normals: [], indices: [0,1,2],
      edges: [{ edgeId: "rim", points: [[0,0,0], [1,0,0], [1,1,0]], closed: true }] }],
    occurrences: [{ id: "placed", definitionId: "stock", semanticEntityId: "stock", transform: new Matrix4().makeTranslation(10,0,0).elements }],
  };
  const before = JSON.stringify(packet);
  const wires = operationPreviewWireframe(packet);
  assert.equal(wires.length, 2);
  assert.deepEqual(wires[1].points, [0,0,0, 1,0,0, 1,1,0, 0,0,0]);
  assert.ok(wires.every(w => w.overlay && w.pickable === false && w.depthMode === "always-on-top"));
  assert.equal(wires[1].transform![12], 10);
  assert.equal(conceptWireframe(packet).length, 0);
  const group = operationPreviewWireframeGroup(packet);
  assert.equal(group.children.length, 1);
  assert.equal(group.children[0].matrix.elements[12], 10);
  disposeConceptWireframe(group);
  assert.equal(group.children.length, 0);
  assert.equal(JSON.stringify(packet), before);
});
