/* eslint-disable react-refresh/only-export-components -- real API/wrapper browser qualification entry. */
import { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import type { TelosHost } from "@aetheris/three-telos";
import {
  createDocument,
  importStep,
  prepareBodyDisplay,
  loadCadmataFixture,
  pickBody,
} from "../api/aetherisApi";
import { AetherisViewport } from "../viewer/AetherisViewport";
import { DEFAULT_CADMATA_LAYERS } from "../viewer/cadmataLayers";
import { DEFAULT_PMI_VISIBILITY } from "../viewer/pmiPresentation";
import {
  resolveCadmataSelection,
  parseCadmataVisualizationArtifact,
  type CadmataVisualizationArtifact,
} from "../viewer/conceptVisualization";
import { resolvePublishedBrepEntity } from "../viewer/semanticInspection";
import { buildDisplaySceneData } from "../viewer/displaySceneBuilder";
import type { DisplayScene } from "../viewer/displayRenderables";
import "../App.css";
import ctc03Source from "../../../docs/development/milestones/modules/sheetmetal/artifacts/ctc03-manufacturing-release/ctc03-manufacturing-ap242.step?raw";
declare global {
  interface Window {
    cadmataX1?: {
      host?: TelosHost;
      artifact?: CadmataVisualizationArtifact;
      selected?: string | null;
      revision: number;
    };
  }
}
function Witness() {
  const [model, setModel] = useState<{
    scene: DisplayScene | null;
    artifact: CadmataVisualizationArtifact;
    documentId: string;
    bodyId: string;
  } | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const [mode, setMode] = useState("ctc03");
  const [revision, setRevision] = useState(0);
  const [visibility, setVisibility] = useState(DEFAULT_PMI_VISIBILITY);
  const [error, setError] = useState("");
  const load = async (kind: string) => {
    const document = await createDocument("Telos X1 qualification");
    let artifact: CadmataVisualizationArtifact, bodyId: string;
    if (kind === "ctc03") {
      const source = ctc03Source;
      const imported = await importStep(
        document.documentId,
        source,
        "ctc03-manufacturing-ap242.step",
      );
      artifact = parseCadmataVisualizationArtifact(
        imported.semanticPresentation,
      );
      bodyId = imported.occurrenceId;
    } else {
      const fixture = await loadCadmataFixture(
        document.documentId,
        kind === "authored-pmi"
          ? "pmi-projected-hole-diameter"
          : "construction-plane-positive-x",
      );
      artifact = parseCadmataVisualizationArtifact(fixture.visualization);
      bodyId = fixture.bodyId;
    }
    const scene = buildDisplaySceneData(
      await prepareBodyDisplay(document.documentId, bodyId),
    ).displayScene;
    setModel({ scene, artifact, documentId: document.documentId, bodyId });
    setRevision((value) => value + 1);
  };
  useEffect(() => {
    void Promise.resolve()
      .then(() => load(mode))
      .catch((reason) => setError(String(reason)));
  }, [mode]);
  useEffect(() => {
    window.cadmataX1 = {
      ...window.cadmataX1,
      artifact: model?.artifact,
      selected,
      revision,
    };
  }, [model, selected, revision]);
  const selection = model
    ? resolveCadmataSelection(model.artifact, selected)
    : null;
  return (
    <>
      <button
        onClick={() => setMode(mode === "ctc03" ? "construction" : "ctc03")}
      >
        Switch model
      </button>
      <button onClick={() => setMode("authored-pmi")}>Authored PMI</button>
      <button
        onClick={() =>
          void load(mode).catch((reason) => setError(String(reason)))
        }
      >
        Rebuild
      </button>
      <button
        onClick={() =>
          setVisibility({
            datums: true,
            dimensions: true,
            geometricTolerances: true,
            engineeringAnnotations: true,
          })
        }
      >
        All PMI
      </button>
      <button onClick={() => setVisibility(DEFAULT_PMI_VISIBILITY)}>
        Default PMI
      </button>
      <div style={{ width: 1100, height: 800 }}>
        <AetherisViewport
          displayScene={model?.scene}
          cadmataArtifact={model?.artifact}
          cadmataLayers={DEFAULT_CADMATA_LAYERS}
          selectedCadmataIds={selection?.entityIds}
          highlightedFaceIds={selection?.faceIds}
          highlightedEdgeIds={selection?.edgeIds}
          pmiVisibility={visibility}
          onCadmataSelect={setSelected}
          onHostReady={(host) => {
            window.cadmataX1 = { ...window.cadmataX1, revision, host };
          }}
          onPickRay={(origin, direction) => {
            if (model)
              void pickBody(model.documentId, model.bodyId, {
                origin,
                direction,
                tessellationOptions: null,
                pickOptions: { nearestOnly: true },
              }).then((result) => {
                const hit = result.hits[0];
                if (hit)
                  setSelected(
                    resolvePublishedBrepEntity(
                      model.artifact,
                      hit.entityKind,
                      hit.faceId ?? hit.edgeId ?? -1,
                    )?.stableId ?? null,
                  );
              });
          }}
        />
      </div>
      <output>{selected ?? "No selection"}</output>
      {error && <div role="alert">{error}</div>}
    </>
  );
}
createRoot(document.querySelector("#root")!).render(<Witness />);
