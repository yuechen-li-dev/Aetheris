import { resolve, dirname } from "node:path";
import { pathToFileURL } from "node:url";
import { mkdir, writeFile, readdir } from "node:fs/promises";
import { spawn } from "node:child_process";

// Real product audit. File input/startup and existing APIs supply every model.
const { chromium } = await import(pathToFileURL(resolve(process.argv[2])).href);
const output = resolve(
  process.argv[3] ?? "artifacts/local/display-projection-closeout/products-a2",
);
await mkdir(output, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const records: unknown[] = [];
async function pageFor(url: string) {
  const page = await browser.newPage({
    viewport: { width: 1600, height: 1000 },
    deviceScaleFactor: 2,
  });
  await page.addInitScript(() => {
    (window as any).projectionAuditPipelines = [];
    const original = GPUDevice.prototype.createRenderPipeline;
    GPUDevice.prototype.createRenderPipeline = function (descriptor) {
      (window as any).projectionAuditPipelines.push(descriptor.label);
      return original.call(this, descriptor);
    };
  });
  await page.goto(url);
  await page.locator("[data-display-host=three-telos] canvas").waitFor();
  return page;
}
try {
  for (const [name, source] of [
    ["cylinder", "fixtures/Canonical/Basics/cylinder.firmament"],
    ["guitar", "fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm"],
    ["house", "fixtures/Canonical/Scene/WarmModernHouse/house.firmament"],
    ["factory", "fixtures/Canonical/Scene/FactoryX0/factory.firmament"],
    [
      "robot",
      "fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament",
    ],
  ]) {
    const host = spawn(
      "dotnet",
      [
        resolve("Aetheris.Server/bin/Release/net10.0/Cadmata.dll"),
        resolve(source),
        "--urls",
        "http://127.0.0.1:0",
        "--no-browser",
      ],
      {
        windowsHide: true,
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: "Development" },
      },
    );
    try {
      const url = await new Promise<string>((done, reject) => {
        const timer = setTimeout(
          () => reject(new Error("Cadmata startup timeout")),
          20000,
        );
        let text = "";
        host.once("error", reject);
        host.stdout.on("data", (chunk) => {
          text += String(chunk);
          const match = text.match(/Cadmata ready: (http:\/\/[^\s]+)/);
          if (match) {
            clearTimeout(timer);
            done(match[1]);
          }
        });
      });
      // API and UI exercise the same current product owner, without a fixture loader.
      const page = await pageFor(url);
      const response = await page.request.post(
        url + "/api/v1/assemblies/display",
        { data: { path: resolve(source) } },
      );
      const envelope = await response.json();
      await page.waitForFunction(
        () =>
          document.body.textContent?.includes("Action: error") ||
          document.body.textContent?.includes("Product tree"),
        null,
        { timeout: 120000 },
      );
      await page.waitForTimeout(300);
      const data = envelope.data;
      const display = data?.display;
      const cameras = page.getByRole("combobox", { name: "Scene camera" });
      if (await cameras.count()) {
        await cameras.selectOption("Hero");
        await page.waitForTimeout(300);
      }
      records.push({
        product: "cadmata",
        name,
        responseStatus: response.status(),
        diagnostics: envelope.diagnostics,
        definitions: display?.definitions?.length ?? data?.definitions?.length,
        occurrences: display?.occurrences?.length ?? data?.occurrences?.length,
        qualifiedFields: display?.definitions?.filter(
          (d) => d.cir?.qualification === "cir-qualified",
        ).length,
        materials: display?.occurrences?.filter((o) => o.material).length,
        translucentMaterials: display?.occurrences?.filter(
          (o) => o.material?.opacity < 1,
        ).length,
        cameras: display?.cameras?.length,
        fallbacks: [
          ...new Set(display?.definitions?.map((d) => d.fallbackReason) ?? []),
        ],
        packetKeys: data ? Object.keys(data) : [],
        definitionKeys: data?.definitions?.[0]
          ? Object.keys(data.definitions[0])
          : [],
        pipelines: await page.evaluate(
          () => (window as any).projectionAuditPipelines,
        ),
        displayState: await page
          .locator("[data-display-host=three-telos] canvas")
          .evaluate((canvas: HTMLCanvasElement) =>
            JSON.parse(canvas.dataset.telosDisplay ?? "{}"),
          ),
        text: (await page.locator("body").innerText()).slice(0, 10000),
      });
      await page.screenshot({ path: `${output}/cadmata-${name}.png` });
      await page.close();
    } finally {
      host.kill();
    }
  }
  const page = await pageFor("http://127.0.0.1:4173/local");
  await page.waitForTimeout(2500);
  for (const [name, source] of [
    ["cylinder", "fixtures/Canonical/Basics/cylinder.firmament"],
    ["house", "fixtures/Canonical/Scene/WarmModernHouse/house.firmament"],
  ]) {
    const files =
      name === "house"
        ? [
            resolve(source),
            ...(await readdir(dirname(resolve(source))))
              .filter(
                (file) =>
                  file.endsWith(".firmament") && file !== "house.firmament",
              )
              .map((file) => resolve(dirname(source), file)),
          ]
        : [resolve(source)];
    await page.locator("input[type=file]").setInputFiles(files);
    await page.waitForFunction(
      (name) =>
        document.body.innerText.includes(name + ".firmament") &&
        (document.body.innerText.includes(
          "Build failed. The last valid model",
        ) ||
          document.body.innerText.includes(
            name === "house" ? "94 DEFS · 254 OCC" : "✓ 0 DIAGNOSTICS",
          )),
      name,
      { timeout: 120000 },
    );
    await page.waitForTimeout(name === "house" ? 5000 : 3000);
    const cameras = page.getByRole("combobox", { name: "Scene camera" });
    if (await cameras.count()) {
      await cameras.selectOption("Hero");
      await page.waitForTimeout(300);
    }
    records.push({
      product: "helios",
      name,
      pipelines: await page.evaluate(
        () => (window as any).projectionAuditPipelines,
      ),
      displayState: await page
        .locator("[data-display-host=three-telos] canvas")
        .evaluate((canvas: HTMLCanvasElement) =>
          JSON.parse(canvas.dataset.telosDisplay ?? "{}"),
        ),
      text: (await page.locator("body").innerText()).slice(-10000),
    });
    await page.screenshot({ path: `${output}/helios-${name}.png` });
  }
  await page.close();
} finally {
  await writeFile(`${output}/audit.json`, JSON.stringify(records, null, 2));
  await browser.close();
}
