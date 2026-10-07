import assert from "node:assert/strict";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { resolve, join } from "node:path";
import { pathToFileURL } from "node:url";

// Matched-camera diagnostic captures through the actual production upload/viewer.
// Start a foreground Cadmata host separately; this script installs no service.
const [playwrightPath, corpus, output, address = "http://127.0.0.1:5087", ids = "mcm-04", faceSteps = "", probePoints = "", productWitness = ""] = process.argv.slice(2);
assert(playwrightPath && corpus && output, "Usage: node inspect-step-legacy-cadmata.mts <playwright-index.mjs> <corpus> <output> [host] [ids] [isolated-STEP-ids]");
const { chromium } = await import(pathToFileURL(resolve(playwrightPath)).href);
const manifest = JSON.parse(await readFile("testdata/step242/manifests/mcmaster-legacy-x0.json", "utf8"));
await mkdir(output, { recursive: true });
for (const id of ids.split(",")) {
  const browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true, args: ["--js-flags=--max-old-space-size=8192"] });
  try {
    const specimen = manifest.specimens.find((s: { id: string }) => s.id === id);
    assert(specimen, id);
    const page = await browser.newPage({ viewport: { width: 1440, height: 1050 } });
    const errors: string[] = []; page.on("pageerror", (error: Error) => errors.push(String(error)));
    await page.goto(address);
    await page.locator('input[type="file"]').setInputFiles(join(corpus, specimen.filename));
    const response = page.waitForResponse((r: any) => r.url().endsWith("/import/step"), { timeout: 120000 });
    await page.getByRole("button", { name: "Import STEP 242", exact: true }).click();
    assert((await response).ok(), "Real STEP upload must succeed");
    await page.waitForFunction(() => {
      const state = JSON.parse(document.querySelector("canvas")?.getAttribute("data-telos-display") ?? "{}");
      return state.meshSurfaces > 0 || state.fields > 0;
    }, undefined, { timeout: 120000 });
    await page.getByText("Surface / trim inspection", { exact: true }).click();
    const canvas = page.getByLabel("Engineering WebGPU viewport");
    const faces = await page.getByLabel("Isolated display face").locator("option").evaluateAll((options: any[]) =>
      options.slice(1).map(o => ({ key: o.value, label: o.textContent })));
    const captures: any[] = [];
    async function capture(mode: string, suffix = mode, matched = true) {
      await page.getByLabel("Surface inspection view").selectOption(mode);
      await page.waitForFunction((mode: string) => JSON.parse(document.querySelector("canvas")?.getAttribute("data-surface-inspection") ?? "{}").mode === mode, mode);
      await page.evaluate(async () => { await new Promise(requestAnimationFrame); await new Promise(requestAnimationFrame); });
      const state = JSON.parse(await canvas.getAttribute("data-surface-inspection") ?? "{}");
      if (matched && captures.length) assert.deepEqual(state.camera, captures[0].camera, "Inspection must preserve the camera");
      assert(mode !== "wire" || state.visibleMeshes === 0, "Wire-only must hide filled patches");
      assert(mode !== "surfaces" || state.visibleEdges === 0, "Surfaces-only must hide BRep curves");
      await canvas.screenshot({ path: join(output, `${id}-${suffix}.png`) });
      captures.push({ ...state, image: `${id}-${suffix}.png` });
    }
    for (const mode of ["normal", "surfaces", "wire", "overlay", "patches"]) await capture(mode);
    for (const point of probePoints.split(",").filter(Boolean)) {
      await page.getByLabel("Isolated display face").selectOption("");
      await page.getByLabel("Surface inspection view").selectOption("surfaces");
      const [x, y] = point.split(":").map(Number);
      const box = await canvas.boundingBox(); assert(box);
      await canvas.click({ position: { x: x * box.width, y: y * box.height } });
      await page.evaluate(async () => { await new Promise(requestAnimationFrame); await new Promise(requestAnimationFrame); });
      await capture("overlay", `probe-${point.replace(":", "-")}`);
    }
    for (const step of faceSteps.split(",").filter(Boolean)) {
      const face = faces.find((f: any) => f.label.includes(`STEP #${step} /`));
      if (!face) continue;
      await page.getByLabel("Isolated display face").selectOption(face.key);
      await capture("overlay", `face-${step}`);
      assert.equal(captures.at(-1).visibleMeshes, 1, "Isolation must retain exactly one occurrence/face");
      assert(captures.at(-1).visibleEdges > 0, "Isolated source face must expose its boundary curves");
    }
    const interactions: any[] = [];
    if (productWitness === "product") {
      await page.getByLabel("Isolated display face").selectOption("");
      const box = await canvas.boundingBox(); assert(box);
      const before = await canvas.screenshot();
      await page.mouse.move(box.x + box.width * .65, box.y + box.height * .4);
      await page.mouse.down();
      await page.mouse.move(box.x + box.width * .65 + 90, box.y + box.height * .4 + 35, { steps: 12 });
      await page.mouse.up();
      await capture("surfaces", "orbit", false);
      assert(!before.equals(await canvas.screenshot()), "Orbit must change real pixels");
      await page.mouse.wheel(0, -600);
      await capture("overlay", "close-overlay", false);
      assert(captures.at(-1).camera.span < captures[0].camera.span, "Close zoom must reduce fitted span");
      for (const step of faceSteps.split(",").filter(Boolean)) {
        const face = faces.find((f: any) => f.label.includes(`STEP #${step} /`));
        if (!face) continue;
        await page.getByLabel("Isolated display face").selectOption(face.key);
        await capture("surfaces", `close-face-${step}`, false);
        const expected = JSON.parse(face.key);
        let picked: any = null;
        // A bounded scan through the actual canvas picker. A preselected dropdown
        // is not evidence of a click; inspect the hit emitted by the click handler.
        scan: for (let y = .25; y <= .75; y += .05) for (let x = .2; x <= .8; x += .05) {
          await canvas.click({ position: { x: box.width * x, y: box.height * y } });
          picked = JSON.parse(await canvas.getAttribute("data-surface-inspection-pick") ?? "null");
          if (picked?.faceId === expected[1] && picked?.occurrenceId === expected[0]) break scan;
        }
        assert(picked?.faceId === expected[1] && picked?.occurrenceId === expected[0], `Click must retain ${face.label}`);
        interactions.push({ step, expectedFace: face, picked });
        await capture("overlay", `selected-face-${step}`, false);
      }
    }
    assert.deepEqual(errors, [], "Browser must have no runtime errors");
    await writeFile(join(output, `${id}-inspection.json`), JSON.stringify({ id, filename: specimen.filename,
      path: "production Cadmata / Edge WebGPU / real upload", faces, captures, interactions, errors }, null, 2));
    console.log(id, "matched camera", captures.length, "views", faces.length, "source faces");
  } finally { await browser.close(); }
}
