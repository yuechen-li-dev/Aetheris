import { test } from "node:test";
import assert from "node:assert/strict";
import { Vector3 } from "three";
import {
  TelosCamera,
  temporalProjection,
  temporalJitter,
} from "../dist/index.js";
for (const mode of ["perspective", "orthographic"] as const) {
  test(`${mode}: render jitter is bounded and logical picking is invariant`, () => {
    const camera = new TelosCamera();
    camera.mode = mode;
    camera.resize(800, 600);
    camera.update();
    const original = camera.viewProjection.clone();
    const ray = camera.worldRay([400, 300]);
    const world = new Vector3(0.2, 0.1, 0);
    const logical = world.clone().applyMatrix4(original);
    for (let frame = 0; frame < 24; frame++) {
      const jitter = temporalJitter(frame);
      const render = world
        .clone()
        .applyMatrix4(temporalProjection(camera, 1600, 1200, frame));
      assert.ok(Math.abs((render.x - logical.x) * 800 - jitter[0]) < 1e-9);
      assert.ok(Math.abs((render.y - logical.y) * -600 - jitter[1]) < 1e-9);
      assert.ok(jitter.every((value) => Math.abs(value) <= 0.5));
      assert.deepEqual(camera.viewProjection.elements, original.elements);
      assert.deepEqual(camera.worldRay([400, 300]), ray);
      assert.deepEqual(jitter, temporalJitter(frame + 8));
    }
  });
}
