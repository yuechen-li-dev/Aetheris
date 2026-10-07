import { test } from "node:test";
import assert from "node:assert/strict";
import { inspectSurfaces, surfaceInspectionFaces, TelosCamera, TelosPick, materialFromAppearance } from "../dist/index.js";
import type { TelosScene } from "../dist/index.js";

test("packed face inspection retains original triangle picking and occurrence identity", () => {
  const definition = { id: "body", positions: [-1,-1,0, 1,-1,0, -1,1,0, 1,1,0],
    normals: [0,0,1, 0,0,1, 0,0,1, 0,0,1], indices: [0,1,2, 2,1,3],
    ranges: [{ startTriangle: 0, triangleCount: 1, faceId: "first" }, { startTriangle: 1, triangleCount: 1, faceId: "second" }] };
  const scene: TelosScene = { meshes: ["a", "b"].map(occurrenceId => ({ definition,
    identity: { occurrenceId, definitionId: "body" }, material: materialFromAppearance() })), lines: [], fields: [] };
  const face = surfaceInspectionFaces(scene).find(face => face.occurrenceId === "a" && face.faceId === "second")!;
  const projected = inspectSurfaces(scene, "patches", face);
  assert.equal(projected.meshes.filter(mesh => mesh.visible).length, 1);
  assert.equal(projected.meshes[1].definition.positions, definition.positions);
  assert.equal(projected.meshes[1].definition.indices, inspectSurfaces(scene, "overlay", face).meshes[1].definition.indices);
  const camera = new TelosCamera(); camera.resize(800,600); camera.mode = "orthographic";
  camera.position.set(0,0,5); camera.target.set(0,0,0); camera.span = 4; camera.update();
  const pick = new TelosPick(camera); pick.setScene(projected);
  assert.equal(pick.proxies[0].geometry.getAttribute("position"), pick.proxies[1].geometry.getAttribute("position"));
  assert.equal(pick.proxies[0].geometry.index, pick.proxies[1].geometry.index);
  const hit = pick.pick([450,250]);
  assert.equal(hit?.faceId, "second"); assert.equal(hit?.triangleIndex, 1);
  assert.equal(hit?.definitionId, "body"); assert.equal(hit?.occurrenceId, "a");
  assert.equal(inspectSurfaces(scene,"normal"), scene);
  assert.equal(definition.indices.length, 6); pick.dispose();
});

test("whole field comparison draws the field; face inspection explicitly projects the retained proxy", () => {
  const material = materialFromAppearance();
  const proxy = { id: "proxy", positions: [0,0,0,1,0,0,0,1,0], normals: [0,0,1,0,0,1,0,0,1],
    indices: [0,1,2], ranges: [{ startTriangle:0, triangleCount:1, faceId:7 }] };
  const scene: TelosScene = { meshes:[], lines:[], fields:[{ identity:{ occurrenceId:"field", definitionId:"proxy" },
    material, proxy, bounds:{ minimum:[0,0,0], maximum:[1,1,1] },
    artifact:{ shaderId:"field", wgsl:"", vertexEntryPoint:"vs", fragmentEntryPoint:"fs", bindings:[], capabilities:[], sourceIdentity:"x" } }] };
  assert.equal(inspectSurfaces(scene,"surfaces").fields[0].visible,true);
  assert.equal(inspectSurfaces(scene,"surfaces").meshes.length,0);
  const isolated = inspectSurfaces(scene,"overlay",surfaceInspectionFaces(scene)[0]);
  assert.equal(isolated.fields[0].visible,false);
  assert.equal(isolated.meshes[0].identity.faceId,7);
  assert.equal(isolated.meshes[0].definition.positions,proxy.positions);
});
