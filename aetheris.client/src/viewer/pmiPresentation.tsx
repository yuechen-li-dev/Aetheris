/* eslint-disable react-refresh/only-export-components -- pure presentation helpers and content share the semantic contract. */
import type {
  CadmataEntity,
  CadmataVisualizationArtifact,
} from "./conceptVisualization";
import {
  formatPmiLabel,
  SEMANTIC_PMI_KINDS,
  indexSemanticInspection,
} from "./semanticInspection";
/** Geometry selection highlights published related PMI without broadening the topology pick. */
export function pmiPresentationSelection(
  artifact: CadmataVisualizationArtifact | null,
  selected: Set<string>,
) {
  if (
    !artifact ||
    artifact.entities.some(
      (entity) =>
        selected.has(entity.stableId) && SEMANTIC_PMI_KINDS.has(entity.kind),
    )
  )
    return selected;
  const result = new Set(selected),
    index = indexSemanticInspection(artifact);
  for (const id of selected) {
    const entity = index.byId.get(id);
    if (!entity?.kind.startsWith("BRep")) continue;
    const owners = [
      ...(entity.topology?.faceIds ?? []).flatMap(
        (face) => index.faceOwners.get(face) ?? [],
      ),
      ...(entity.topology?.edgeIds ?? []).flatMap(
        (edge) => index.edgeOwners.get(edge) ?? [],
      ),
    ];
    for (const owner of owners) {
      if (SEMANTIC_PMI_KINDS.has(owner.kind)) result.add(owner.stableId);
      for (const pmi of index.pmiByTarget.get(owner.stableId) ?? [])
        result.add(pmi.stableId);
    }
  }
  return result;
}
export type PmiCategory =
  | "datums"
  | "dimensions"
  | "geometricTolerances"
  | "engineeringAnnotations";
export type PmiVisibility = Record<PmiCategory, boolean>;
export const DEFAULT_PMI_VISIBILITY: PmiVisibility = {
  datums: true,
  dimensions: false,
  geometricTolerances: true,
  engineeringAnnotations: false,
};

export type Point = [number, number, number];
type ScreenPoint = { x: number; y: number; z: number };
export type LayoutItem = {
  entity: CadmataEntity;
  anchor: Point;
  width: number;
  height: number;
};

function anchor(entity: CadmataEntity): Point | null {
  const geometry = entity.geometry;
  if (geometry?.type === "circle")
    return [geometry.center.x, geometry.center.y, geometry.center.z];
  if (geometry?.type === "point")
    return [geometry.point.x, geometry.point.y, geometry.point.z];
  if (geometry?.type === "polyline" && geometry.points.length) {
    const point = geometry.points[geometry.points.length - 1];
    return [point.x, point.y, point.z];
  }
  return null;
}

export function pmiCategory(entity: CadmataEntity): PmiCategory | null {
  if (entity.kind === "Datum") return "datums";
  if (
    entity.kind === "Dimension" ||
    entity.kind === "Diameter" ||
    entity.kind === "HoleDiameter"
  )
    return "dimensions";
  if (entity.kind === "Position") return "geometricTolerances";
  if (entity.kind === "Annotation") return "engineeringAnnotations";
  return null;
}

function priority(entity: CadmataEntity) {
  const category = pmiCategory(entity);
  return category === "datums"
    ? 0
    : category === "geometricTolerances"
      ? 1
      : category === "dimensions"
        ? 2
        : 3;
}

function estimatedSize(entity: CadmataEntity) {
  const category = pmiCategory(entity);
  if (category === "datums") return { width: 86, height: 38 };
  if (category === "geometricTolerances") return { width: 190, height: 54 };
  if (category === "engineeringAnnotations") return { width: 230, height: 82 };
  return { width: 166, height: 48 };
}

export function semanticPmiItems(
  artifact: CadmataVisualizationArtifact,
  visibility: PmiVisibility,
): LayoutItem[] {
  return artifact.entities
    .filter((entity) => SEMANTIC_PMI_KINDS.has(entity.kind))
    .filter((entity) => {
      const category = pmiCategory(entity);
      return category !== null && visibility[category];
    })
    .map((entity) => {
      const resolvedAnchor = anchor(entity);
      return resolvedAnchor
        ? { entity, anchor: resolvedAnchor, ...estimatedSize(entity) }
        : null;
    })
    .filter((item): item is LayoutItem => item !== null)
    .sort(
      (left, right) =>
        priority(left.entity) - priority(right.entity) ||
        left.entity.stableId.localeCompare(right.entity.stableId),
    );
}

