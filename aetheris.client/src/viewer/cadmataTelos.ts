import {
  materialFromAppearance,
  identityTransform,
  type TelosScene,
  type TelosMesh,
  type TelosLine,
  type NumericArray,
} from "@aetheris/three-telos";
import type { AetherisViewportProps } from "./viewportProps";
import { cadmataOverlays } from "./cadmataOverlays";
import { ATELIER_VIEWPORT_THEME } from "./viewportTheme";
import {
  mapFacePatchToRenderFacePatch,
  type RenderFacePatch,
} from "./tessellationMapper";
import type { AssemblyDisplayPacketDto } from "../api/aetherisApi";

// Definitions are immutable within a packet. Selection updates reuse both CPU arrays and GPU buffers.
const assemblyGeometry = new WeakMap<
  AssemblyDisplayPacketDto,
  Map<string, RenderFacePatch[]>
>();

/** Product packet interpretation only. Camera, drawing and engineering picking live in Telos. */
export function cadmataTelosScene(props: AetherisViewportProps): TelosScene {
  const theme = props.theme ?? ATELIER_VIEWPORT_THEME;
  const meshes: TelosMesh[] = [],
    lines: TelosLine[] = [];
  const material = materialFromAppearance(theme.objectMaterial);
  const addFace = (
    patch: RenderFacePatch,
    id: string,
    occurrenceId: string,
    transform: NumericArray = identityTransform(),
    selected = false,
  ) => {
    meshes.push({
      definition: {
        id,
        positions: patch.positions,
        normals: patch.normals,
        indices: patch.indices,
      },
      identity: { occurrenceId, faceId: patch.faceId },
      transform,
      material,
      selected,
    });
  };
  for (const renderable of props.displayScene?.renderables ?? []) {
    const selected =
      props.highlightedFaceIds?.has(renderable.faceId) ??
      props.highlightedFaceId === renderable.faceId;
    if (renderable.kind === "MeshPatch")
      addFace(
        renderable.mesh,
        "face:" + renderable.faceId,
        "part",
        identityTransform(),
        selected,
      );
    else if (renderable.kind === "AnalyticPatch")
      addFace(
        renderable.previewMesh,
        "face:" + renderable.faceId,
        "part",
        identityTransform(),
        selected,
      );
    else if (renderable.kind === "WirePatch")
      for (const edge of renderable.wires)
        lines.push({
          id: "edge:" + edge.edgeId,
          points: edge.points,
          identity: {
            occurrenceId: "part",
            faceId: renderable.faceId,
            edgeId: edge.edgeId,
          },
          widthPixels: theme.edgeStyle.width,
          color: materialFromAppearance({
            color: theme.edgeStyle.color,
          }).baseColor.concat(1),
          selected:
            selected ||
            (props.highlightedEdgeIds?.has(edge.edgeId) ??
              props.highlightedEdgeId === edge.edgeId),
        });
  }
  const packet = props.assemblyPacket;
  if (packet) {
    let definitions = assemblyGeometry.get(packet);
    if (!definitions) {
      definitions = new Map(
        packet.definitions.map((definition) => [
          definition.stableId,
          definition.facePatches.map(mapFacePatchToRenderFacePatch),
        ]),
      );
      assemblyGeometry.set(packet, definitions);
    }
    const selection = new Set(
      packet.occurrences.find(
        (item) => item.stableId === props.selectedAssemblyOccurrenceId,
      )?.selectionMembers ?? [props.selectedAssemblyOccurrenceId],
    );
    for (const occurrence of packet.occurrences)
      if (occurrence.definitionStableId)
        for (const patch of definitions.get(occurrence.definitionStableId) ??
          []) {
          addFace(
            patch,
            occurrence.definitionStableId + ":" + patch.faceId,
            occurrence.stableId,
            occurrence.worldTransform,
            selection.has(occurrence.stableId),
          );
        }
  }
  if (props.showAxisGuide !== false)
    for (const [axis, color] of [
      theme.axis.x,
      theme.axis.y,
      theme.axis.z,
    ].entries()) {
      const endpoint = [0, 0, 0];
      endpoint[axis] = 2;
      lines.push({
        id: "axis:" + axis,
        points: [0, 0, 0, ...endpoint],
        identity: { occurrenceId: "axis:" + axis },
        widthPixels: 1,
        color: materialFromAppearance({ color }).baseColor.concat(1),
        overlay: true,
      });
    }
  const overlays = cadmataOverlays(props);
  return { meshes: [...meshes, ...overlays.meshes], lines: [...lines, ...overlays.lines], fields: [] };
}
