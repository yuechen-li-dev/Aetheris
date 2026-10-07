import { Color } from "three";
import type { TelosPresentation, Vec3 } from "@aetheris/three-telos";
import type { ViewportTheme } from "./viewportTheme";

const linear = (value: string) => new Color(value).toArray() as unknown as Vec3;
const encoded = (value: string) => new Color(value).convertLinearToSRGB().toArray() as unknown as Vec3;
const cache = new WeakMap<ViewportTheme, TelosPresentation>();
/** Project the existing product theme into the shared WebGPU presentation contract. */
export function telosPresentation(theme: ViewportTheme): TelosPresentation {
  let value = cache.get(theme);
  if (value) return value;
  const l = theme.lights, g = theme.gridStyle;
  const kind = theme.id === "blueprint" || theme.id === "aurora" ? theme.id
    : theme.id === "monument" ? "paper" : theme.background.kind === "flat" ? "studio" : theme.background.kind;
  value = { id: theme.id,
    backdrop: { kind, base: encoded(theme.sceneBackground), accent: encoded(theme.background.accent),
      intensity: theme.background.kind === "flat" ? 1 : theme.background.intensity, vignette: theme.postProcess.vignette },
    lighting: { ambient:l.ambient, exposure:theme.environment.toneMappingExposure,
      sky:linear(l.hemisphereSky), ground:linear(l.hemisphereGround), hemisphere:l.hemisphereIntensity,
      key:l.keyPosition, keyColor:linear(l.keyColor), keyIntensity:l.keyIntensity,
      fill:l.fillPosition, fillColor:linear(l.fillColor), fillIntensity:l.fillIntensity,
      rim:linear(l.rimColor), rimIntensity:l.rimIntensity, selection:linear(theme.selectedMaterial.color) },
    grid:{ minor:encoded(g.minorColor), major:encoded(g.majorColor), minorOpacity:g.minorOpacity, majorOpacity:g.majorOpacity,
      majorStep:g.majorStep, targetCells:g.targetCellCount, maxLines:g.maxLinesPerAxis, extentScale:g.extentScale, offset:g.yOffset },
  };
  cache.set(theme,value); return value;
}
