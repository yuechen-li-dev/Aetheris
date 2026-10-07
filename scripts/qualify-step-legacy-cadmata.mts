import assert from "node:assert/strict";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { resolve, join } from "node:path";
import { pathToFileURL } from "node:url";

// Invoke against a foreground, current-source Cadmata host. No host/service is
// installed or started by this script. CAD files enter through the real upload UI.
const [playwrightPath, corpus, output, address = "http://127.0.0.1:5087", witnessIds = "mcm-13,mcm-17,mcm-21,mcm-15,mcm-24"] = process.argv.slice(2);
assert(playwrightPath && corpus && output, "Usage: node scripts/qualify-step-legacy-cadmata.mts <playwright-index.mjs> <corpus-directory> <output-directory> [host-url]");
const { chromium } = await import(pathToFileURL(resolve(playwrightPath)).href);
const manifest = JSON.parse(await readFile("testdata/step242/manifests/mcmaster-legacy-x0.json", "utf8"));
await mkdir(output, { recursive: true });
const reports: unknown[] = [];
  for (const id of witnessIds.split(",")) {
    const browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true,
      args: ["--js-flags=--max-old-space-size=8192", ...(process.env.CADMATA_WITNESS_SOFTWARE_GPU === "1"
        ? ["--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--enable-unsafe-webgpu"] : [])] });
    try {
    const specimen = manifest.specimens.find((s: { id: string }) => s.id === id);
    const page = await browser.newPage({ viewport: { width: 1440, height: 1050 } });
    page.setDefaultTimeout(45000);
    const errors: string[] = [];
    // Let the browser receive large STEP replies directly over HTTP. Relaying them
    // through Fetch.fulfillRequest base64 can exceed the DevTools transport limit.
    // Record HTTP success and the status/display evidence consumed by the real UI.
    page.on("pageerror", (e: Error) => errors.push(String(e)));
    const report: Record<string, unknown> = { id, filename: specimen.filename, path: "current-source production Cadmata frontend / Edge WebGPU", humanReview: "not-performed" };
    try {
      await page.goto(address);
      await page.locator('input[type="file"]').setInputFiles(join(corpus, specimen.filename));
      const responsePromise = page.waitForResponse((response: { url(): string }) => response.url().endsWith("/import/step"), { timeout: 120000 });
      await page.getByRole("button", { name: "Import STEP 242", exact: true }).click();
      const response = await responsePromise;
      report.uploadHttpStatus = response.status();
      assert(response.ok(), "Actual browser upload must succeed over HTTP");
      await page.waitForFunction(() => document.querySelector(".import-status-box")?.textContent?.includes(":"), undefined, { timeout: 120000 });
      await page.waitForFunction(() => {
        const canvas = document.querySelector("canvas");
        const display = JSON.parse(canvas?.getAttribute("data-telos-display") ?? "{}");
        return display.meshSurfaces > 0 || display.fields > 0;
      }, undefined, { timeout: 120000 });
      const canvas = page.getByLabel("Engineering WebGPU viewport");
      const box = await canvas.boundingBox();
      assert(box, "Viewport must be framed");
      report.visibleStatus = await page.locator(".import-status-box").innerText();
      report.importStatus = String(report.visibleStatus).match(/\b(Qualified|Inspectable|Degraded|Failed):/)?.[1];
      report.display = JSON.parse(await canvas.getAttribute("data-telos-display") ?? "{}");
      await writeFile(join(output, id + "-import.json"), JSON.stringify({
        uploadHttpStatus: report.uploadHttpStatus, visibleStatus: report.visibleStatus, display: report.display,
        responsePath: "direct browser HTTP; UI consumed actual server reply; full BRep evidence is in corpus diagnostics",
      }, null, 2));
      await page.screenshot({ path: join(output, id + "-product.png") });
      const before = await canvas.screenshot();
      await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
      await page.mouse.down();
      await page.mouse.move(box.x + box.width / 2 + 80, box.y + box.height / 2 + 25, { steps: 12 });
      await page.mouse.up();
      await page.waitForTimeout(200);
      report.orbitChangedPixels = !before.equals(await canvas.screenshot());
      assert(report.orbitChangedPixels, "Orbit must redraw imported geometry");
      report.frameCadence = await page.evaluate(async () => {
        const intervals: number[] = [];
        let previous = await new Promise<number>(resolve => requestAnimationFrame(resolve));
        for (let i = 0; i < 30; i++) {
          const next = await new Promise<number>(resolve => requestAnimationFrame(resolve));
          intervals.push(next - previous); previous = next;
        }
        intervals.sort((a, b) => a - b);
        return { samples: intervals.length, medianMilliseconds: intervals[15], p95Milliseconds: intervals[28],
          basis: "browser requestAnimationFrame cadence after orbit; not a GPU benchmark" };
      });
      const pickResponses: unknown[] = [];
      page.on("response", async (response: { url(): string; json(): Promise<unknown> }) => {
        if (response.url().endsWith("/pick")) pickResponses.push(await response.json());
      });
      for (const [x, y] of [[.5, .5], [.4, .5], [.6, .5], [.5, .4], [.5, .6]]) {
        await canvas.click({ position: { x: box.width * x, y: box.height * y } });
        await page.waitForTimeout(300);
        if (await page.locator('[aria-label="Assembly product tree"] .active-row').count() > 0
          || pickResponses.some((r: any) => r.data?.hits?.length > 0)) break;
      }
      await page.screenshot({ path: join(output, id + "-orbit-selection.png") });
      report.selectionHitVerified = await page.locator('[aria-label="Assembly product tree"] .active-row').count() > 0
        || pickResponses.some((r: any) => r.data?.hits?.length > 0);
      assert(report.selectionHitVerified, "Selection must hit imported geometry");
      report.pickResponses = pickResponses;
      report.topologyOverlay = Number((report.display as { topologyLines?: number }).topologyLines) > 0
        ? "kernel-edge-lines-submitted; visual human review pending" : "missing";
      assert.notEqual(report.topologyOverlay, "missing", "Imported topology edges must reach the actual Telos display host");
      if (id === "mcm-15") assert.equal(report.importStatus, "Degraded", "Incomplete U-joint must be visibly degraded");
      report.success = true;
    } catch (e) {
      report.success = false;
      report.failure = String(e);
      report.visibleText = await page.locator("body").innerText().then((text: string) => text.slice(-4000)).catch(() => "page unavailable");
      await page.screenshot({ path: join(output, id + "-failure.png") }).catch(() => undefined);
    }
    report.pageErrors = errors;
    reports.push(report);
    await writeFile(join(output, "product-summary.json"), JSON.stringify(reports, null, 2));
    console.log(id, report.success, report.importStatus, report.failure ?? "");
    await page.close().catch(() => undefined);
    } finally { await browser.close().catch(() => undefined); }
  }
