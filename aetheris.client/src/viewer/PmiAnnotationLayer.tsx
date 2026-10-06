import { useEffect, useMemo, useRef } from "react";
import {
  materialFromAppearance,
  type TelosHost,
  type TelosLine,
} from "@aetheris/three-telos";
import type { CadmataVisualizationArtifact } from "./conceptVisualization";
import type { ViewportTheme } from "./viewportTheme";
import {
  semanticPmiItems,
  layoutPmiCallouts,
  pmiCategory,
  CalloutContent,
  pmiPresentationSelection,
  type PmiVisibility,
} from "./pmiPresentation";

/** DOM text is world-anchored, screen-sized and always-on-top. Telos owns camera and leader resources. */
export function PmiAnnotationLayer({
  host,
  artifact,
  visible,
  visibility,
  selectedIds,
  onSelect,
  theme,
  showAxisGuide = true,
}: {
  host: TelosHost | null;
  artifact: CadmataVisualizationArtifact | null;
  visible: boolean;
  visibility: PmiVisibility;
  selectedIds: Set<string>;
  onSelect: (id: string) => void;
  theme: ViewportTheme;
  showAxisGuide?: boolean;
}) {
  const items = useMemo(
    () => (visible && artifact ? semanticPmiItems(artifact, visibility) : []),
    [artifact, visible, visibility],
  );
  const highlighted = useMemo(
    () => pmiPresentationSelection(artifact, selectedIds),
    [artifact, selectedIds],
  );
  const buttons = useRef(new Map<string, HTMLButtonElement>());
  const axes = useRef(new Map<number, HTMLSpanElement>());
  const manual = useRef(new Map<string, { x: number; y: number }>());
  const offsets = useRef(new Map<string, { x: number; y: number }>());
  const drag = useRef<{
    id: string;
    x: number;
    y: number;
    initial: { x: number; y: number };
  } | null>(null);
  useEffect(() => {
    if (!host) return;
    let signature = "";
    const update = () => {
      const camera = host.camera;
      const key = JSON.stringify([
        camera.viewProjection.elements,
        camera.width,
        camera.height,
        [...manual.current],
      ]);
      if (key === signature) return;
      signature = key;
      const projected = items.map((item) => {
        const [x, y, z] = camera.project(item.anchor);
        return { ...item, screenAnchor: { x, y, z } };
      });
      const layout = layoutPmiCallouts(
        projected,
        camera.width,
        camera.height,
        manual.current,
      );
      const lines: TelosLine[] = [];
      projected.forEach((item, i) => {
        const placement = layout[i],
          element = buttons.current.get(item.entity.stableId);
        const shown =
          !placement.hidden &&
          item.screenAnchor.z >= 0 &&
          item.screenAnchor.z <= 1 &&
          Number.isFinite(item.screenAnchor.x);
        if (element) {
          element.style.display = shown ? "" : "none";
          element.style.left = placement.x + "px";
          element.style.top = placement.y + "px";
        }
        offsets.current.set(item.entity.stableId, {
          x: placement.x - item.screenAnchor.x,
          y: placement.y - item.screenAnchor.y,
        });
        if (!shown) return;
        const label = camera.unproject(
          [placement.x, placement.y],
          item.screenAnchor.z,
        );
        lines.push({
          id: "pmi:" + item.entity.stableId,
          identity: { occurrenceId: "pmi", overlayId: item.entity.stableId },
          points: [...item.anchor, ...label.toArray()],
          overlay: true,
          depthMode: "always-on-top",
          color: materialFromAppearance({
            color: theme.annotation.leader,
          }).baseColor.concat(1),
          widthPixels: highlighted.has(item.entity.stableId) ? 2.5 : 1.2,
        });
      });
      host.setDynamicLines(lines);
      for (const [axis, element] of axes.current) {
        const point = [0, 0, 0];
        point[axis] = 2;
        const [x, y, z] = camera.project(point);
        element.style.display = z >= 0 && z <= 1 ? "" : "none";
        element.style.left = x + "px";
        element.style.top = y + "px";
      }
    };
    const detach = host.beforeFrame(update);
    const move = (event: PointerEvent) => {
      const current = drag.current;
      if (!current) return;
      manual.current.set(current.id, {
        x: current.initial.x + event.clientX - current.x,
        y: current.initial.y + event.clientY - current.y,
      });
      host.invalidate();
    };
    const up = () => {
      drag.current = null;
    };
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up);
    window.addEventListener("pointercancel", up);
    return () => {
      detach();
      host.invalidate();
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", up);
      window.removeEventListener("pointercancel", up);
    };
  }, [host, items, highlighted, theme, showAxisGuide]);
  useEffect(
    () => () => {
      host?.setDynamicLines([]);
      host?.invalidate();
    },
    [host],
  );
  return (
    <div
      data-telos-labels="world-anchored-screen-sized"
      style={{
        position: "absolute",
        inset: 0,
        overflow: "hidden",
        pointerEvents: "none",
      }}
    >
      {items.map(({ entity }) => {
        const category = pmiCategory(entity)!;
        const selected = highlighted.has(entity.stableId);
        const color = selected
          ? theme.annotation.selected
          : category === "datums"
            ? theme.annotation.datum
            : category === "dimensions"
              ? theme.annotation.dimension
              : theme.annotation.text;
        return (
          <button
            key={entity.stableId}
            ref={(element) => {
              if (element) buttons.current.set(entity.stableId, element);
              else buttons.current.delete(entity.stableId);
            }}
            className={`pmi-callout pmi-callout--${category}${!entity.topology?.faceIds?.length ? " pmi-callout--global" : ""}${selected ? " is-selected" : ""}`}
            data-pmi-id={entity.stableId}
            type="button"
            aria-label={`Inspect ${entity.label}`}
            title="Select; drag to adjust presentation only"
            style={{
              position: "absolute",
              display: "none",
              transform: "translate(-50%, -50%)",
              pointerEvents: "auto",
              zIndex: selected ? 90 : 60,
              color,
              background: theme.annotation.background,
              borderColor: color,
            }}
            onPointerDown={(event) => {
              event.stopPropagation();
              event.currentTarget.setPointerCapture(event.pointerId);
              drag.current = {
                id: entity.stableId,
                x: event.clientX,
                y: event.clientY,
                initial: offsets.current.get(entity.stableId) ?? { x: 0, y: 0 },
              };
            }}
            onClick={(event) => {
              event.stopPropagation();
              onSelect(entity.stableId);
            }}
          >
            <CalloutContent entity={entity} />
          </button>
        );
      })}
      {showAxisGuide &&
        [theme.axis.x, theme.axis.y, theme.axis.z].map((color, axis) => (
          <span
            key={axis}
            ref={(element) => {
              if (element) axes.current.set(axis, element);
              else axes.current.delete(axis);
            }}
            style={{
              position: "absolute",
              color,
              fontSize: 12,
              display: "none",
            }}
          >
            {"XYZ"[axis]}
          </span>
        ))}
    </div>
  );
}
