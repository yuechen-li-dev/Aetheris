import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import {
  TelosCamera,
  TelosPick,
  materialFromAppearance,
} from "../dist/index.js";
import type { TelosMesh, TelosLine } from "../dist/index.js";
const camera = () => {
  const camera = new TelosCamera();
  camera.resize(800, 600);
  camera.mode = "orthographic";
  camera.position.set(0, 0, 5);
  camera.target.set(0, 0, 0);
  camera.span = 4;
  camera.update();
  return camera;
};
const definition = {
  id: "plane",
  positions: [-1, -1, 0, 1, -1, 0, -1, 1, 0, 1, 1, 0],
  normals: [0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1],
  indices: [0, 1, 2, 2, 1, 3],
};
const mesh: TelosMesh = {
  definition,
  identity: { occurrenceId: "part", faceId: "face(+Z)" },
  material: materialFromAppearance({ color: "#aaa" }),
};
test("normal Cadmata graphics imports are independent of the unsupported-browser renderer", () => {
  const directory = new URL(
    "../../../aetheris.client/src/viewer/",
    import.meta.url,
  );
  for (const name of readdirSync(directory).filter((name) =>
    /\.tsx?$/.test(name),
  )) {
    const source = readFileSync(new URL(name, directory), "utf8");
    if (name.startsWith("Legacy") || name === "ThemeBackground.tsx") continue;
    assert.doesNotMatch(
      source,
      /from ["']@react-three\/|WebGLRenderer|useThree\(|useFrame\(/,
      name,
    );
  }
  const wrapper = readFileSync(
    new URL("AetherisViewport.tsx", directory),
    "utf8",
  );
  assert.match(wrapper, /lazy\(/);
  assert.doesNotMatch(wrapper, /props\.cadmataArtifact\s*\?(?!\?)/);
});
test("overlay priority, visibility, and engineering geometry identity", () => {
  const pick = new TelosPick(camera());
  const leader: TelosLine = {
    id: "leader",
    points: [-1, 0, -1, 1, 0, -1],
    identity: { occurrenceId: "overlay", overlayId: "datum:A" },
    depthMode: "always-on-top",
  };
  pick.setScene({ meshes: [mesh], lines: [], fields: [] });
  assert.equal(pick.pick([400, 300])?.faceId, "face(+Z)");
  pick.dynamicLines = [leader];
  assert.equal(pick.pick([400, 300])?.overlayId, "datum:A");
  pick.dynamicLines = [{ ...leader, visible: false }];
  assert.equal(pick.pick([400, 300])?.faceId, "face(+Z)");
  pick.dynamicLines = [{ ...leader, depthMode: "depth-tested" }];
  assert.equal(pick.pick([400, 300])?.faceId, "face(+Z)");
  pick.dispose();
});
test("selection/filter updates retain CPU geometry; a rebuild replaces it and preserves stable face identity", () => {
  const pick = new TelosPick(camera());
  pick.setScene({ meshes: [mesh], lines: [], fields: [] });
  const proxy = pick.proxies[0];
  pick.setScene({
    meshes: [{ ...mesh, selected: true }],
    lines: [],
    fields: [],
  });
  assert.equal(pick.proxies[0], proxy);
  pick.setScene({
    meshes: [
      {
        ...mesh,
        definition: { ...definition, positions: [...definition.positions] },
      },
    ],
    lines: [],
    fields: [],
  });
  assert.notEqual(pick.proxies[0], proxy);
  assert.equal(pick.pick([400, 300])?.faceId, "face(+Z)");
  pick.dispose();
});
