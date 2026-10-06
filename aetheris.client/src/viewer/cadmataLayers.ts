import type { CadmataLayer } from "./conceptVisualization";
export type CadmataLayerVisibility = Record<CadmataLayer, boolean>;
export const DEFAULT_CADMATA_LAYERS: CadmataLayerVisibility = {
  material: true,
  brepEdges: true,
  conceptPoints: true,
  conceptAxes: true,
  conceptRegions: true,
  conceptPlanes: true,
  constructionPlanes: true,
  profileGuides: true,
  profileLoops: true,
  composeRegions: true,
  selections: true,
  diagnostics: true,
};
