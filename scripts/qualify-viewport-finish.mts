import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { mkdir, writeFile } from "node:fs/promises";
import { spawn } from "node:child_process";

const { chromium } = await import(pathToFileURL(resolve(process.argv[2])).href);
const phase = process.argv[3] ?? "before";
const output = resolve("artifacts/local/viewport-finish-x0", phase);
await mkdir(output, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const records: unknown[] = [];
try {
  for (const [product, url] of [
    ["cadmata", process.argv[4] ?? "http://127.0.0.1:5173"],
    ["helios", "http://127.0.0.1:4173/local"],
  ]) {
    const page = await browser.newPage({
      viewport: { width: 1600, height: 1000 },
      deviceScaleFactor: Number(process.argv[5] ?? 1),
    });
    const errors: string[] = [];
    const missingAssets: string[] = [];
    page.context().on("response", (response) => {
      if (response.status() === 404) missingAssets.push(response.url());
    });
    page.on("pageerror", (error) => errors.push(String(error)));
    page.on("console", (message) => {
      if (message.type() === "error") errors.push(`${message.text()} (${message.location().url})`);
    });
    await page.addInitScript(() => {
      const scope = window as any;
      scope.viewportProbe = {
        pipelines: [],
        submits: 0,
        encodeMs: [],
        completionMs: [],
        textures: new Set(),
        buffers: new Set(),
      };
      const configure = GPUCanvasContext.prototype.configure;
      GPUCanvasContext.prototype.configure = function (descriptor) {
        scope.viewportProbe.device = descriptor.device;
        const device = descriptor.device as any;
        for (const [method, collection] of [
          ["createTexture", "textures"],
          ["createBuffer", "buffers"],
        ]) {
          const original = device[method].bind(device);
          device[method] = (options: any) => {
            const resource = original(options);
            scope.viewportProbe[collection].add(resource);
            const destroy = resource.destroy.bind(resource);
            resource.destroy = () => {
              scope.viewportProbe[collection].delete(resource);
              destroy();
            };
            return resource;
          };
        }
        const pipeline = device.createRenderPipeline.bind(device);
        device.createRenderPipeline = (options: any) => {
          const started = performance.now();
          const result = pipeline(options);
          scope.viewportProbe.pipelines.push({
            samples: options.multisample?.count ?? 1,
            label: options.label,
            milliseconds: performance.now() - started,
          });
          return result;
        };
        const encoder = device.createCommandEncoder.bind(device);
        device.createCommandEncoder = (options: any) => {
          scope.viewportProbe.encoderStarted = performance.now();
          return encoder(options);
        };
        const submit = device.queue.submit.bind(device.queue);
        device.queue.submit = (commands: any) => {
          const started = performance.now();
          scope.viewportProbe.encodeMs.push(
            started - scope.viewportProbe.encoderStarted,
          );
          scope.viewportProbe.submits++;
          const result = submit(commands);
          void device.queue
            .onSubmittedWorkDone()
            .then(() =>
              scope.viewportProbe.completionMs.push(
                performance.now() - started,
              ),
            )
            .catch(() => {});
          return result;
        };
        return configure.call(this, descriptor);
      };
    });
    await page.goto(url);
    await page
      .locator("[data-display-host=three-telos] canvas")
      .waitFor({ timeout: 120000 });
    await page.waitForTimeout(3000);
    await page.screenshot({ path: `${output}/${product}-initial.png` });
    records.push({
      product,
      state: "initial",
      alerts: await page.getByRole("alert").allTextContents(),
      text: (await page.locator("body").innerText()).slice(0, 15000),
      errors: [...errors],
      missingAssets: [...missingAssets],
    });
    if (product === "cadmata") {
      await page
        .getByRole("tab", { name: "STEP 242 Viewer", exact: true })
        .click();
      await page
        .locator("input[type=file]")
        .setInputFiles(
          resolve(
            "docs/development/milestones/modules/sheetmetal/artifacts/ctc03-manufacturing-release/ctc03-manufacturing-ap242.step",
          ),
        );
      await page
        .getByRole("button", { name: "Import STEP 242", exact: true })
        .click();
      await page.waitForTimeout(5000);
      await page.waitForTimeout(300);
      await page.screenshot({ path: `${output}/cadmata-pmi.png` });
      await page
        .getByRole("combobox", { name: "Viewport theme", exact: true })
        .selectOption("monument");
      await page.waitForTimeout(200);
      await page.screenshot({ path: `${output}/cadmata-light.png` });
      await page
        .getByRole("combobox", { name: "Viewport theme", exact: true })
        .selectOption("atelier");
    } else {
      await page.waitForFunction(
        () =>
          document
            .querySelector(".status-ready")
            ?.textContent?.includes("READY"),
        null,
        { timeout: 120000 },
      );
      await page.screenshot({ path: `${output}/helios-part.png` });
    }
    records.push({
      product,
      state: "model",
      alerts: await page.getByRole("alert").allTextContents(),
      text: (await page.locator("body").innerText()).slice(0, 15000),
      errors: [...errors],
      missingAssets: [...missingAssets],
    });
    if (phase === "final") {
      const canvas = page.locator("[data-display-host=three-telos] canvas");
      if (product === "cadmata") {
        await page
          .getByRole("button", { name: "Inspect Datum A", exact: true })
          .click();
        if (
          !(
            await page
              .getByRole("button", { name: "Inspect Datum A", exact: true })
              .getAttribute("class")
          )?.includes("is-selected")
        )
          throw new Error("PMI selection lost");
      } else {
        await page.getByRole("button", { name: "TOP", exact: true }).click();
        await page.waitForTimeout(150);
        const bounds = (await canvas.boundingBox())!;
        await canvas.click({
          position: { x: bounds.width * 0.65, y: bounds.height * 0.45 },
        });
        await page.waitForTimeout(150);
        const selection = await page.locator(".status-selection").innerText();
        if (!selection.includes("face:"))
          throw new Error("engineering face selection missing: " + selection);
        records.push({ product, state: "selection", selector: selection });
      }
      await page.screenshot({ path: `${output}/${product}-selected.png` });
      const before = await canvas.screenshot();
      const rect = (await canvas.boundingBox())!;
      await page.mouse.move(
        rect.x + rect.width * 0.7,
        rect.y + rect.height * 0.45,
      );
      await page.mouse.down();
      await page.mouse.move(
        rect.x + rect.width * 0.7 + 75,
        rect.y + rect.height * 0.45 + 25,
        { steps: 12 },
      );
      await page.mouse.up();
      await page.waitForTimeout(200);
      if (before.equals(await canvas.screenshot()))
        throw new Error(`${product} orbit did not redraw`);
      await page.mouse.move(0, 0);
      await page.setViewportSize({ width: 1150, height: 850 });
      await page.waitForTimeout(200);
      const sizes = await canvas.evaluate((element: HTMLCanvasElement) => ({
        width: element.width,
        height: element.height,
        cssWidth: element.clientWidth,
        cssHeight: element.clientHeight,
        dpr: devicePixelRatio,
      }));
      if (
        Math.abs(sizes.width - sizes.cssWidth * sizes.dpr) > 2 ||
        Math.abs(sizes.height - sizes.cssHeight * sizes.dpr) > 2
      )
        throw new Error("stale resize target");
      await page.screenshot({ path: `${output}/${product}-resize.png` });
      const session = await page.context().newCDPSession(page);
      await session.send("Emulation.setDeviceMetricsOverride", {
        width: 1150,
        height: 850,
        deviceScaleFactor: 3,
        mobile: false,
      });
      await page.waitForTimeout(750);
      const liveDpr = await canvas.evaluate((element: HTMLCanvasElement) => ({
        width: element.width,
        height: element.height,
        cssWidth: element.clientWidth,
        cssHeight: element.clientHeight,
        dpr: devicePixelRatio,
      }));
      if (
        liveDpr.dpr !== 3 ||
        Math.abs(liveDpr.width - liveDpr.cssWidth * 3) > 2
      )
        throw new Error(
          "DPR change did not resize render target: " + JSON.stringify(liveDpr),
        );
      const resources = () =>
        page.evaluate(() => {
          const probe = (window as any).viewportProbe;
          const summarize = (values: number[]) => ({
            count: values.length,
            mean: values.reduce((sum, value) => sum + value, 0) / values.length,
            maximum: Math.max(...values),
          });
          return {
            buffers: probe.buffers.size,
            textures: probe.textures.size,
            submits: probe.submits,
            encodeMs: summarize(probe.encodeMs),
            completionMs: summarize(probe.completionMs),
            samples: [
              ...new Set(probe.pipelines.map((item: any) => item.samples)),
            ],
          };
        });
      const steadyBefore = await resources();
      await page.waitForTimeout(500);
      const steadyAfter = await resources();
      if (steadyAfter.submits !== steadyBefore.submits)
        throw new Error(`${product} continues rendering while idle`);
      const cameraSteps = await page.evaluate(async () => {
        const element = document.querySelector(
          "[data-display-host=three-telos] canvas",
        )!;
        const device = (window as any).viewportProbe.device;
        const times: number[] = [];
        for (let index = 0; index < 20; index++) {
          const started = performance.now();
          element.dispatchEvent(
            new WheelEvent("wheel", {
              deltaY: index % 2 ? -5 : 5,
              cancelable: true,
            }),
          );
          await new Promise(requestAnimationFrame);
          await device.queue.onSubmittedWorkDone();
          times.push(performance.now() - started);
        }
        return {
          count: times.length,
          mean: times.reduce((sum, value) => sum + value, 0) / times.length,
          maximum: Math.max(...times),
        };
      });
      await page.evaluate(() => (window as any).viewportProbe.device.destroy());
      await page
        .getByRole("button", { name: "Restart viewport", exact: true })
        .waitFor();
      await page.screenshot({ path: `${output}/${product}-device-loss.png` });
      await page
        .getByRole("button", { name: "Restart viewport", exact: true })
        .click();
      await page.waitForTimeout(1000);
      if (
        await page
          .getByRole("button", { name: "Restart viewport", exact: true })
          .count()
      )
        throw new Error("viewport recovery failed");
      await page.screenshot({ path: `${output}/${product}-recovered.png` });
      records.push({
        product,
        state: "lifecycle",
        sizes,
        liveDpr,
        steadyBefore,
        steadyAfter,
        cameraSteps,
        recovered: await resources(),
      });
      if (product === "helios") {
        await page.locator("input[type=file]").setInputFiles({
          name: "invalid.firmament",
          mimeType: "text/plain",
          buffer: Buffer.from(
            "Model Broken { Units: mm Box Body { Size: [invalid] } }",
          ),
        });
        await page.waitForTimeout(2500);
        const text = await page.locator("body").innerText();
        if (
          !text.includes("DISPLAY REV 0") ||
          !text.includes("MODEL OUT OF DATE")
        )
          throw new Error("last valid model was lost");
        await page.screenshot({ path: `${output}/helios-last-valid.png` });
        records.push({
          product,
          state: "failed-build",
          text: text.slice(-5000),
        });
        await page
          .locator("input[type=file]")
          .setInputFiles(
            resolve("fixtures/Canonical/Scene/WarmModernHouse/house.firmament"),
          );
        await page.waitForTimeout(2500);
        records.push({
          product,
          state: "house-refusal",
          text: (await page.locator("body").innerText()).slice(-7000),
        });
        await page.screenshot({ path: `${output}/helios-house-refusal.png` });
      } else {
        await page.setViewportSize({ width: 1600, height: 1000 });
        await session.send("Emulation.setDeviceMetricsOverride", {
          width: 1600,
          height: 1000,
          deviceScaleFactor: 2,
          mobile: false,
        });
        await page.waitForTimeout(750);
        await page
          .locator("input[type=file]")
          .setInputFiles(
            resolve("artifacts/local/viewport-finish-x0/threaded.step"),
          );
        const imported = page.waitForResponse(
          (response) => response.url().endsWith("/import/step"),
          { timeout: 120000 },
        );
        const prepared = page
          .waitForResponse(
            (response) => response.url().endsWith("/display/prepare"),
            { timeout: 120000 },
          )
          .catch(() => null);
        await page
          .getByRole("button", { name: "Import STEP 242", exact: true })
          .click();
        const importResponse = await imported;
        const importedFileName = importResponse.request().postDataJSON().name;
        if (importedFileName !== "threaded.step")
          throw new Error("Stale STEP selection imported");
        const importStatus = importResponse.status();
        const display = importStatus === 200 ? await prepared : null;
        const displaySuccess = display?.status() === 200;
        if (displaySuccess) {
          await page.waitForFunction(
            () =>
              document
                .querySelector(".telos-viewport")
                ?.getAttribute("aria-busy") === "false",
            null,
            { timeout: 120000 },
          );
          const bounds = await page
            .locator(".telos-viewport canvas")
            .boundingBox();
          if (bounds) {
            await page.mouse.move(
              bounds.x + bounds.width / 2,
              bounds.y + bounds.height / 2,
            );
            await page.mouse.wheel(0, -180);
          }
        }
        await page.waitForTimeout(500);
        await page.screenshot({
          path: `${output}/${displaySuccess ? "mechanical-closeup" : "thread-refusal"}.png`,
        });
        records.push({
          product,
          state: "threaded",
          alerts: await page.getByRole("alert").allTextContents(),
          resources: await resources(),
          importStatus,
          importedFileName,
          displaySuccess,
          text: (await page.locator("body").innerText()).slice(-7000),
        });
      }
    }
    await page.close();
  }
  if (phase === "final") {
    for (const [product, url] of [
      ["cadmata", process.argv[4] ?? "http://127.0.0.1:5173"],
      ["helios", "http://127.0.0.1:4173/local"],
    ]) {
      const page = await browser.newPage({
        viewport: { width: 1600, height: 1000 },
      });
      await page.addInitScript(() => {
        Object.defineProperty(navigator, "gpu", { value: undefined });
      });
      await page.goto(url);
      await page
        .locator("[data-display-host=transitional-webgl] canvas")
        .waitFor({ timeout: 30000 });
      if (await page.locator("[data-display-host=three-telos]").count())
        throw new Error("dual renderer fallback");
      await page.screenshot({ path: `${output}/${product}-fallback.png` });
      records.push({
        product,
        state: "fallback",
        fallbackCanvasCount: await page
          .locator("[data-display-host=transitional-webgl] canvas")
          .count(),
      });
      await page.close();
    }
    for (const [name, source] of [
      [
        "guitar",
        "fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm",
      ],
      [
        "robot",
        "fixtures/Canonical/AssemblyInterfaces/IndustrialAtlas/atlas-industrial.firmament",
      ],
    ]) {
      // Startup claims are intentionally one-shot. Each capture owns a fresh real product process.
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
          const timeout = setTimeout(
            () => reject(new Error("Cadmata startup timeout")),
            20000,
          );
          host.on("error", reject);
          let output = "";
          host.stdout.on("data", (chunk) => {
            output += String(chunk);
            const match = output.match(/Cadmata ready: (http:\/\/[^\s]+)/);
            if (match) {
              clearTimeout(timeout);
              done(match[1]);
            }
          });
        });
        const page = await browser.newPage({
          viewport: { width: 1600, height: 1000 },
          deviceScaleFactor: 2,
        });
        const started = performance.now();
        await page.goto(url);
        await page
          .getByText("Product tree", { exact: true })
          .waitFor({ timeout: 120000 });
        await page.waitForTimeout(500);
        await page.screenshot({ path: `${output}/${name}.png` });
        const alerts = await page.getByRole("alert").allTextContents();
        records.push({
          product: "cadmata",
          state: name,
          firstDisplayMs: performance.now() - started,
          alerts,
        });
        await page.close();
      } finally {
        host.kill();
      }
    }
  }
} finally {
  await browser.close();
  await writeFile(`${output}/audit.json`, JSON.stringify(records, null, 2));
}
