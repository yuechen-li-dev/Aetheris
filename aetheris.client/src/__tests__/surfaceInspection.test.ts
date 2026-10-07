import { expect, it } from "vitest";
import type { TelosScene } from "@aetheris/three-telos";
import { inspectSurfaces } from "../viewer/surfaceInspection";

it("compares disposable surfaces and authoritative edges without changing buffers or occurrence identities", () => {
  const positions = new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]);
  const material = { baseColor: [.5, .5, .5], opacity: 1, roughness: .6, metallic: 0 };
  const scene: TelosScene = {
    meshes: ["first", "second"].map(occurrenceId => ({ identity: { occurrenceId, faceId: 7 }, material,
      definition: { id: occurrenceId, positions, normals: positions, indices: [0, 1, 2] } })),
    lines: ["first", "second"].map(occurrenceId => ({ id: occurrenceId, identity: { occurrenceId, edgeId: 11 }, points: positions })),
    fields: [],
  };
  expect(inspectSurfaces(scene, "normal")).toBe(scene);
  const wire = inspectSurfaces(scene, "wire");
  expect(wire.meshes.every(m => m.visible === false)).toBe(true);
  expect(wire.lines.every(l => l.visible && l.depthMode === "always-on-top")).toBe(true);
  const surface = inspectSurfaces(scene, "surfaces");
  expect(surface.meshes.every(m => m.visible)).toBe(true);
  expect(surface.lines.every(l => l.visible === false)).toBe(true);
  const overlay = inspectSurfaces(scene, "overlay", { key: "first", occurrenceId: "first", faceId: 7, edgeIds: [11] });
  expect(overlay.meshes[0].material.opacity).toBe(.2);
  expect(overlay.meshes[1].visible).toBe(false);
  expect(overlay.lines[0].visible).toBe(true);
  expect(overlay.lines[1].visible).toBe(false);
  expect(overlay.meshes[0].definition.positions).toBe(positions);
  expect(overlay.meshes[0].identity).toBe(scene.meshes[0].identity);
  expect(scene.meshes[0].visible).toBeUndefined();
  expect(scene.meshes[0].material.opacity).toBe(1);
});
