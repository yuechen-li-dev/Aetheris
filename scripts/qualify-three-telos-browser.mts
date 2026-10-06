import { createServer } from "node:http";
import { readFile, mkdir, writeFile } from "node:fs/promises";
import { resolve, extname, sep } from "node:path";
import { pathToFileURL, fileURLToPath } from "node:url";

const repo = resolve(fileURLToPath(new URL("..", import.meta.url)));
const modulePath = process.argv[2];
if (!modulePath) throw new Error("Pass an explicit Playwright module path.");
const { chromium } = await import(pathToFileURL(resolve(modulePath)).href);
const server = createServer(async (request, response) => {
  try {
    const file = resolve(
      repo,
      "." +
        decodeURIComponent(new URL(request.url!, "http://localhost").pathname),
    );
    if (!file.startsWith(repo + sep)) throw new Error("outside repo");
    const data = await readFile(file);
    const type: Record<string, string> = {
      ".js": "text/javascript",
      ".html": "text/html",
      ".json": "application/json",
      ".wasm": "application/wasm",
    };
    response.setHeader(
      "Content-Type",
      type[extname(file)] ?? "application/octet-stream",
    );
    response.end(data);
  } catch {
    response.statusCode = 404;
    response.end("not found");
  }
});
await new Promise<void>((done) => server.listen(0, "127.0.0.1", done));
const address = server.address();
if (!address || typeof address === "string") throw new Error("no port");
const output = resolve(repo, "artifacts/local/three-telos");
await mkdir(output, { recursive: true });
let browser;
try {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  const page = await browser.newPage({
    viewport: { width: 1100, height: 1000 },
  });
  const consoleErrors: string[] = [];
  page.on("pageerror", (error: Error) => consoleErrors.push(String(error)));
  await page.goto(
    `http://127.0.0.1:${address.port}/Aetheris.Web.Runtime/telos/tests/browser.html`,
  );
  await page.waitForFunction(
    () =>
      Boolean(
        (window as unknown as { telosQualification?: unknown })
          .telosQualification,
      ),
    null,
    { timeout: 60000 },
  );
  const report = await page.evaluate(
    () =>
      (window as unknown as { telosQualification: { status: string } })
        .telosQualification,
  );
  await page.screenshot({
    path: resolve(output, "witness.png"),
    fullPage: true,
  });
  await page.locator('canvas').screenshot({ path: resolve(output, 'witness-canvas.png') });
  await writeFile(
    resolve(output, "browser-report.json"),
    JSON.stringify({ ...report, consoleErrors }, null, 2),
  );
  console.log(JSON.stringify({ ...report, consoleErrors }, null, 2));
  if (report.status !== "PASS" || consoleErrors.length) process.exitCode = 1;
} finally {
  await browser?.close();
  await new Promise<void>((done, reject) =>
    server.close((error) => (error ? reject(error) : done())),
  );
}
