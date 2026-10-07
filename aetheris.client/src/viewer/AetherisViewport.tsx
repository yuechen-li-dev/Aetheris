import { lazy, Suspense, useEffect, useRef, useState } from "react";
import { TelosHost, clearColor } from "@aetheris/three-telos";
import type { AetherisViewportProps } from "./viewportProps";
import { PmiAnnotationLayer } from "./PmiAnnotationLayer";
import { DEFAULT_PMI_VISIBILITY } from "./pmiPresentation";
const LegacyAetherisViewport = lazy(() =>
  import("./LegacyAetherisViewport").then((module) => ({
    default: module.LegacyAetherisViewport,
  })),
);
import { cadmataTelosScene } from "./cadmataTelos";
import { ATELIER_VIEWPORT_THEME } from "./viewportTheme";
import { reportDesktopDiagnostic } from "../desktopDiagnostics";
export type { AetherisViewportProps } from "./viewportProps";

/** One graphics authority; legacy rendering is loaded only for unsupported browsers. */
export function AetherisViewport(props: AetherisViewportProps) {
  const [generation, setGeneration] = useState(0);
  const supported = typeof navigator !== "undefined" && !!navigator.gpu;
  const legacyReason = !supported ? "WebGPU unavailable" : null;
  if (legacyReason)
    return (
      <div
        style={{ width: "100%", height: "100%", position: "relative" }}
        data-display-host="transitional-webgl"
        data-display-reason={legacyReason}
      >
        <Suspense fallback={<small>Loading browser fallback…</small>}>
          <LegacyAetherisViewport {...props} />
        </Suspense>
        <small
          style={{
            position: "absolute",
            bottom: 4,
            left: 8,
            pointerEvents: "none",
          }}
        >
          Transitional WebGL: {legacyReason}
        </small>
      </div>
    );
  return (
    <TelosViewport
      key={generation}
      {...props}
      onRetry={() => setGeneration((value) => value + 1)}
    />
  );
}

