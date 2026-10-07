import { describe, it, expect } from "vitest";
import { cadmataTelosScene } from "../viewer/cadmataTelos";
import type { DisplayScene } from "../viewer/displayRenderables";
import type { AssemblyDisplayPacketDto } from "../api/aetherisApi";

describe("Cadmata Telos packet adapter", () => {
  it("shares kernel edge buffers across occurrence transforms and selection changes", () => {
    const first = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 5, 0, 0, 1];
    const second = [...first]; second[12] = 9;
    const packet: AssemblyDisplayPacketDto = {
      schema: "test", name: "fixture", rootOccurrenceStableId: "first",
      definitions: [{ stableId: "part", definitionIdentity: "part", facePatches: [],
        edgePolylines: [{ edgeId: 31, points: [{ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }], isClosed: false }] }],
      occurrences: [first, second].map((worldTransform, i) => ({ stableId: i ? "second" : "first", name: "part",
        instancePath: String(i), parentStableId: null, definitionStableId: "part", kind: "Part", worldTransform,
        placementAuthority: "ImportedOccurrence" })),
      mates: [], toleranceStackups: [], bounds: { minimum: [0, 0, 0], maximum: [10, 1, 1] }, diagnostics: [], performance: {},
    };
    const before = cadmataTelosScene({ assemblyPacket: packet, showAxisGuide: false });
    const after = cadmataTelosScene({ assemblyPacket: packet, showAxisGuide: false, selectedAssemblyOccurrenceId: "first" });
    expect(before.lines).toHaveLength(2);
    expect(before.lines[0].identity).toEqual({ occurrenceId: "first", edgeId: 31 });
    expect(before.lines[0].transform).toBe(first);
    expect(before.lines[1].transform).toBe(second);
    expect(before.lines[0].points).toBe(before.lines[1].points);
    expect(after.lines[0].points).toBe(before.lines[0].points);
    expect(after.lines[0].selected).toBe(true);
    expect(after.lines[1].selected).toBe(false);
  });
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
