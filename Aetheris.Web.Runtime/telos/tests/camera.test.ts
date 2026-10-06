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

test("resolved occurrence materials retain shared geometry and camera metadata uses Telos authority", () => {
  const definition = {
    id: "shared",
    positions: [0, 0, 0],
    normals: [0, 0, 1],
    indices: [],
  };
  const material = {
    baseColor: [0.7, 0.2, 0.1],
    roughness: 0.3,
    metallic: 0.1,
    opacity: 0.18,
  };
  const scene = fromDisplayMesh({
    definitions: [definition],
    occurrences: [
      {
        id: "glass",
        definitionId: "shared",
        semanticEntityId: "pane",
        transform: new Matrix4().elements,
        material,
      },
      {
        id: "default",
        definitionId: "shared",
        semanticEntityId: "body",
        transform: new Matrix4().elements,
      },
    ],
  });
  assert.equal(scene.meshes[0].material, material);
  assert.equal(scene.meshes[0].definition, scene.meshes[1].definition);
  assert.equal(scene.meshes[1].material.opacity, 1);
  const camera = new TelosCamera();
  camera.applyDisplayCamera({
    transform: new Matrix4().makeTranslation(100, 200, 300).elements,
    lookAtMm: [0, 0, 0],
    fovDegrees: 67,
  });
  assert.deepEqual(camera.position.toArray(), [100, 200, 300]);
  assert.deepEqual(camera.target.toArray(), [0, 0, 0]);
  assert.equal(camera.fov, 67);
});

test("fit contains every bounds corner in narrow and wide product panes", () => {
  for (const mode of ["perspective", "orthographic"] as const)
    for (const [width, height] of [
      [320, 900],
      [1600, 500],
    ])
      for (const scale of [1e-5, 1, 1e6]) {
        const camera = new TelosCamera();
        camera.mode = mode;
        camera.up.set(0, 0, 1);
        camera.resize(width, height);
        camera.fit([-scale, -scale, -scale], [scale, scale, scale]);
        assert.ok(camera.position.z > camera.target.z);
        for (const x of [-scale, scale])
          for (const y of [-scale, scale])
            for (const z of [-scale, scale]) {
              const [px, py, depth] = camera.project([x, y, z]);
              assert.ok(px > 0 && px < width && py > 0 && py < height);
              assert.ok(depth > 0 && depth < 1);
            }
      }
});

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
  assert.notEqual(scene.lines[0].id, scene.lines[1].id);
});

test("Auto field projection retains picking/fallback geometry and diagnoses missing transport", () => {
  const definition = {
    id: "qualified",
    positions: [0, 0, 0],
    normals: [0, 0, 1],
    indices: [0],
    cir: {
      qualification: "cir-qualified",
      structuralIdentity: "semantic",
      minimumMm: [0, 0, 0],
      maximumMm: [1, 1, 1],
    },
  };
  const material = {
    baseColor: [0.1, 0.2, 0.3],
    roughness: 0.4,
    metallic: 0.7,
    opacity: 0.8,
  };
  const packet = {
    definitions: [definition],
    occurrences: [
      {
        id: "instance",
        definitionId: "qualified",
        semanticEntityId: "body",
        transform: new Matrix4().elements,
        material,
      },
    ],
  };
  const missing = fromDisplayMesh(packet);
  assert.equal(missing.meshes.length, 1);
  assert.equal(
    missing.projectionDiagnostics?.[0].status,
    "shader-artifact-transport-missing",
  );
  const artifact = {
    shaderId: "compiled",
    sourceIdentity: "semantic",
    wgsl: "compiler-test-output",
    vertexEntryPoint: "vertex",
    fragmentEntryPoint: "fragment",
    bindings: [],
    capabilities: [],
  };
  const bound = fromDisplayMesh({
    ...packet,
    definitions: [
      { ...definition, shader: { status: "shader-artifact-bound", artifact } },
    ],
  });
  assert.equal(bound.meshes.length, 0);
  assert.equal(bound.fields.length, 1);
  assert.equal(bound.fields[0].proxy, bound.fields[0].fallback?.definition);
  assert.equal(bound.fields[0].material, material);
  assert.equal(bound.fields[0].identity.definitionId, "qualified");
  const stale = fromDisplayMesh({
    ...packet,
    definitions: [
      {
        ...definition,
        shader: {
          status: "shader-artifact-bound",
          artifact: { ...artifact, sourceIdentity: "stale" },
        },
      },
    ],
  });
  assert.equal(stale.fields.length, 0);
  assert.equal(stale.meshes.length, 1);
  assert.equal(
    stale.projectionDiagnostics?.[0].status,
    "shader-artifact-transport-missing",
  );
});
