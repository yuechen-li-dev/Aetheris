import type { DisplayScene } from "./displayRenderables";
import type { AssemblyDisplayPacketDto } from "../api/aetherisApi";
import type { CadmataVisualizationArtifact } from "./conceptVisualization";
import type { CadmataLayerVisibility } from "./cadmataLayers";
import type { PmiVisibility } from "./pmiPresentation";
import type { ViewportTheme } from "./viewportTheme";
export interface AetherisViewportProps {
  busyMessage?: string | null;
  errorMessage?: string | null;
  /** Bounded shared Telos developer comparison controls; spatial AA remains the default. */
  aaMode?: import("@aetheris/three-telos").TelosAAMode;
  aaDebug?: import("@aetheris/three-telos").TelosAADebug;
  /** Optional host inspection seam for integration/qualification; does not transfer camera ownership. */
  onHostReady?: (host: import("@aetheris/three-telos").TelosHost) => void;
  displayScene?: DisplayScene | null;
  highlightedFaceId?: number | null;
  highlightedEdgeId?: number | null;
  highlightedFaceIds?: Set<number>;
  highlightedEdgeIds?: Set<number>;
  showGrid?: boolean;
  showAxisGuide?: boolean;
  theme?: ViewportTheme;
  onPickRay?: (
    origin: { x: number; y: number; z: number },
    direction: { x: number; y: number; z: number },
  ) => void;
  cadmataArtifact?: CadmataVisualizationArtifact | null;
  cadmataLayers?: CadmataLayerVisibility;
  selectedCadmataIds?: Set<string>;
  onCadmataSelect?: (stableId: string) => void;
  showPmi?: boolean;
  pmiVisibility?: PmiVisibility;
  assemblyPacket?: AssemblyDisplayPacketDto | null;
  selectedAssemblyOccurrenceId?: string | null;
  onAssemblyOccurrenceSelect?: (stableId: string) => void;
}
