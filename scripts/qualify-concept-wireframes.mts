import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { mkdir, readFile, writeFile } from "node:fs/promises";

// Run against the real Cadmata/Helios applications, using the installed browser-test dependency.
const { chromium } = await import(pathToFileURL(resolve(process.argv[2])).href);
const product = process.argv[3] ?? "cadmata";
if (!["cadmata", "helios"].includes(product)) throw new Error("Expected cadmata or helios");
const output = resolve("artifacts/local/concept-wireframes");
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 960 } });
  page.setDefaultTimeout(180000);
  const errors: string[] = [];
  page.on("pageerror", (error: Error) => errors.push(error.message));
  await page.goto(product === "cadmata" ? "http://127.0.0.1:5173" : "http://127.0.0.1:4173/local");
  console.log(product + ": application opened");
  if (product === "helios") {
    await page.locator(".status-ready").waitFor();
    await page.locator('input[type="file"]').setInputFiles(resolve("fixtures/Regression/Visualization/concept-structs.firmament"));
  }
  const toggle = page.getByRole("button", { name: "Concepts", exact: true });
  await toggle.waitFor();
  await page.waitForFunction(() => !document.querySelector<HTMLButtonElement>('button[title*="Concept Struct"]')?.disabled);
  const canvas = page.locator('[data-display-host="three-telos"] canvas[data-concept-wires]');
  await canvas.waitFor();
  if (await canvas.getAttribute("data-concept-wires") !== "0") throw new Error("Concepts must start hidden");
  await canvas.screenshot({ path: resolve(output, product + "-off.png") });
  await toggle.click();
  await page.waitForFunction(() => Number(document.querySelector('canvas[data-concept-wires]')?.getAttribute("data-concept-wires")) > 0);
  await page.waitForTimeout(300);
  const count = Number(await canvas.getAttribute("data-concept-wires"));
  if (count !== 20) throw new Error("Expected exact Box3/Plane/Axis/two-point guides, got " + count);
  await canvas.screenshot({ path: resolve(output, product + "-gold.png") });
  console.log(product + ": gold wires rendered");
  await page.screenshot({ path: resolve(output, product + "-application.png") });
  await toggle.click();
  await page.waitForFunction(() => document.querySelector('canvas[data-concept-wires]')?.getAttribute("data-concept-wires") === "0");
  await page.waitForTimeout(200);
  await canvas.screenshot({ path: resolve(output, product + "-off-again.png") });
  await toggle.click();
  let retainedAfterFailure = false;
  if (product === "helios") {
    // Reopen a bad revision of this same part through the normal project build path.
    // This avoids making the test depend on Monaco keyboard/event timing.
    const source = await readFile(resolve("fixtures/Regression/Visualization/concept-structs.firmament"), "utf8");
    await page.locator('input[type="file"]').setInputFiles({ name: "concept-structs.firmament", mimeType: "text/plain",
      buffer: Buffer.from(source.replace("distance: 1.5mm", "distnace: 1.5mm")) });
    console.log(product + ": invalid same-part revision submitted");
    await page.waitForFunction(() => document.querySelector(".viewport-caption")?.textContent?.includes("Last good model"), null, { timeout: 30000 });
    await page.screenshot({ path: resolve(output, product + "-failed-draft.png") });
    retainedAfterFailure = Number(await canvas.getAttribute("data-concept-wires")) === count;
    if (!retainedAfterFailure) throw new Error("Failed draft lost the last-valid Concept guides");
    console.log(product + ": failed revision retained its guides");
  }
  if (product === "helios") {
    // Helios probes the adapter once per application startup, including adapter-unavailable browsers.
    await page.addInitScript(() => Object.defineProperty(navigator, "gpu", { value: undefined, configurable: true }));
    await page.reload();
    await page.locator(".status-ready").waitFor();
    await page.locator('input[type="file"]').setInputFiles(resolve("fixtures/Regression/Visualization/concept-structs.firmament"));
    await page.waitForFunction(() => !document.querySelector<HTMLButtonElement>('button[title*="Concept Struct"]')?.disabled);
    await toggle.click();
  } else {
    await page.evaluate(() => Object.defineProperty(navigator, "gpu", { value: undefined, configurable: true }));
    await toggle.click();
    await toggle.click();
  }
  const fallback = page.locator('[data-display-host="transitional-webgl"] canvas').first();
  await fallback.waitFor();
  await page.waitForTimeout(1000);
  await fallback.screenshot({ path: resolve(output, product + "-webgl-gold.png") });
  await toggle.click();
  await page.waitForTimeout(200);
  await fallback.screenshot({ path: resolve(output, product + "-webgl-off.png") });
  if (errors.length) throw new Error(JSON.stringify(errors));
  await writeFile(resolve(output, product + "-browser.json"), JSON.stringify({ success: true, product,
    conceptWires: count, webgpuAndWebgl: true, retainedAfterFailure: product === "helios" ? retainedAfterFailure : null, errors }, null, 2));
  console.log(product + " real application: Concept toggle, GPU/fallback and retention checks passed");
} catch (error) {
  console.error(error);
  throw error;
} finally { await browser.close(); }