function TelosViewport(props: AetherisViewportProps & { onRetry(): void }) {
  const canvas = useRef<HTMLCanvasElement>(null),
    container = useRef<HTMLDivElement>(null),
    host = useRef<TelosHost | null>(null);
  const latest = useRef(props);
  const [diagnostic, setDiagnostic] = useState<string | null>(null);
  const [labelHost, setLabelHost] = useState<TelosHost | null>(null);
  const lastModel = useRef<unknown>(undefined);
  const apply = () => {
    const current = host.current;
    if (!current) return;
    const value = latest.current,
      theme = value.theme ?? ATELIER_VIEWPORT_THEME;
    current.setAA(value.aaMode ?? "SpatialOnly", value.aaDebug ?? "color");
    current.setScene(cadmataTelosScene(value));
    current.background = clearColor(theme.sceneBackground);
    current.grid = value.showGrid !== false && theme.gridStyle.enabled;
    const model = value.assemblyPacket ?? value.displayScene;
    if (lastModel.current !== model) {
      lastModel.current = model;
      current.fit();
    }
    current.invalidate();
  };
  useEffect(() => {
    let disposed = false;
    const initialization = new AbortController();
    const element = canvas.current;
    if (!element) return;
    const click = (event: MouseEvent) => {
      const current = host.current;
      if (!current) return;
      const rect = element.getBoundingClientRect(),
        pixel: [number, number] = [
          event.clientX - rect.left,
          event.clientY - rect.top,
        ];
      const hit = current.picker.pick(pixel);
      if (hit?.overlayId) {
        latest.current.onCadmataSelect?.(hit.overlayId);
        return;
      }
      const ray = current.camera.worldRay(pixel),
        value = latest.current;
      value.onPickRay?.(
        { x: ray.origin.x, y: ray.origin.y, z: ray.origin.z },
        { x: ray.direction.x, y: ray.direction.y, z: ray.direction.z },
      );
      if (value.assemblyPacket && hit)
        value.onAssemblyOccurrenceSelect?.(hit.occurrenceId);
    };
    element.addEventListener("click", click);
    const hover = (event: PointerEvent) => {
      const current = host.current;
      if (!current || event.buttons) return;
      const rect = element.getBoundingClientRect();
      const hit =
        event.type === "pointerleave"
          ? null
          : current.picker.pick([
              event.clientX - rect.left,
              event.clientY - rect.top,
            ]);
      let changed = false;
      for (const mesh of current.scene.meshes) {
        const hovered =
          !!hit &&
          !mesh.overlay &&
          mesh.identity.occurrenceId === hit.occurrenceId &&
          (hit.faceId === undefined || mesh.identity.faceId === hit.faceId);
        changed ||= mesh.hovered !== hovered;
        mesh.hovered = hovered;
      }
      if (changed) current.invalidate();
    };
    element.addEventListener("pointermove", hover);
    element.addEventListener("pointerleave", hover);
    void TelosHost.create(element, setDiagnostic, initialization.signal)
      .then((current) => {
        if (disposed) {
          current.dispose();
          return;
        }
        host.current = current;
        reportDesktopDiagnostic("renderer", { host: "three-telos", aa: latest.current.aaMode ?? "SpatialOnly" });
        setLabelHost(current);
        current.camera.mode = "orthographic";
        current.camera.up.set(0, 0, 1);
        current.gridPlane = "xy";
        current.attach(container.current!);
        apply();
        latest.current.onHostReady?.(current);
      })
      .catch((error) => {
        reportDesktopDiagnostic("renderer-failure", String(error));
        if (!disposed) setDiagnostic(String(error));
      });
    return () => {
      disposed = true;
      initialization.abort();
      element.removeEventListener("click", click);
      element.removeEventListener("pointermove", hover);
      element.removeEventListener("pointerleave", hover);
      host.current?.dispose();
      host.current = null;
    };
  }, []);
  useEffect(() => {
    latest.current = props;
    apply();
  }, [props]);
  return (
    <div
      ref={container}
      style={{ width: "100%", height: "100%", position: "relative" }}
      data-display-host="three-telos"
      aria-busy={!labelHost || !!props.busyMessage}
      className="telos-viewport"
    >
      <canvas
        ref={canvas}
        aria-label="Engineering WebGPU viewport"
        style={{ display: "block", width: "100%", height: "100%" }}
      />
      {!!props.assemblyPacket?.display?.cameras?.length && (
        <select
          aria-label="Scene camera"
          style={{ position: "absolute", left: 12, top: 12, zIndex: 2 }}
          defaultValue=""
          onChange={(event) => {
            const camera = props.assemblyPacket?.display?.cameras?.find(
              (c) => c.name === event.target.value,
            );
            if (camera && host.current) {
              host.current.camera.applyDisplayCamera(camera);
              host.current.invalidate();
            } else if (!event.target.value && host.current) {
              host.current.fit();
              host.current.invalidate();
            }
          }}
        >
          <option value="">Fit view</option>
          {props.assemblyPacket.display.cameras.map((c) => (
            <option key={c.name}>{c.name}</option>
          ))}
        </select>
      )}
      {!diagnostic && (!labelHost || props.busyMessage) && (
        <div className="telos-status" role="status">
          {props.busyMessage ?? "Preparing viewport…"}
        </div>
      )}
      {labelHost &&
        !props.busyMessage &&
        !props.displayScene?.renderables.length &&
        !props.assemblyPacket && (
          <div
            className="telos-empty"
            style={{
              color: (props.theme ?? ATELIER_VIEWPORT_THEME).annotation.text,
            }}
          >
            <strong>Open a model to begin</strong>
            <span>
              Import a STEP file or choose a model from the Product Gallery.
            </span>
            <small>Drag to orbit · Shift-drag to pan · Scroll to zoom</small>
          </div>
        )}
      {(diagnostic || props.errorMessage) && (
        <div className="telos-status telos-error" role="alert">
          {diagnostic
            ? "The viewport could not render. Your model is retained."
            : props.errorMessage}
          {diagnostic && (
            <>
              <details>
                <summary>Details</summary>
                {diagnostic}
              </details>
              <button onClick={props.onRetry}>Restart viewport</button>
            </>
          )}
        </div>
      )}
      <PmiAnnotationLayer
        host={labelHost}
        artifact={props.cadmataArtifact ?? null}
        visible={props.showPmi !== false}
        visibility={props.pmiVisibility ?? DEFAULT_PMI_VISIBILITY}
        selectedIds={props.selectedCadmataIds ?? EMPTY_SELECTION}
        onSelect={props.onCadmataSelect ?? NO_SELECT}
        theme={props.theme ?? ATELIER_VIEWPORT_THEME}
        showAxisGuide={props.showAxisGuide !== false}
      />
    </div>
  );
}
const EMPTY_SELECTION = new Set<string>();
const NO_SELECT = () => {};
