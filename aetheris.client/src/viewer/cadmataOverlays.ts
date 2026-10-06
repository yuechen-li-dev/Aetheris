import { SphereGeometry, Matrix4, Vector3 } from "three";
import {
  materialFromAppearance,
  type TelosGeometry,
  type TelosScene,
  type TelosMesh,
  type TelosLine,
} from "@aetheris/three-telos";
import type { CadmataEntity } from "./conceptVisualization";
import type { AetherisViewportProps } from "./viewportProps";
import { SEMANTIC_PMI_KINDS } from "./semanticInspection";
import { ATELIER_VIEWPORT_THEME } from "./viewportTheme";

const sphere = new SphereGeometry(1.8, 12, 8);
const pointDefinition: TelosGeometry = {
  id: "authoring-point/1",
  positions: sphere.attributes.position.array,
  normals: sphere.attributes.normal.array,
  indices: sphere.index!.array,
};
sphere.dispose();
const shapes = new WeakMap<
  CadmataEntity,
  { definition?: TelosGeometry; points?: number[]; transform?: number[] }
>();
const xyz = (point: { x: number; y: number; z: number }) => [
  point.x,
  point.y,
  point.z,
];
function shape(entity: CadmataEntity) {
  const existing = shapes.get(entity);
  if (existing) return existing;
  const geometry = entity.geometry;
  const result: {
    definition?: TelosGeometry;
    points?: number[];
    transform?: number[];
  } = {};
  if (geometry?.type === "point") {
    result.definition = pointDefinition;
    result.transform = new Matrix4().makeTranslation(
      ...(xyz(geometry.point) as [number, number, number]),
    ).elements;
  } else if (geometry?.type === "plane") {
    const origin = new Vector3(...xyz(geometry.origin)),
      u = new Vector3(...xyz(geometry.u)),
      v = new Vector3(...xyz(geometry.v));
    const normal = u.clone().cross(v).normalize().toArray();
    result.definition = {
      id: "overlay:" + entity.stableId,
      positions: [-1, 1].flatMap((a) =>
        [-1, 1].flatMap((b) =>
          origin.clone().addScaledVector(u, a).addScaledVector(v, b).toArray(),
        ),
      ),
      normals: Array.from({ length: 4 }, () => normal).flat(),
      indices: [0, 2, 1, 1, 2, 3],
    };
  } else if (geometry?.type === "circle") {
    // Preserve the current XY circle authoring convention.
    result.points = Array.from({ length: 49 }, (_, i) => [
      geometry.center.x + Math.cos((i / 48) * Math.PI * 2) * geometry.radius,
      geometry.center.y + Math.sin((i / 48) * Math.PI * 2) * geometry.radius,
      geometry.center.z,
    ]).flat();
  } else if (geometry?.type === "polyline" && geometry.points.length) {
    result.points = (
      geometry.closed
        ? [...geometry.points, geometry.points[0]]
        : geometry.points
    ).flatMap(xyz);
  }
  shapes.set(entity, result);
  return result;
}

/** Compiler-owned world geometry; selection and layer policy remain in Cadmata. */
export function cadmataOverlays(props: AetherisViewportProps): TelosScene {
  const meshes: TelosMesh[] = [],
    lines: TelosLine[] = [];
  const theme = props.theme ?? ATELIER_VIEWPORT_THEME;
  if (!props.cadmataLayers) return { meshes, lines, fields: [] };
  for (const entity of props.cadmataArtifact?.entities ?? []) {
    if (
      !props.cadmataLayers[entity.layer] ||
      SEMANTIC_PMI_KINDS.has(entity.kind) ||
      entity.kind === "EngineeringTarget"
    )
      continue;
    const selected = props.selectedCadmataIds?.has(entity.stableId);
    const color = selected
      ? theme.overlay.selection
      : entity.layer === "profileGuides" || entity.layer === "profileLoops"
        ? theme.overlay.profile
        : entity.layer === "composeRegions"
          ? theme.overlay.compose
          : entity.layer === "diagnostics"
            ? theme.overlay.diagnostic
            : theme.overlay.concept;
    const identity = { occurrenceId: "authoring", overlayId: entity.stableId };
    const data = shape(entity);
    if (data.definition)
      meshes.push({
        definition: data.definition,
        transform: data.transform,
        identity,
        material: {
          ...materialFromAppearance({ color }),
          opacity: entity.geometry?.type === "plane" ? 0.12 : 1,
        },
        overlay: true,
        unlit: true,
        depthMode:
          entity.geometry?.type === "point" ? "always-on-top" : "depth-tested",
      });
    if (data.points)
      lines.push({
        id: "overlay:" + entity.stableId,
        points: data.points,
        identity,
        overlay: true,
        depthMode: "depth-biased",
        color: materialFromAppearance({ color }).baseColor.concat(1),
        widthPixels: selected
          ? 4
          : entity.geometry?.type === "circle"
            ? 1.5
            : entity.layer === "profileLoops"
              ? 2.5
              : 1.25,
      });
  }
  return { meshes, lines, fields: [] };
}
