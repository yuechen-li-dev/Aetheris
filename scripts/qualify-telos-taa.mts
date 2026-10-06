import { createServer } from "node:http";
import { readFile, mkdir, writeFile } from "node:fs/promises";
import { resolve, extname, sep } from "node:path";
import { pathToFileURL } from "node:url";
const { chromium } = await import(pathToFileURL(resolve(process.argv[2])).href);
const repo = process.cwd();
const output = resolve(repo, "artifacts/local/telos-taa/browser");
await mkdir(output, { recursive: true });
const server = createServer(async (request, response) => {
  try {
    const file = resolve(
      repo,
      "." +
        decodeURIComponent(new URL(request.url!, "http://localhost").pathname),
    );
    if (!file.startsWith(repo + sep)) throw new Error("outside repo");
    response.setHeader(
      "Content-Type",
      (
        {
          ".js": "text/javascript",
          ".html": "text/html",
          ".json": "application/json",
        } as Record<string, string>
      )[extname(file)] ?? "application/octet-stream",
    );
    response.end(await readFile(file));
  } catch {
    response.statusCode = 404;
    response.end("missing");
  }
});
await new Promise<void>((done) => server.listen(0, "127.0.0.1", done));
const address = server.address();
if (!address || typeof address === "string") throw new Error("no address");
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const context = await browser.newContext({
  viewport: { width: 760, height: 690 },
  recordVideo: { dir: output, size: { width: 760, height: 690 } },
});
const page = await context.newPage();
const errors: string[] = [];
page.on("pageerror", (error: Error) => errors.push(String(error)));
try {
  if (process.argv.includes("--fallback-only")) {
    await page.route("**/temporal-*.wgsl", (route: any) => route.abort());
    await page.goto(
      `http://127.0.0.1:${address.port}/Aetheris.Web.Runtime/telos/tests/browser.html`,
    );
    await page.waitForFunction(
      () => Boolean((window as any).telosQualification),
      { timeout: 30000 },
    );
    const fallback = await page.evaluate(async () => {
      const qualification = (window as any).telosQualification;
      if (qualification.status !== "PASS")
        throw new Error(JSON.stringify(qualification));
      const host = (window as any).telosWitness;
      host.setAA("TAAUtility");
      host.render();
      await host.readPixels();
      const result = {
        mode: host.aaMode,
        temporalAvailable: Boolean(host.temporal),
        stopped: host.owner.stopped,
      };
      host.dispose();
      return result;
    });
    if (
      fallback.mode !== "SpatialOnly" ||
      fallback.temporalAvailable ||
      fallback.stopped
    )
      throw new Error(JSON.stringify(fallback));
    await writeFile(
      resolve(output, "fallback.json"),
      JSON.stringify(fallback, null, 2),
    );
    console.log("Missing temporal assets: spatial viewport qualified");
  } else {
    await page.goto(
      `http://127.0.0.1:${address.port}/Aetheris.Web.Runtime/telos/tests/taa-browser.html`,
    );
    await page.waitForFunction(() => Boolean((window as any).taa), {
      timeout: 30000,
    });
    const setup = await page.evaluate(() => ({
      adapter: (window as any).taa.host.owner.capabilities.features,
      format: (window as any).taa.host.owner.format,
    }));
    console.log(JSON.stringify(setup));
    for (const mode of ["None", "TAA", "TAAUtility"]) {
      for (const scenario of [
        "static",
        "orbit",
        "fast",
        "disocclusion",
        "moving-occurrence",
      ]) {
        await page.evaluate(
          (mode: string) => (window as any).taa.start(mode),
          mode,
        );
        for (let index = 0; index < 24; index++) {
          await page.evaluate(
            ({ scenario, index }: any) =>
              (window as any).taa.frame(scenario, index),
            { scenario, index },
          );
          await page.waitForTimeout(24);
        }
        await page
          .locator("#view")
          .screenshot({ path: resolve(output, `${mode}-${scenario}.png`) });
        console.log(`${mode}: ${scenario} complete`);
        if (mode === "TAAUtility" && scenario === "static") {
          const counts = await page.evaluate(() => (window as any).taa.debug());
          await writeFile(
            resolve(output, "policy-counts.json"),
            JSON.stringify(counts, null, 2),
          );
          await page
            .locator("#view")
            .screenshot({ path: resolve(output, "utility-policy.png") });
          if (!(counts.stable > 0)) throw new Error("no stable history pixels");
          if (!(counts.reject > 0)) throw new Error("no rejection pixels");
        }
      }
    }
    const quality = [];
    for (const mode of ["None", "TAA", "TAAUtility"]) {
      quality.push(
        await page.evaluate(
          (mode: string) => (window as any).taa.quality(mode),
          mode,
        ),
      );
    }
    const realCad = [];
    for (const mode of ["None", "TAA", "TAAUtility"]) {
      realCad.push(
        await page.evaluate(
          (mode: string) => (window as any).taa.realCad(mode),
          mode,
        ),
      );
      await page
        .locator("#view")
        .screenshot({ path: resolve(output, `${mode}-ctc03.png`) });
    }
    const result = await page.evaluate(() => ({
      results: (window as any).taa.results,
      errors: (window as any).taa.errors,
    }));
    await writeFile(
      resolve(output, "metrics.json"),
      JSON.stringify(
        { setup, quality, realCad, ...result, pageErrors: errors },
        null,
        2,
      ),
    );
    if (errors.length || result.errors.length)
      throw new Error(JSON.stringify({ errors, resultErrors: result.errors }));
    await page.evaluate(() => (window as any).taa.dispose());
  }
} finally {
  await context.close();
  await browser.close();
  server.close();
}
