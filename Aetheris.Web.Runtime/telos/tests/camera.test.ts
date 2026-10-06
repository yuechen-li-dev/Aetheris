import { test } from "node:test";
import assert from "node:assert/strict";
import { Vector3, Matrix4 } from "three";
import {
  TelosCamera,
  TelosPick,
  materialFromAppearance,
  fromDisplayMesh,
} from "../dist/index.js";
import type { TelosScene } from "../dist/index.js";

for (const mode of ["perspective", "orthographic"] as const)
  test(`${mode}: project, unproject, ray, aspect and scale`, () => {
    const camera = new TelosCamera();
    camera.mode = mode;
    camera.resize(800, 600);
    for (const scale of [1e-5, 1, 1e6]) {
      camera.fit([-scale, -scale, -scale], [scale, scale, scale]);
      const world = new Vector3(scale * 0.2, scale * 0.3, scale * 0.1),
        pixel = camera.project(world.toArray());
      const restored = camera.unproject([pixel[0], pixel[1]], pixel[2]);
      assert.ok(restored.distanceTo(world) < scale * 1e-6);
      const ray = camera.worldRay([pixel[0], pixel[1]]);
      assert.ok(ray.direction.length() > 0.999999);
      assert.ok(
        ray.direction
          .clone()
          .cross(world.clone().sub(ray.origin).normalize())
          .length() < 1e-8,
      );
      const before = camera.projection.elements[0];
      camera.resize(1600, 600);
      assert.ok(camera.projection.elements[0] < before);
      camera.resize(800, 600);
    }
    const adapter = camera.toThree();
    adapter.position.set(100, 200, 300);
    assert.notEqual(camera.position.x, 100);
  });

test("stable identity, occurrence transforms and nearest surface picking", () => {
  const camera = new TelosCamera();
  camera.position.set(0, 0, 5);
  camera.target.set(0, 0, 0);
  camera.resize(200, 200);
  camera.update();
  const definition = {
    id: "shared",
    positions: [-1, -1, 0, 1, -1, 0, 0, 1, 0],
    normals: [0, 0, 1, 0, 0, 1, 0, 0, 1],
    indices: [0, 1, 2],
    ranges: [
      { startTriangle: 0, triangleCount: 1, faceId: "construction-face" },
    ],
  };
  const material = materialFromAppearance();
  const scene: TelosScene = {
    meshes: [
      {
        definition,
        material,
        identity: { occurrenceId: "back", bodyId: "body" },
      },
      {
        definition,
        material,
        transform: new Matrix4().makeTranslation(0, 0, 1).elements,
        identity: { occurrenceId: "front", bodyId: "body" },
      },
    ],
    lines: [],
    fields: [],
  };
  const picker = new TelosPick(camera);
  picker.setScene(scene);
  const hit = picker.pick([100, 100]);
  assert.equal(hit?.occurrenceId, "front");
  assert.equal(hit?.faceId, "construction-face");
  assert.equal(hit?.triangleIndex, 0);
  assert.equal(hit?.worldPosition[2], 1);
  picker.dispose();
  assert.equal(picker.proxies.length, 0);
});

test("SDK adapter preserves shared geometry and kernel edge identity", () => {
  const definition = {
    id: "definition",
    positions: [0, 0, 0],
    normals: [0, 0, 1],
    indices: [0],
    edges: [
      {
        edgeId: "brep-edge",
        points: [
          [0, 0, 0],
          [1, 0, 0],
        ],
        closed: false,
      },
    ],
  };
  const scene = fromDisplayMesh({
    definitions: [definition],
    occurrences: ["left", "right"].map((id) => ({
      id,
      definitionId: definition.id,
      semanticEntityId: "body",
      transform: new Matrix4().elements,
    })),
  });
  assert.equal(scene.meshes[0].definition, scene.meshes[1].definition);
  assert.equal(scene.lines[1].identity.edgeId, "brep-edge");
  assert.equal(scene.lines[1].identity.occurrenceId, "right");
});
