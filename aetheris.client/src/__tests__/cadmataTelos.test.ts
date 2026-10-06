import { describe, it, expect } from "vitest";
import { cadmataTelosScene } from "../viewer/cadmataTelos";
import type { DisplayScene } from "../viewer/displayRenderables";

describe("Cadmata Telos packet adapter", () => {
  it("preserves mesh face and kernel edge identity with highlight state", () => {
    const scene: DisplayScene = {
      status: "Complete",
      sourceAuthority: "BRep",
      displayAuthority: "DisplayIR",
      lanes: [],
      diagnostics: [],
      renderables: [
        {
          kind: "MeshPatch",
          faceId: 17,
          surfaceKind: "Plane",
          status: "Mesh",
          patchKind: "MeshPatch",
          materializationLane: "BoundedMesh",
          diagnostics: [],
          mesh: {
            faceId: 17,
            positions: new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]),
            normals: new Float32Array([0, 0, 1, 0, 0, 1, 0, 0, 1]),
            indices: new Uint32Array([0, 1, 2]),
          },
        },
        {
          kind: "WirePatch",
          faceId: 17,
          surfaceKind: "Plane",
          status: "Wire",
          patchKind: "WirePatch",
          materializationLane: null,
          diagnostics: [],
          wires: [{ edgeId: 31, points: new Float32Array([0, 0, 0, 1, 0, 0]) }],
        },
      ],
    };
    const result = cadmataTelosScene({
      displayScene: scene,
      highlightedFaceId: 17,
      showAxisGuide: false,
    });
    expect(result.meshes[0].identity.faceId).toBe(17);
    expect(result.meshes[0].selected).toBe(true);
    expect(result.lines[0].identity.edgeId).toBe(31);
    expect(result.lines[0].selected).toBe(true);
  });
});
