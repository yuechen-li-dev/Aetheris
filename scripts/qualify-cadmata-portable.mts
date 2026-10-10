import { spawn, execFile } from "node:child_process";
import { promisify } from "node:util";
import { mkdir, writeFile, readFile, readdir } from "node:fs/promises";
import { createServer } from "node:net";
import { resolve, join } from "node:path";
import { pathToFileURL } from "node:url";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";

// Connect to the actual extracted Windows application's WebView2. Never launch
// a substitute Edge browser, Vite server or source-tree backend.
const [packageDirectory, outputDirectory, playwrightModule, stepFile, startupFile] = process.argv.slice(2).map(path => resolve(path));
const { chromium } = await import(pathToFileURL(playwrightModule).href);
await mkdir(outputDirectory, { recursive: true });
const reservation = createServer();
await new Promise<void>((resolve, reject) => {
  reservation.once("error", reject);
  reservation.listen(0, "127.0.0.1", resolve);
});
const debugPort = Number(process.env.CADMATA_QUALIFICATION_DEBUG_PORT ?? (reservation.address() as { port: number }).port);
await new Promise<void>(resolve => reservation.close(() => resolve()));
const metadata = JSON.parse(await readFile(join(packageDirectory, "BUILD-METADATA.json"), "utf8"));
const executableSha256 = createHash("sha256").update(await readFile(join(packageDirectory, "Cadmata.exe"))).digest("hex");
const started = Date.now();
const app = spawn(join(packageDirectory, "Cadmata.exe"), startupFile ? [startupFile] : [], {
  cwd: process.env.CADMATA_QUALIFICATION_CWD ?? process.env.SystemRoot,
  windowsHide: false, // WinExe has no console; qualify the visible product window.
  env: {
    ...process.env, PATH: join(process.env.SystemRoot!, "System32"),
    DOTNET_ROOT: join(outputDirectory, "no-installed-dotnet"),
    ASPNETCORE_URLS: "http://0.0.0.0:5000", ASPNETCORE_ENVIRONMENT: "Development",
    AETHERIS_CADMATA_PATH: "", AETHERIS_CAD_ASSISTANT_PATH: "",
    WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${debugPort}`,
  },
});
assert(app.pid, "The extracted app must start");
let closed = false;
app.on("exit", () => closed = true);
const report: Record<string, unknown> = { executable: join(packageDirectory, "Cadmata.exe"), pid: app.pid,
  qualification: "automated actual packaged WebView2 session; not a human manual session",
  guards: ["System32-only application PATH", "unrelated CWD", "fresh browser profile", "external web requests blocked by desktop host"], };
report.package = { identity: metadata.identity, webviewVersion: metadata.webviewVersion,
  executableSha256 };
let browser;
try {
  for (let attempt = 0; ; attempt++) {
    try { await fetch(`http://127.0.0.1:${debugPort}/json/version`); break; }
    catch {
      assert(!closed, "Cadmata exited before WebView2 became ready");
      if (attempt > 180) throw new Error("Packaged WebView2 did not become ready within 45 seconds");
      await new Promise(resolve => setTimeout(resolve, 250));
    }
  }
  browser = await chromium.connectOverCDP(`http://127.0.0.1:${debugPort}`);
  const context = browser.contexts()[0];
  const page = context.pages()[0] ?? await context.waitForEvent("page");
  page.setDefaultTimeout(45000);
  page.setDefaultNavigationTimeout(45000);
  const errors: string[] = [];
  const requests: string[] = [];
  const shaders: string[] = [];
  page.on("pageerror", (error: Error) => errors.push(String(error)));
  page.on("request", (request: { url(): string }) => requests.push(request.url()));
  page.on("response", async (response: { url(): string; json(): Promise<unknown> }) => {
    if (response.url().includes("/assembly/") || response.url().endsWith("/assemblies/display"))
      await writeFile(join(outputDirectory, "authored-display.json"), JSON.stringify(await response.json(), null, 2));
    if (response.url().includes(".wgsl")) shaders.push(response.url());
  });
  await page.waitForURL(/http:\/\/127\.0\.0\.1:\d+\/$/, { timeout: 45000 });
  const address = new URL(page.url()).origin;
  const logs = join(process.env.LOCALAPPDATA!, "Aetheris/Cadmata/logs");
  const logFile = (await readdir(logs)).find(file => file.endsWith(`-${app.pid}.log`));
  assert(logFile, "This app's PID-specific diagnostic log must exist");
  assert((await readFile(join(logs, logFile), "utf8")).includes("backend ready " + address),
    "Debug connection must belong to this newly spawned application before any UI actions");
  report.diagnosticLog = join(logs, logFile);
  report.unauthenticatedStatus = (await fetch(address + "/api/v1/startup/step", { method: "POST" })).status;
  assert.equal(report.unauthenticatedStatus, 403);
  await page.locator('[aria-label="Assembly product tree"] button').first().waitFor({ timeout: 45000 });
  await page.waitForFunction(() => document.querySelector('[data-display-host="three-telos"]')?.getAttribute("aria-busy") === "false");
  report.sampleVisibleMs = Date.now() - started;
  report.gpu = await page.evaluate(async () => {
    const adapter = await navigator.gpu?.requestAdapter();
    return { available: !!adapter, vendor: adapter?.info.vendor, architecture: adapter?.info.architecture, description: adapter?.info.description };
  });
  assert((report.gpu as { available: boolean }).available, "Bundled WebView2 must expose WebGPU");
  await page.waitForFunction(() => {
    const display = JSON.parse(document.querySelector("canvas")?.getAttribute("data-telos-display") ?? "{}");
    return display.meshSurfaces > 0 || display.fields > 0;
  }, undefined, { timeout: 30000 });
  report.directCirFieldDraws = await page.locator("canvas").getAttribute("data-telos-field-draws");
  report.sampleDisplay = JSON.parse(await page.locator("canvas").getAttribute("data-telos-display") ?? "{}");
  if (startupFile) assert(Number(report.directCirFieldDraws) > 0, "Qualified cylinder must draw direct CIR");
  const canvas = page.getByLabel("Engineering WebGPU viewport");
  const box = await canvas.boundingBox();
  assert(box);
  await page.screenshot({ path: join(outputDirectory, "packaged-sample.png") });
  const before = await canvas.screenshot();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width / 2 + 70, box.y + box.height / 2 + 25, { steps: 12 });
  await page.mouse.up();
  await page.waitForTimeout(200);
  assert(!before.equals(await canvas.screenshot()), "Orbit must change the rendered view");
  await page.mouse.down({ button: "right" });
  await page.mouse.move(box.x + box.width / 2 + 100, box.y + box.height / 2 + 40, { steps: 6 });
  await page.mouse.up({ button: "right" });
  await page.mouse.wheel(0, -130);
  await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
  await page.locator('[aria-label="Assembly product tree"] .active-row').waitFor();
  await page.screenshot({ path: join(outputDirectory, "packaged-orbit-selection.png") });
  report.navigationAndSelection = true;
  await page.locator('input[type="file"]').setInputFiles(stepFile);
  await page.getByRole("button", { name: "Import STEP 242", exact: true }).click();
  await page.getByText("Import complete.", { exact: true }).waitFor({ timeout: 120000 });
  const downloadButton = page.getByRole("button", { name: "Download Canonical 242", exact: true });
  await downloadButton.waitFor({ timeout: 120000 });
  await page.waitForFunction(() => {
    const canvas = document.querySelector<HTMLCanvasElement>("canvas");
    return JSON.parse(canvas?.dataset.telosDisplay ?? "{}").meshSurfaces > 0;
  });
  report.meshFallback = JSON.parse(await canvas.getAttribute("data-telos-display") ?? "{}");
  await page.waitForFunction(() => !document.querySelector('[data-display-host="three-telos"][aria-busy="true"]'));
  await page.screenshot({ path: join(outputDirectory, "packaged-step-import.png") });
  assert(!before.equals(await canvas.screenshot()), "External STEP must replace the rendered sample");
  const session = await context.newCDPSession(page);
  await session.send("Browser.setDownloadBehavior", { behavior: "allow", downloadPath: outputDirectory, eventsEnabled: true });
  const downloadEvent = new Promise<string>((resolve) => session.once("Browser.downloadWillBegin", (event: { suggestedFilename: string }) => resolve(event.suggestedFilename)));
  await downloadButton.click();
  const name = await Promise.race([downloadEvent, new Promise<never>((_, reject) => setTimeout(() => reject(new Error("STEP download event missing")), 15000))]);
  const exportedPath = join(outputDirectory, name);
  for (let attempt = 0; ; attempt++) {
    try { assert((await readFile(exportedPath, "utf8")).startsWith("ISO-10303-21;")); break; }
    catch (error) { if (attempt > 60) throw error; await new Promise(resolve => setTimeout(resolve, 250)); }
  }
  report.exportedStep = exportedPath;
  await page.locator('input[type="file"]').setInputFiles(exportedPath);
  await page.getByRole("button", { name: "Import STEP 242", exact: true }).click();
  await page.waitForTimeout(1500);
  assert(await downloadButton.isEnabled());
  // An invalid file must retain the last usable model and visible error.
  await page.locator('input[type="file"]').setInputFiles({ name: "invalid.step", mimeType: "application/step", buffer: Buffer.from("invalid STEP") });
  await page.getByRole("button", { name: "Import STEP 242", exact: true }).click();
  await page.getByText("Import failed", { exact: true }).waitFor();
  assert(await downloadButton.isEnabled());
  report.invalidFilePreservedModel = true;
  report.shaderUrls = shaders;
  report.requests = requests.filter(url => /^https?:/.test(url));
  assert((report.requests as string[]).every(url => new URL(url).origin === address), "Application resources must stay on its loopback origin");
  report.errors = errors;
  assert.equal(errors.length, 0, "No uncaught browser errors");
  await writeFile(join(outputDirectory, "page-text.txt"), await page.locator("body").innerText());
  // Integration-test WM_CLOSE on the owning form, including a window hidden
  // by the CI launcher. CloseMainWindow excludes hidden windows. This exercises
  // FormClosed cleanup, but is not the required human title-bar interaction.
  if (process.env.CADMATA_QUALIFICATION_MANUAL_CLOSE === "1") {
    await writeFile(join(outputDirectory, "ready-for-native-close.json"), JSON.stringify({ pid: app.pid, address }));
    const closeDeadline = Date.now() + 120000;
    while (!closed && Date.now() < closeDeadline) await new Promise(resolve => setTimeout(resolve, 250));
    report.closeMethod = "native UI close by external qualification operator";
  } else {
  const closeCommand = `
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class CadmataQualificationWindow {
  public delegate bool Visitor(IntPtr handle, IntPtr data);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Visitor visitor, IntPtr data);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr handle, StringBuilder text, int length);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr handle, uint message, IntPtr w, IntPtr l);
}
'@
$closedWindowCount = 0
[CadmataQualificationWindow]::EnumWindows({ param($handle, $data)
  $ownerProcess = 0
  [void][CadmataQualificationWindow]::GetWindowThreadProcessId($handle, [ref]$ownerProcess)
  $windowTitle = [Text.StringBuilder]::new(512)
  [void][CadmataQualificationWindow]::GetWindowText($handle, $windowTitle, 512)
  if ($ownerProcess -eq ${app.pid} -and $windowTitle.ToString().StartsWith('Cadmata')) {
    [void][CadmataQualificationWindow]::PostMessage($handle, 16, [IntPtr]::Zero, [IntPtr]::Zero)
    $script:closedWindowCount++
  }
  return $true
}, [IntPtr]::Zero) | Out-Null
$closedWindowCount
`;
  const close = await promisify(execFile)(join(process.env.SystemRoot!, "System32/WindowsPowerShell/v1.0/powershell.exe"),
    ["-NoProfile", "-Command", closeCommand], { windowsHide: true });
  report.closeWindowCount = Number(close.stdout.trim());
  assert(Number(report.closeWindowCount) > 0, "Owning native window must receive WM_CLOSE");
  report.closeMethod = "automated native WM_CLOSE, not a human title-bar click";
  }
  for (let attempt = 0; !closed && attempt < 80; attempt++) await new Promise(resolve => setTimeout(resolve, 100));
  assert(closed, "Closing the window must terminate the application");
  try { await fetch(address); throw new Error("Local CAD service survived closing the window"); }
  catch (error) { if (String(error).includes("survived")) throw error; }
  report.closeStoppedServer = true;
} catch (error) {
  report.failure = String(error);
  if (browser) {
    const page = browser.contexts()[0]?.pages()[0];
    if (page) await page.screenshot({ path: join(outputDirectory, "packaged-failure.png") }).catch(() => {});
  }
  throw error;
} finally {
  await writeFile(join(outputDirectory, "qualification.json"), JSON.stringify(report, null, 2));
  if (browser) await browser.close().catch(() => {});
  if (!closed) app.kill();
}
