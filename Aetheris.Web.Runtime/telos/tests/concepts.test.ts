import test from "node:test";
import assert from "node:assert/strict";
import { Matrix4, Vector3 } from "three";
import { conceptWireframe, conceptWireframeGroup, disposeConceptWireframe } from "../dist/concepts.js";
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
