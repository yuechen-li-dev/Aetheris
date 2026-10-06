/* eslint-disable react-refresh/only-export-components -- standalone browser qualification entry point. */
import { useState } from "react";
import { createRoot } from "react-dom/client";
import { AetherisViewport } from "../viewer/AetherisViewport";
import type { DisplayScene } from "../viewer/displayRenderables";
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
        positions: new Float32Array([-1, -1, 0, 1, -1, 0, -1, 1, 0, 1, 1, 0]),
        normals: new Float32Array([0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1]),
        indices: new Uint32Array([0, 1, 2, 2, 1, 3]),
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
      wires: [
        {
          edgeId: 31,
          points: new Float32Array([
            -1, -1, 0, 1, -1, 0, 1, 1, 0, -1, 1, 0, -1, -1, 0,
          ]),
        },
      ],
    },
  ],
};
declare global {
  interface Window {
    cadmataTelosRay?: {
      origin: { x: number; y: number; z: number };
      direction: { x: number; y: number; z: number };
    };
  }
}
function Witness() {
  const [selected, setSelected] = useState(false);
  return (
    <>
      <h1>Real Cadmata wrapper + DisplayScene fixture</h1>
      <button onClick={() => setSelected(!selected)}>Select</button>
      <div style={{ width: 800, height: 600 }}>
        <AetherisViewport
          displayScene={scene}
          highlightedFaceId={selected ? 17 : null}
          highlightedEdgeId={selected ? 31 : null}
          onPickRay={(origin, direction) => {
            window.cadmataTelosRay = { origin, direction };
          }}
        />
      </div>
    </>
  );
}
createRoot(document.querySelector("#root")!).render(<Witness />);
