import type { Vec3 } from "./contracts.js";

export const TELOS_BACKDROPS = ["studio", "paper", "mars", "sirius", "singularity", "aeons", "blueprint", "aurora"] as const;
/** Display presentation only. No model transforms, topology or camera commands. */
export interface TelosPresentation {
  id: string;
  backdrop: { kind: typeof TELOS_BACKDROPS[number]; base: Vec3; accent: Vec3; intensity: number; vignette: number };
  lighting: {
    ambient: number; exposure: number;
    sky: Vec3; ground: Vec3; hemisphere: number;
    key: Vec3; keyColor: Vec3; keyIntensity: number;
    fill: Vec3; fillColor: Vec3; fillIntensity: number;
    rim: Vec3; rimIntensity: number;
    selection: Vec3;
  };
  grid: { minor: Vec3; major: Vec3; minorOpacity: number; majorOpacity: number;
    majorStep: number; targetCells: number; maxLines: number; extentScale: number; offset: number };
}

/** Background/grid colours are display encoded; lighting colours are linear. */
export function presentationUniform(presentation: TelosPresentation, width: number, height: number): Float32Array<ArrayBuffer> {
  return new Float32Array([width, height, TELOS_BACKDROPS.indexOf(presentation.backdrop.kind), presentation.backdrop.intensity,
    ...presentation.backdrop.base, 1, ...presentation.backdrop.accent, 1, presentation.backdrop.vignette, 0, 0, 0]);
}
