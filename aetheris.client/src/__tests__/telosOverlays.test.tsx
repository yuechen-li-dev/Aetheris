import { describe, expect, it } from "vitest";
import { cadmataOverlays } from "../viewer/cadmataOverlays";
import { DEFAULT_CADMATA_LAYERS } from "../viewer/cadmataLayers";
import {
  pmiPresentationSelection,
  semanticPmiItems,
  DEFAULT_PMI_VISIBILITY,
} from "../viewer/pmiPresentation";
import {
  resolveCadmataSelection,
  type CadmataVisualizationArtifact,
} from "../viewer/conceptVisualization";
const artifact: CadmataVisualizationArtifact = {
  schemaVersion: "cadmata-concept-viz-x1",
  fixtureId: "overlays",
  sourcePath: "authoritative.firmament",
  selections: [],
  entities: [
    {
      stableId: "plane",
      kind: "ConstructionPlane",
      label: "YZ",
      layer: "constructionPlanes",
      geometry: {
        type: "plane",
        origin: { x: 3, y: 0, z: 0 },
        u: { x: 0, y: 2, z: 0 },
        v: { x: 0, y: 0, z: 4 },
      },
    },
    {
      stableId: "point",
      kind: "Point",
      label: "Origin",
      layer: "conceptPoints",
      geometry: { type: "point", point: { x: 3, y: 0, z: 0 } },
    },
    {
      stableId: "face:+Z",
      kind: "BRepFace",
      label: "Top",
      layer: "material",
      parentIds: ["target"],
      topology: { faceIds: [17] },
    },
    {
      stableId: "target",
      kind: "EngineeringTarget",
      label: "Top",
      layer: "selections",
      topology: { faceIds: [17] },
    },
    {
      stableId: "datum:A",
      kind: "Datum",
      label: "Datum A",
      layer: "selections",
      childIds: ["target"],
      metadata: { targetSemanticId: "target" },
      geometry: { type: "point", point: { x: 0, y: 0, z: 2 } },
    },
  ],
};
describe("Telos authoring adapter and PMI correspondence", () => {
  it("uses published plane basis and explicit depth; reuses geometry across selection and filters", () => {
    const props = {
      cadmataArtifact: artifact,
      cadmataLayers: DEFAULT_CADMATA_LAYERS,
    };
    const first = cadmataOverlays(props),
      selected = cadmataOverlays({
        ...props,
        selectedCadmataIds: new Set(["plane"]),
      });
    expect(first.meshes[0].definition.positions).toEqual([
      3, -2, -4, 3, -2, 4, 3, 2, -4, 3, 2, 4,
    ]);
    expect(first.meshes[0].depthMode).toBe("depth-tested");
    expect(first.meshes[1].depthMode).toBe("always-on-top");
    expect(selected.meshes[0].definition).toBe(first.meshes[0].definition);
    expect(
      cadmataOverlays({
        ...props,
        cadmataLayers: { ...DEFAULT_CADMATA_LAYERS, constructionPlanes: false },
      }).meshes,
    ).toHaveLength(1);
    expect(
      first.meshes.some((mesh) => mesh.identity.overlayId === "datum:A"),
    ).toBe(false);
  });
  it("preserves semantic anchors through source rebuild and selection in both directions", () => {
    const selection = resolveCadmataSelection(artifact, "datum:A");
    expect(selection.faceIds.has(17)).toBe(true);
    expect(
      pmiPresentationSelection(artifact, new Set(["face:+Z"])).has("datum:A"),
    ).toBe(true);
    const item = semanticPmiItems(artifact, DEFAULT_PMI_VISIBILITY)[0];
    expect(item.anchor).toEqual([0, 0, 2]);
    const rebuilt = {
      ...artifact,
      entities: artifact.entities.map((entity) =>
        entity.stableId === "datum:A"
          ? {
              ...entity,
              geometry: { type: "point" as const, point: { x: 0, y: 0, z: 3 } },
            }
          : entity,
      ),
    };
    expect(semanticPmiItems(rebuilt, DEFAULT_PMI_VISIBILITY)[0]).toMatchObject({
      entity: { stableId: "datum:A" },
      anchor: [0, 0, 3],
    });
    expect(item.anchor).toEqual([0, 0, 2]);
  });
});
