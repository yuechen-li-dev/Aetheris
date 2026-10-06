import { useEffect, useRef, useState } from "react";
import { TelosHost, clearColor } from "@aetheris/three-telos";
import {
  LegacyAetherisViewport,
  type AetherisViewportProps,
} from "./LegacyAetherisViewport";
import { cadmataTelosScene } from "./cadmataTelos";
import { ATELIER_VIEWPORT_THEME } from "./viewportTheme";
export type { AetherisViewportProps } from "./LegacyAetherisViewport";

/** Transitional WebGL remains for unsupported GPUs and unmigrated semantic authoring/PMI overlays. */
export function AetherisViewport(props: AetherisViewportProps) {
  const supported = typeof navigator !== "undefined" && "gpu" in navigator;
  const legacyReason = !supported
    ? "WebGPU unavailable"
    : props.cadmataArtifact
      ? "authoring overlays / PMI migration pending"
      : null;
  if (legacyReason)
    return (
      <div
        style={{ width: "100%", height: "100%", position: "relative" }}
        data-display-host="transitional-webgl"
        data-display-reason={legacyReason}
      >
        <LegacyAetherisViewport {...props} />
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
  return <TelosViewport {...props} />;
}

function TelosViewport(props: AetherisViewportProps) {
  const canvas = useRef<HTMLCanvasElement>(null),
    container = useRef<HTMLDivElement>(null),
    host = useRef<TelosHost | null>(null);
  const latest = useRef(props);
  const [diagnostic, setDiagnostic] = useState<string | null>(null);
  const lastModel = useRef<unknown>(undefined);
  const apply = () => {
    const current = host.current;
    if (!current) return;
    const value = latest.current,
      theme = value.theme ?? ATELIER_VIEWPORT_THEME;
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
      const ray = current.camera.worldRay(pixel),
        value = latest.current;
      value.onPickRay?.(
        { x: ray.origin.x, y: ray.origin.y, z: ray.origin.z },
        { x: ray.direction.x, y: ray.direction.y, z: ray.direction.z },
      );
      const hit = current.picker.pick(pixel);
      if (value.assemblyPacket && hit)
        value.onAssemblyOccurrenceSelect?.(hit.occurrenceId);
    };
    element.addEventListener("click", click);
    void TelosHost.create(element, setDiagnostic)
      .then((current) => {
        if (disposed) {
          current.dispose();
          return;
        }
        host.current = current;
        current.camera.mode = "orthographic";
        current.attach(container.current!);
        apply();
      })
      .catch((error) => {
        if (!disposed) setDiagnostic(String(error));
      });
    return () => {
      disposed = true;
      element.removeEventListener("click", click);
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
    >
      <canvas
        ref={canvas}
        aria-label="Engineering WebGPU viewport"
        style={{ display: "block", width: "100%", height: "100%" }}
      />
      {diagnostic && <div role="alert">{diagnostic}</div>}
    </div>
  );
}
