import { spawn } from "node:child_process";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import assert from "node:assert/strict";

const [bundle, output, playwrightModule, step] = process.argv.slice(2).map(path => resolve(path));
const { chromium } = await import(pathToFileURL(playwrightModule).href);
await mkdir(output, { recursive: true });
const metadata = JSON.parse(await readFile(join(bundle, "BUILD-METADATA.json"), "utf8"));
const app = spawn(join(bundle, "Cadmata"), ["--no-browser", "--urls", "http://127.0.0.1:0"], {
  cwd: output, env: { ...process.env, DOTNET_ROOT: join(output, "no-installed-dotnet") },
});
let stdout = "", stderr = "";
app.stdout.on("data", chunk => stdout += chunk);
app.stderr.on("data", chunk => stderr += chunk);
const exited = new Promise<void>(resolve => app.once("exit", () => resolve()));
let browser;
const errors: string[] = [];
try {
  const deadline = Date.now() + 45000;
  let address = "";
  while (!address) {
    assert(app.exitCode === null && Date.now() < deadline, `Packaged server failed to start: ${stderr}\n${stdout}`);
    address = stdout.match(/Cadmata ready: (http:\/\/127\.0\.0\.1:\d+)/)?.[1] ?? "";
    if (!address) await new Promise(resolve => setTimeout(resolve, 200));
  }
  browser = await chromium.launch({ headless: true, args: ["--enable-unsafe-webgpu", "--use-angle=swiftshader"] });
  const context = await browser.newContext({ acceptDownloads: true });
  await context.route("**/*", route => {
    const url = route.request().url();
    return /^https?:/.test(url) && new URL(url).origin !== address ? route.abort() : route.continue();
  });
  const page = await context.newPage();
  page.setDefaultTimeout(120000);
  page.on("pageerror", error => errors.push(String(error)));
  await page.goto(address);
  await page.getByText("Ready. Select a file to import.", { exact: true }).waitFor();
  const before = await page.locator("canvas").screenshot();
  await page.screenshot({ path: join(output, "blank.png") });
  await page.locator('input[type="file"]').setInputFiles(step);
  await page.getByRole("button", { name: "Import STEP 242", exact: true }).click();
  await page.getByText("Qualified: Import complete.", { exact: true }).waitFor();
  await page.waitForFunction(() => {
    const canvas = document.querySelector<HTMLCanvasElement>("canvas");
    return JSON.parse(canvas?.dataset.telosDisplay ?? "{}").meshSurfaces > 0 &&
      document.querySelector('[data-display-host="three-telos"]')?.getAttribute("aria-busy") === "false";
  });
  const display = JSON.parse(await page.locator("canvas").getAttribute("data-telos-display") ?? "{}");
  assert(!before.equals(await page.locator("canvas").screenshot()), "STEP import must visibly change the rendered canvas");
  await page.screenshot({ path: join(output, "imported.png") });
  const downloadEvent = page.waitForEvent("download");
  await page.getByRole("button", { name: "Download Canonical 242", exact: true }).click();
  const download = await downloadEvent;
  const exported = join(output, "canonical.step");
  await download.saveAs(exported);
  assert((await readFile(exported, "utf8")).startsWith("ISO-10303-21;"));
  await page.locator('input[type="file"]').setInputFiles(exported);
  const reimport = page.waitForResponse(response => response.request().method() === "POST" && response.url().endsWith("/import/step"));
  await page.getByRole("button", { name: "Import STEP 242", exact: true }).click();
  assert((await (await reimport).json()).success, "Canonical exported STEP must reimport successfully");
  await page.getByText(/^(Qualified|Inspectable|Degraded): Import complete\./).waitFor();
  assert.deepEqual(errors, []);
  await writeFile(join(output, "qualification.json"), JSON.stringify({ runtime: metadata.runtime,
    sourceRevision: metadata.sourceRevision, mode: "extracted self-contained server and headless Chromium",
    blankStartup: true, stepImport: true, display, canonicalReimport: true, errors }, null, 2));
} finally {
  await browser?.close();
  app.kill("SIGTERM");
  await Promise.race([exited, new Promise<void>(resolve => setTimeout(() => { app.kill("SIGKILL"); resolve(); }, 5000))]);
  await writeFile(join(output, "server.stdout.log"), stdout);
  await writeFile(join(output, "server.stderr.log"), stderr);
}