function overlaps(
  a: { x: number; y: number; width: number; height: number },
  b: { x: number; y: number; width: number; height: number },
) {
  return (
    Math.abs(a.x - b.x) < (a.width + b.width) / 2 + 8 &&
    Math.abs(a.y - b.y) < (a.height + b.height) / 2 + 6
  );
}

/** Deterministic bounded greedy screen-space placement. */
export function layoutPmiCallouts(
  items: readonly (LayoutItem & { screenAnchor: ScreenPoint })[],
  width: number,
  height: number,
  manualOffsets: ReadonlyMap<string, { x: number; y: number }>,
) {
  const placed: { x: number; y: number; width: number; height: number }[] = [];
  return items.map((item, index) => {
    const manual = manualOffsets.get(item.entity.stableId);
    const radialCandidates = Array.from({ length: 32 }, (_, attempt) => {
      const ring = Math.floor(attempt / 8);
      const angle = ((attempt + index * 3) % 8) * (Math.PI / 4);
      const radius = 68 + ring * 58;
      return {
        x: item.screenAnchor.x + Math.cos(angle) * radius,
        y: item.screenAnchor.y + Math.sin(angle) * radius,
      };
    });
    const gridCandidates = Array.from({ length: 10 }, (_, slot) => ({
      x: slot % 2 === 0 ? item.width / 2 + 18 : width - item.width / 2 - 18,
      y: 106 + Math.floor(slot / 2) * Math.max(item.height + 14, 92),
    }));
    const candidates = manual
      ? [
          {
            x: item.screenAnchor.x + manual.x,
            y: item.screenAnchor.y + manual.y,
          },
        ]
      : [...radialCandidates, ...gridCandidates];
    let candidate = candidates.find((point) => {
      const box = { ...point, width: item.width, height: item.height };
      return (
        point.x - item.width / 2 >= 8 &&
        point.x + item.width / 2 <= width - 8 &&
        point.y - item.height / 2 >= 56 &&
        point.y + item.height / 2 <= height - 8 &&
        !placed.some((other) => overlaps(box, other))
      );
    });
    const hidden =
      !candidate && priority(item.entity) >= 3 && items.length > 10;
    candidate ??= candidates[candidates.length - 1];
    candidate = {
      x: Math.max(
        item.width / 2 + 8,
        Math.min(width - item.width / 2 - 8, candidate.x),
      ),
      y: Math.max(
        item.height / 2 + 56,
        Math.min(height - item.height / 2 - 8, candidate.y),
      ),
    };
    if (!hidden)
      placed.push({ ...candidate, width: item.width, height: item.height });
    return { ...candidate, hidden };
  });
}

export function CalloutContent({ entity }: { entity: CadmataEntity }) {
  if (entity.kind === "Datum")
    return (
      <>
        <strong className="pmi-callout__datum">
          {entity.label.replace(/^Datum\s+/i, "")}
        </strong>
        <span>DATUM</span>
      </>
    );
  if (entity.kind === "Position")
    return (
      <>
        <strong>
          POSITION · ⌀{String(entity.metadata?.nominal ?? "?")}{" "}
          {String(entity.metadata?.unit ?? "mm")}
        </strong>
        <small>
          {String(entity.metadata?.datumRefs ?? "No datum frame")} ·{" "}
          {String(entity.metadata?.target ?? "")}
        </small>
      </>
    );
  if (entity.kind === "Annotation")
    return (
      <>
        <strong>{entity.label}</strong>
        <small>{String(entity.metadata?.text ?? "")}</small>
      </>
    );
  return (
    <>
      <strong>
        {formatPmiLabel(entity)} {String(entity.metadata?.unit ?? "")}
      </strong>
      <small>{String(entity.metadata?.target ?? "")}</small>
    </>
  );
}
