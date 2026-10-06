import { Matrix4 } from "three";
import {
  TelosHost,
  materialFromAppearance,
  type TelosAAMode,
  type TelosScene,
  type TelosField,
  type TelosShaderArtifact,
} from "../src/index.js";
const errors: string[] = [];
const host = await TelosHost.create(
  document.querySelector<HTMLCanvasElement>("#view")!,
  (error) => errors.push(error),
);
const reference = await TelosHost.create(
  document.querySelector<HTMLCanvasElement>("#reference")!,
  (error) => errors.push(error),
);
if (!host.temporal)
  throw new Error("temporal pipeline unavailable: " + errors.join("; "));
// Explicit frame stepping, no browser RAF may add unrecorded samples to the experiment.
host.invalidate = () => {};
reference.invalidate = () => {};
for (const viewport of [host, reference]) {
  viewport.resize(512, 512, 1);
  viewport.camera.position.set(0, 0, 5);
  viewport.camera.target.set(0, 0, 0);
  viewport.camera.near = 0.1;
  viewport.camera.far = 30;
  viewport.camera.mode = "orthographic";
  viewport.camera.span = 4;
  viewport.camera.update();
  viewport.background = [0.02, 0.02, 0.02, 1];
}
const artifact = (await (
  await fetch("/artifacts/local/three-telos/cir/cylinder.json")
).json()) as TelosShaderArtifact;
const field: TelosField = {
  artifact,
  identity: { occurrenceId: "curved-cylinder" },
  bounds: { minimum: [-0.8, -0.8, -1], maximum: [0.8, 0.8, 1] },
  material: materialFromAppearance({
    baseColor: [0.95, 0.95, 0.95],
    metallic: 0,
  }),
};
const scene: TelosScene = {
  fields: [field],
  meshes: [
    {
      definition: {
        id: "dark-foreground",
        positions: [
          -1.5, -1.3, 1.2, -0.1, -1.3, 1.2, -1.5, 1.3, 1.2, -0.1, 1.3, 1.2,
        ],
        normals: [0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1],
        indices: [0, 1, 2, 2, 1, 3],
      },
      identity: { occurrenceId: "foreground-mesh" },
      material: materialFromAppearance({ baseColor: [0.08, 0.18, 0.12] }),
    },
  ],
  lines: [
    {
      id: "topology",
      points: [-1.5, -1.3, 1.2, -0.1, -1.3, 1.2, -0.1, 1.3, 1.2],
      widthPixels: 1,
      identity: { occurrenceId: "foreground-mesh", edgeId: "sharp-boundary" },
      color: [1, 0.5, 0.05, 1],
    },
  ],
};
for (const viewport of [host, reference]) viewport.setScene(scene);
const results: Record<string, unknown>[] = [];
let previous: Uint8Array | undefined;
let mode: TelosAAMode = "None";
const api = {
  host,
  reference,
  errors,
  results,
  async start(next: TelosAAMode) {
    mode = next;
    host.setAA(next);
    host.temporal!.reset("controlled-witness");
    previous = undefined;
    host.camera.mode = "orthographic";
    host.camera.position.set(0, 0, 5);
    host.camera.target.set(0, 0, 0);
    host.camera.span = 4;
    host.camera.update();
    field.transform = new Matrix4().elements;
    host.setScene(scene);
    reference.setScene(scene);
  },
  async frame(scenario: string, index: number) {
    const angle =
      scenario === "orbit"
        ? index * 0.012
        : scenario === "fast"
          ? index * 0.13
          : 0;
    if (scenario === "disocclusion") {
      host.camera.position.set(index * 0.018, 0, 5);
      host.camera.target.set(index * 0.018, 0, 0);
    } else {
      host.camera.position.set(Math.sin(angle) * 5, 0, Math.cos(angle) * 5);
      host.camera.target.set(0, 0, 0);
    }
    if (scenario === "moving-occurrence") {
      field.transform = new Matrix4().makeTranslation(
        index * 0.035,
        0,
        0,
      ).elements;
      host.setScene(scene);
      reference.setScene(scene);
    }
    host.camera.update();
    reference.camera.position.copy(host.camera.position);
    reference.camera.target.copy(host.camera.target);
    reference.camera.update();
    const ray = host.camera.worldRay([256, 256]);
    const start = performance.now();
    host.render();
    const readback = host.readPixels();
    await host.owner.device.queue.onSubmittedWorkDone();
    const completedMs = performance.now() - start;
    const pixels = await readback;
    const resolveGpuMs =
      mode === "TAA" || mode === "TAAUtility"
        ? await host.temporal!.readGpuMilliseconds()
        : null;
    reference.render();
    const expected = await reference.readPixels();
    let ghost = 0;
    let backgroundPixels = 0;
    let difference = 0;
    let imageError = 0;
    // Only pixels outside geometry by >=2 physical pixels: avoid counting sample coverage as a trail.
    for (let y = 2; y < 510; y++)
      for (let x = 2; x < 510; x++) {
        const i = (y * 512 + x) * 4;
        let background = true;
        for (const [dx, dy] of [
          [0, 0],
          [-2, 0],
          [2, 0],
          [0, -2],
          [0, 2],
        ]) {
          const j = ((y + dy) * 512 + x + dx) * 4;
          if (expected[j] > 10 || expected[j + 1] > 10 || expected[j + 2] > 10)
            background = false;
        }
        if (background) {
          ghost +=
            Math.max(0, pixels[i] - expected[i]) +
            Math.max(0, pixels[i + 1] - expected[i + 1]) +
            Math.max(0, pixels[i + 2] - expected[i + 2]);
          backgroundPixels++;
        }
        imageError +=
          Math.abs(pixels[i] - expected[i]) +
          Math.abs(pixels[i + 1] - expected[i + 1]) +
          Math.abs(pixels[i + 2] - expected[i + 2]);
        if (previous)
          difference +=
            Math.abs(pixels[i] - previous[i]) +
            Math.abs(pixels[i + 1] - previous[i + 1]) +
            Math.abs(pixels[i + 2] - previous[i + 2]);
      }
    previous = pixels;
    if (!host.camera.worldRay([256, 256]).direction.equals(ray.direction))
      throw new Error("picking jittered");
    if (errors.length) throw new Error(errors.join("; "));
    const result = {
      mode,
      scenario,
      index,
      completedMs,
      resolveGpuMs,
      encodeMs: host.metrics.frameMs,
      resolveEncodeMs: host.temporal!.resolveEncodeMs,
      reprojectionCpuMs: host.temporal!.reprojectionCpuMs,
      ghostEnergy: ghost / Math.max(backgroundPixels * 3, 1),
      imageError: imageError / (512 * 512 * 3),
      temporalDifference: difference / (512 * 512 * 3),
      resetReason: host.temporal!.lastResetReason,
    };
    results.push(result);
    document.querySelector("#label")!.textContent =
      `${mode} / ${scenario} / frame ${index}`;
    return result;
  },
  async quality(next: TelosAAMode) {
    await api.start(next);
    reference.resize(512, 512, 4);
    reference.render();
    const highResolution = await reference.readPixels();
    let current!: Uint8Array;
    for (let frame = 0; frame < 16; frame++) {
      host.render();
      current = await host.readPixels();
    }
    let absoluteError = 0;
    for (let y = 0; y < 512; y++)
      for (let x = 0; x < 512; x++) {
        for (let channel = 0; channel < 3; channel++) {
          let sum = 0;
          for (let dy = 0; dy < 4; dy++)
            for (let dx = 0; dx < 4; dx++)
              sum +=
                highResolution[
                  ((y * 4 + dy) * 2048 + x * 4 + dx) * 4 + channel
                ];
          absoluteError += Math.abs(
            current[(y * 512 + x) * 4 + channel] - sum / 16,
          );
        }
      }
    reference.resize(512, 512, 1);
    if (errors.length) throw new Error(errors.join("; "));
    return {
      mode: next,
      samplesPerReferencePixel: 16,
      meanAbsoluteError: absoluteError / (512 * 512 * 3),
    };
  },
  async realCad(next: TelosAAMode) {
    const packet = await (
      await fetch("/artifacts/local/three-telos/x1/ctc03-display.json")
    ).json();
    const meshes = packet.data.faces
      .filter((face: any) => face.meshPatch)
      .map((face: any) => {
        const mesh = face.meshPatch;
        const flatten = (points: any[]) =>
          points.flatMap((point) => [point.x, point.y, point.z]);
        return {
          definition: {
            id: "ctc03:" + face.faceId,
            positions: flatten(mesh.positions),
            normals: flatten(mesh.normals),
            indices: mesh.triangleIndices,
          },
          identity: { occurrenceId: "ctc03", faceId: face.faceId },
          material: materialFromAppearance({ baseColor: [0.7, 0.74, 0.78] }),
        };
      });
    host.setScene({ meshes, fields: [], lines: [] });
    host.camera.mode = "perspective";
    host.fit();
    host.setAA(next);
    for (let frame = 0; frame < 16; frame++) {
      host.render();
      await host.readPixels();
    }
    if (errors.length) throw new Error(errors.join("; "));
    return {
      faces: meshes.length,
      mode: next,
      resetReason: host.temporal!.lastResetReason,
    };
  },
  async debug() {
    host.setAA(mode, "policy");
    host.render();
    const pixels = await host.readPixels();
    const counts = { reject: 0, stable: 0, clamp: 0, edge: 0 };
    for (let i = 0; i < pixels.length; i += 4) {
      // BGRA bytes on this qualified Edge adapter.
      if (pixels[i + 2] > 240 && pixels[i + 1] < 20) counts.reject++;
      if (pixels[i + 1] > 240 && pixels[i + 2] < 20) counts.stable++;
      if (pixels[i + 1] > 240 && pixels[i + 2] > 240) counts.clamp++;
      if (pixels[i] > 240 && pixels[i + 2] < 20) counts.edge++;
    }
    if (errors.length) throw new Error(errors.join("; "));
    return counts;
  },
  dispose() {
    host.dispose();
    reference.dispose();
  },
};
(window as unknown as { taa: typeof api }).taa = api;
document.querySelector("pre")!.textContent =
  "Ready. Explicit frames, shared CIR/mesh depth, stable picking.";
