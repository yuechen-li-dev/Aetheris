import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { readFile, writeFile } from "node:fs/promises";
import type {
  TelosHost,
  TelosShaderArtifact,
} from "../Aetheris.Web.Runtime/telos/dist/index.js";
import type { CadmataVisualizationArtifact } from "../aetheris.client/src/viewer/conceptVisualization.ts";
declare global {
  interface Window {
    cadmataX1: {
      host: TelosHost;
      artifact: CadmataVisualizationArtifact;
      selected: string | null;
      revision: number;
    };
    x1Buffers: Set<GPUBuffer>;
    x1Geometry: unknown[];
  }
}
const { chromium } = await import(pathToFileURL(resolve(process.argv[2])).href);
const output = "artifacts/local/three-telos/x1",
  dpr = Number(process.argv[3] ?? 1);
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const check = (value: unknown, message: string) => {
  if (!value) throw new Error(message);
};
try {
  const page = await browser.newPage({
    viewport: { width: 1450, height: 1000 },
    deviceScaleFactor: dpr,
  });
  const errors: string[] = [];
  page.on("pageerror", (error: Error) => errors.push(String(error)));
  await page.goto(
    "http://127.0.0.1:5173/@fs/" +
      resolve("fixtures/three-telos/cadmata-x1.html").replaceAll("\\", "/"),
  );
  await page.waitForFunction(
    () => window.cadmataX1?.host && window.cadmataX1.revision > 0,
    null,
    { timeout: 120000 },
  );
  await page
    .getByRole("button", { name: "Inspect Datum A", exact: true })
    .waitFor();
  await page.getByRole("button", { name: "All PMI", exact: true }).click();
  await page.waitForTimeout(150);
  check(
    (await page.locator("[data-pmi-id]").count()) === 29,
    "canonical PMI inventory",
  );
  const steady = await page.evaluate(async () => {
    const host = window.cadmataX1.host;
    host.grid = false;
    host.render();
    await host.owner.device.queue.onSubmittedWorkDone();
    const times: number[] = [],
      resources = host.owner.resources;
    window.x1Buffers = new Set(resources.buffers);
    window.x1Geometry = [...host.geometry.values()];
    for (let i = 0; i < 20; i++) {
      const started = performance.now();
      host.render();
      await host.owner.device.queue.onSubmittedWorkDone();
      times.push(performance.now() - started);
    }
    return {
      times,
      buffers: resources.buffers.size,
      pipelines: resources.pipelines.size,
      modules: resources.modules.size,
      geometry: host.geometry.size,
      metrics: { ...host.metrics },
      adapter: {
        vendor: host.owner.adapter.info.vendor,
        architecture: host.owner.adapter.info.architecture,
        description: host.owner.adapter.info.description,
      },
      visibleLabels: [
        ...document.querySelectorAll<HTMLButtonElement>("[data-pmi-id]"),
      ].filter((element) => element.style.display !== "none").length,
      retained: [...window.x1Buffers].every((buffer) =>
        resources.buffers.has(buffer),
      ),
    };
  });
  check(steady.retained, "steady resources changed");
  const moving = await page.evaluate(async () => {
    const host = window.cadmataX1.host;
    const samples: { frame: number; projection: number; upload: number }[] = [];
    for (let i = 0; i < 20; i++) {
      host.camera.position.x += 0.2;
      const started = performance.now();
      host.render();
      await host.owner.device.queue.onSubmittedWorkDone();
      samples.push({
        frame: performance.now() - started,
        projection: host.metrics.overlayUpdateMs,
        upload: host.metrics.dynamicLineUpdateMs,
      });
    }
    return {
      samples,
      retained:
        window.x1Buffers.size === host.owner.resources.buffers.size &&
        [...window.x1Buffers].every((buffer) =>
          host.owner.resources.buffers.has(buffer),
        ),
    };
  });
  check(moving.retained, "camera-facing updates allocated resources");

  await page.screenshot({ path: output + "/all-pmi-dpr" + dpr + ".png" });
  await page.getByRole("button", { name: "Default PMI", exact: true }).click();
  const datum = page.getByRole("button", {
    name: "Inspect Datum A",
    exact: true,
  });
  await datum.click();
  await page.waitForTimeout(100);
  const selection = await page.evaluate(() => {
    const { host, artifact, selected } = window.cadmataX1;
    const entity = artifact.entities.find(
      (entity) => entity.stableId === selected,
    )!;
    return {
      selected,
      faces: entity.topology?.faceIds,
      highlighted: host.scene.meshes
        .filter((mesh) => mesh.selected)
        .map((mesh) => mesh.identity.faceId),
    };
  });
  check(
    selection.faces?.every((face: number) =>
      selection.highlighted.includes(face),
    ),
    "PMI to geometry highlight lost",
  );
  await page.evaluate(() => {
    const host = window.cadmataX1.host;
    host.render();
    window.x1Buffers = new Set(host.owner.resources.buffers);
    window.x1Geometry = [...host.geometry.values()];
  });
  const box = await datum.boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(
    box.x + box.width / 2 + 60,
    box.y + box.height / 2 - 30,
    { steps: 20 },
  );
  await page.mouse.up();
  await page.waitForTimeout(100);
  const drag = await page.evaluate(() => {
    const host = window.cadmataX1.host;
    return {
      retained:
        window.x1Buffers.size === host.owner.resources.buffers.size &&
        [...window.x1Buffers].every((buffer) =>
          host.owner.resources.buffers.has(buffer),
        ),
      geometryRetained: window.x1Geometry.every((geometry) =>
        [...host.geometry.values()].includes(geometry as never),
      ),
      frames: host.metrics.frames,
    };
  });
  check(drag.retained && drag.geometryRetained, "drag rebuilt GPU resources");
  await page.screenshot({
    path: output + "/drag-resources-dpr" + dpr + ".png",
  });
  const point = await page.evaluate(() => {
    const host = window.cadmataX1.host;
    const labels = [...document.querySelectorAll("[data-pmi-id]")].map(
      (element) => element.getBoundingClientRect(),
    );
    const bounds = host.owner.canvas.getBoundingClientRect();
    for (let y = 80; y < host.camera.height - 20; y += 12)
      for (let x = 20; x < host.camera.width - 20; x += 12) {
        if (
          labels.some(
            (rect) =>
              x + bounds.x >= rect.left &&
              x + bounds.x <= rect.right &&
              y + bounds.y >= rect.top &&
              y + bounds.y <= rect.bottom,
          )
        )
          continue;
        const hit = host.picker.pick([x, y]);
        if (!hit?.overlayId && hit?.faceId === 1) return { x, y };
      }
    return null;
  });
  check(point, "no visible Face 1 pick");
  await page.locator("canvas").click({ position: point });
  await page.waitForFunction(
    () =>
      window.cadmataX1.artifact.entities.find(
        (entity) => entity.stableId === window.cadmataX1.selected,
      )?.kind === "BRepFace",
  );
  check(
    await datum.evaluate((element: HTMLElement) =>
      element.classList.contains("is-selected"),
    ),
    "geometry to related PMI highlight lost",
  );
  await page.screenshot({ path: output + "/geometry-pmi-dpr" + dpr + ".png" });
  const cameras = await page.evaluate(async () => {
    const host = window.cadmataX1.host,
      results: unknown[] = [];
    for (const mode of ["perspective", "orthographic"] as const) {
      host.camera.mode = mode;
      host.render();
      await host.owner.device.queue.onSubmittedWorkDone();
      results.push({
        mode,
        frames: host.metrics.frames,
        labels: [
          ...document.querySelectorAll<HTMLButtonElement>("[data-pmi-id]"),
        ].filter((element) => element.style.display !== "none").length,
      });
    }
    const generation = host.frame.generation;
    host.resize(900, 650, 2);
    host.render();
    results.push({
      resize: host.frame.generation > generation,
      width: host.frame.width,
      height: host.frame.height,
    });
    host.camera.near = host.camera.position.distanceTo(host.camera.target);
    host.render();
    await host.owner.device.queue.onSubmittedWorkDone();
    results.push({ closeClip: true, stopped: host.owner.stopped });
    host.camera.near = 0.01;
    host.camera.fit([-150, 0, 0], [0, 400, 160]);
    host.render();
    return results;
  });
  check(
    !JSON.stringify(cameras).includes('"stopped":true'),
    "close clipping validation failed",
  );
  await page.screenshot({ path: output + "/camera-dpr" + dpr + ".png" });
  await page.getByRole("button", { name: "Switch model", exact: true }).click();
  await page.waitForFunction(
    () =>
      window.cadmataX1.artifact.fixtureId === "construction-plane-positive-x",
  );
  await page.waitForTimeout(200);
  const construction = await page.evaluate(() => ({
    meshes: window.cadmataX1.host.scene.meshes
      .filter((mesh) => mesh.overlay)
      .map((mesh) => ({ id: mesh.identity.overlayId, depth: mesh.depthMode })),
    lines: window.cadmataX1.host.scene.lines.filter(
      (line) => line.identity.overlayId,
    ).length,
  }));
  check(construction.lines > 0, "construction preview not submitted");
  await page.screenshot({ path: output + "/construction-dpr" + dpr + ".png" });
  await page.getByRole("button", { name: "Authored PMI", exact: true }).click();
  await page.waitForFunction(
    () => window.cadmataX1.artifact.fixtureId === "pmi-projected-hole-diameter",
  );
  const authoredDatum = page.getByRole("button", {
    name: "Inspect A",
    exact: true,
  });
  await authoredDatum.click();
  await page.waitForTimeout(150);
  const before = await page.evaluate(() => ({
    selected: window.cadmataX1.selected,
    entity: window.cadmataX1.artifact.entities.find(
      (entity) => entity.stableId === window.cadmataX1.selected,
    ),
    revision: window.cadmataX1.revision,
  }));
  await page.getByRole("button", { name: "Rebuild", exact: true }).click();
  await page.waitForFunction(
    (revision: number) => window.cadmataX1.revision > revision,
    before.revision,
  );
  const after = await page.evaluate(() => ({
    selected: window.cadmataX1.selected,
    entity: window.cadmataX1.artifact.entities.find(
      (entity) => entity.stableId === window.cadmataX1.selected,
    ),
    selectedFaces: window.cadmataX1.host.scene.meshes
      .filter((mesh) => mesh.selected)
      .map((mesh) => mesh.identity.faceId),
  }));
  check(
    before.selected === after.selected &&
      JSON.stringify(before.entity?.geometry) ===
        JSON.stringify(after.entity?.geometry) &&
      after.selectedFaces.length,
    "authored face(+Z) datum rebuild anchoring failed",
  );
  await page.screenshot({
    path: output + "/authored-rebuild-dpr" + dpr + ".png",
  });
  const artifact = JSON.parse(
    await readFile("artifacts/local/three-telos/cir/cylinder.json", "utf8"),
  ) as TelosShaderArtifact;
  await page.evaluate((artifact: TelosShaderArtifact) => {
    const { host, artifact: semantic, selected } = window.cadmataX1;
    const entity = semantic.entities.find(
      (entity) => entity.stableId === selected,
    )!;
    const point =
      entity.geometry!.type === "polyline"
        ? entity.geometry!.points.at(-1)!
        : entity.geometry!.type === "point"
          ? entity.geometry!.point
          : entity.geometry!.type === "circle"
            ? entity.geometry!.center
            : { x: 0, y: 0, z: 0 };
    const transform = [
      1,
      0,
      0,
      0,
      0,
      1,
      0,
      0,
      0,
      0,
      1,
      0,
      point.x,
      point.y,
      point.z,
      1,
    ];
    host.setScene({
      meshes: [],
      lines: [],
      fields: [
        {
          artifact,
          transform,
          identity: { occurrenceId: "qualified-CIR" },
          bounds: { minimum: [-0.8, -0.8, -1], maximum: [0.8, 0.8, 1] },
          material: {
            baseColor: [0.7, 0.15, 0.06],
            roughness: 0.6,
            metallic: 0,
            opacity: 1,
          },
        },
      ],
    });
    host.camera.mode = "orthographic";
    host.camera.position.set(point.x + 3, point.y + 2, point.z + 4);
    host.camera.target.set(point.x, point.y, point.z);
    host.camera.span = 6;
    host.render();
  }, artifact);
  await page.waitForTimeout(150);
  check(await authoredDatum.isVisible(), "semantic PMI over CIR invisible");
  await page.screenshot({ path: output + "/cir-pmi-dpr" + dpr + ".png" });
  const alerts = await page.getByRole("alert").allTextContents();
  check(!alerts.length && !errors.length, JSON.stringify({ alerts, errors }));
  const report = {
    status: "PASS",
    dpr,
    steady,
    moving,
    selection,
    drag,
    geometryToPmi: true,
    cameras,
    construction,
    rebuild: { before, after },
    cirOverlay: true,
    errors,
    alerts,
  };
  await writeFile(
    output + "/overlays-dpr" + dpr + ".json",
    JSON.stringify(report, null, 2),
  );
  console.log(JSON.stringify(report, null, 2));
} finally {
  await browser.close();
}
