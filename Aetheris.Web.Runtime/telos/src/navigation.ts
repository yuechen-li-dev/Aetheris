import { Vector3, Quaternion } from "three";
import type { TelosCamera } from "./camera.js";

/** Inputs change Telos state directly, rather than copying authoritative camera state from controls. */
export function attachNavigation(
  canvas: HTMLCanvasElement,
  camera: TelosCamera,
  invalidate: () => void,
) {
  let drag: { x: number; y: number; button: number } | null = null;
  const down = (event: PointerEvent) => {
    drag = { x: event.clientX, y: event.clientY, button: event.button };
    canvas.setPointerCapture(event.pointerId);
  };
  const move = (event: PointerEvent) => {
    if (!drag) return;
    const dx = event.clientX - drag.x,
      dy = event.clientY - drag.y;
    drag.x = event.clientX;
    drag.y = event.clientY;
    const offset = camera.position.clone().sub(camera.target);
    if (drag.button === 0 && !event.shiftKey) {
      offset.applyQuaternion(
        new Quaternion().setFromAxisAngle(
          camera.up.clone().normalize(),
          -dx * 0.006,
        ),
      );
      const right = new Vector3().crossVectors(offset, camera.up).normalize();
      const next = offset
        .clone()
        .applyQuaternion(new Quaternion().setFromAxisAngle(right, -dy * 0.006));
      if (Math.abs(next.clone().normalize().dot(camera.up)) < 0.995)
        offset.copy(next);
      camera.position.copy(camera.target).add(offset);
    } else {
      const scale =
        (camera.mode === "orthographic"
          ? camera.span
          : offset.length() * Math.tan((camera.fov * Math.PI) / 360) * 2) /
        camera.height;
      const right = new Vector3().setFromMatrixColumn(camera.inverseView, 0),
        up = new Vector3().setFromMatrixColumn(camera.inverseView, 1);
      const delta = right
        .multiplyScalar(-dx * scale)
        .add(up.multiplyScalar(dy * scale));
      camera.position.add(delta);
      camera.target.add(delta);
    }
    camera.update();
    invalidate();
  };
  const up = () => {
    drag = null;
  };
  const wheel = (event: WheelEvent) => {
    event.preventDefault();
    const factor = Math.exp(Math.max(-1, Math.min(1, event.deltaY * 0.001)));
    if (camera.mode === "orthographic")
      camera.span = Math.max(1e-8, camera.span * factor);
    else {
      const offset = camera.position
        .clone()
        .sub(camera.target)
        .multiplyScalar(factor);
      if (
        offset.length() > camera.near * 2 &&
        offset.length() < camera.far * 0.9
      )
        camera.position.copy(camera.target).add(offset);
    }
    camera.update();
    invalidate();
  };
  const context = (event: MouseEvent) => event.preventDefault();
  canvas.style.touchAction = "none";
  canvas.addEventListener("pointerdown", down);
  canvas.addEventListener("pointermove", move);
  canvas.addEventListener("pointerup", up);
  canvas.addEventListener("pointercancel", up);
  canvas.addEventListener("wheel", wheel, { passive: false });
  canvas.addEventListener("contextmenu", context);
  return () => {
    canvas.removeEventListener("pointerdown", down);
    canvas.removeEventListener("pointermove", move);
    canvas.removeEventListener("pointerup", up);
    canvas.removeEventListener("pointercancel", up);
    canvas.removeEventListener("wheel", wheel);
    canvas.removeEventListener("contextmenu", context);
  };
}
