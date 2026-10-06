import assert from "node:assert/strict";
import { resolve, dirname } from "node:path";
import { pathToFileURL } from "node:url";
import { mkdir, readFile, writeFile, readdir } from "node:fs/promises";
import { spawn } from "node:child_process";
import { inflateSync } from "node:zlib";

// Normal startup/file input/editor actions only. GPU instrumentation observes
// submission; the single negative case rejects a real compiler-produced module.
const { chromium } = await import(pathToFileURL(resolve(process.argv[2])).href);
const output = resolve(
  process.argv[3] ?? "artifacts/local/cir-artifact-binding/products",
);
await mkdir(output, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const records: any[] = [];
const failureOnly = process.argv.includes("--failure-only");
async function pageFor(url: string, fail = false) {
  const page = await browser.newPage({
    viewport: { width: 1600, height: 1000 },
    deviceScaleFactor: 1,
  });
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.stack ?? String(e)));
  page.on("requestfailed", (r) =>
    console.error("REQUEST FAILED", r.url(), r.failure()),
  );
  await page.addInitScript((fail: boolean) => {
    const scope = window as any;
    scope.cirAudit = { modules: [], pipelines: [], draws: 0, fail };
    const module = GPUDevice.prototype.createShaderModule;
    GPUDevice.prototype.createShaderModule = function (d) {
      scope.cirAudit.modules.push(d.label);
      if (scope.cirAudit.fail && /^[a-f0-9]{64}$/.test(String(d.label))) {
        scope.cirAudit.fail = false;
        return module.call(this, {
          ...d,
          code: "bounded qualification: invalid WGSL",
        });
      }
      return module.call(this, d);
    };
    const pipeline = GPUDevice.prototype.createRenderPipeline;
    GPUDevice.prototype.createRenderPipeline = function (d) {
      scope.cirAudit.pipelines.push(d.label);
      return pipeline.call(this, d);
    };
    const begin = GPUCommandEncoder.prototype.beginRenderPass;
    GPUCommandEncoder.prototype.beginRenderPass = function (d) {
      const pass = begin.call(this, d),
        set = pass.setPipeline.bind(pass),
        draw = pass.draw.bind(pass);
      let field = false;
      pass.setPipeline = (p) => {
        field = /^\["[a-f0-9]{64}"/.test(p.label);
        return set(p);
      };
      pass.draw = (...args) => {
        if (field) scope.cirAudit.draws++;
        return draw(...args);
      };
      return pass;
    };
  }, fail);
  await page.goto(url);
  return { page, errors };
}
async function inspect(page: any) {
  return page
    .locator("[data-display-host=three-telos] canvas")
    .evaluate((canvas: HTMLCanvasElement) => ({
      ...JSON.parse(canvas.dataset.telosDisplay ?? "{}"),
      submittedFields: Number(canvas.dataset.telosFieldDraws),
      gpu: (window as any).cirAudit,
    }));
}
async function ready(page: any, fields: number, mesh: number, fail = false) {
  await page
    .waitForFunction(
      ({ fields, mesh, fail }) => {
        const canvas = document.querySelector(
          "[data-display-host=three-telos] canvas",
        ) as HTMLCanvasElement;
        if (!canvas?.dataset.telosDisplay) return false;
        const display = JSON.parse(canvas.dataset.telosDisplay);
        return (
          display.fields === fields &&
          display.meshSurfaces === mesh &&
          (fail
            ? display.shaders.some((s: string[]) =>
                s[1].startsWith("shader-artifact-gpu-failed"),
              )
            : fields === 0 || Number(canvas.dataset.telosFieldDraws) === fields)
        );
      },
      { fields, mesh, fail },
      { timeout: 120000 },
    )
    .catch(async (error) => {
      console.error(
        await inspect(page),
        await page.locator("body").innerText(),
      );
      await page.screenshot({ path: `${output}/failed-readiness.png` });
      throw error;
    });
  const state = await inspect(page);
  if (fields)
    assert(state.gpu.draws > 0, "Actual compiler pipeline draw required");
  return state;
}
async function interact(page: any) {
  const canvas = page.locator("[data-display-host=three-telos] canvas"),
    box = await canvas.boundingBox();
  const x = box.x + box.width * 0.5,
    y = box.y + box.height * 0.5;
  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.mouse.move(x + 55, y + 25, { steps: 6 });
  await page.mouse.up();
  await page.mouse.wheel(0, -180);
  await page.waitForTimeout(200);
  await page.mouse.click(x, y);
  await page.waitForTimeout(150);
}
async function replaceDigit(
  page: any,
  source: string,
  token: string,
  digit: string,
  digitOffset = 0,
) {
  const lines = source.split(/\r?\n/);
  const line = lines.findIndex((l) => l.includes(token));
  assert(line >= 0);
  const column = lines[line].indexOf(token) + digitOffset;
  await page.getByRole("button", { name: "Edit Source", exact: true }).click();
  await page.waitForTimeout(100);
  await page.keyboard.press("Control+Home");
  for (let i = 0; i < line; i++) await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Home");
  for (let i = 0; i <= column; i++) await page.keyboard.press("ArrowRight");
  await page.keyboard.press("Backspace");
  await page.keyboard.type(digit, { delay: 30 });
  await page.waitForFunction(() =>
    document
      .querySelector(".statusbar")
      ?.textContent?.includes("MODEL OUT OF DATE"),
  );
  await page.getByRole("button", { name: /^Build$/i }).click();
  await page
    .locator(".statusbar .status-ready")
    .filter({ hasText: "READY" })
    .waitFor({ timeout: 120000 });
  assert(
    !(await page.locator(".statusbar").innerText()).includes(
      "MODEL OUT OF DATE",
    ),
  );
}
async function mixedDepth(page: any, product: string) {
  const results: any[] = [];
  for (const camera of ["Front", "Back"]) {
    await page
      .getByRole("combobox", { name: "Scene camera" })
      .selectOption(camera);
    await page.waitForTimeout(250);
    const box = await page
      .locator("[data-display-host=three-telos] canvas")
      .boundingBox();
    // Clear the prior pick's gold face overlay before testing surface colour/depth.
    await page.mouse.click(box.x + box.width - 15, box.y + box.height - 15);
    await page.waitForTimeout(150);
    const x = Math.floor(box.x + box.width / 2),
      y = Math.floor(box.y + box.height / 2);
    const png = await page.screenshot({ clip: { x, y, width: 1, height: 1 } });
    const chunks: Buffer[] = [];
    for (let i = 8; i < png.length; ) {
      const size = png.readUInt32BE(i);
      if (png.toString("ascii", i + 4, i + 8) === "IDAT")
        chunks.push(png.subarray(i + 8, i + 8 + size));
      i += size + 12;
    }
    const rgb = [...inflateSync(Buffer.concat(chunks)).subarray(1, 4)];
    await page.mouse.click(x, y);
    await page.waitForTimeout(150);
    const selected =
      product === "cadmata"
        ? await page.locator(".semantic-tree__item.active-row").innerText()
        : await page.locator(".status-selection").innerText();
    if (camera === "Front") {
      assert(selected.includes("Mesh"), selected);
      assert(rgb[2] > rgb[0], String(rgb));
    } else {
      assert(selected.includes("CylinderA"), selected);
      assert(rgb[0] > rgb[2], String(rgb));
    }
    results.push({ camera, rgb, selected });
    await page.screenshot({
      path: `${output}/${product}-mixed-depth-${camera}.png`,
    });
  }
  return results;
}
const primitiveSources = [
  ["cylinder", "fixtures/Canonical/Basics/cylinder.firmament"],
  ...["sphere", "cone", "torus"].map((name) => [
    name,
    `fixtures/Canonical/DisplayProjection/${name}.firmament`,
  ]),
  ["mixed", "fixtures/Canonical/DisplayProjection/mixed.firmament"],
  ["scene", "fixtures/Canonical/DisplayProjection/scene.firmament"],
  [
    "locating-pin",
    "fixtures/Canonical/DisplayProjection/locating-pin.firmament",
  ],
] as const;
try {
  for (const [name, source] of failureOnly
    ? []
    : [
        ...primitiveSources,
        ["failure", "fixtures/Canonical/Basics/cylinder.firmament"],
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
        let text = "";
        const timer = setTimeout(() => reject(Error("Cadmata timeout")), 20000);
        host.on("error", reject);
        host.stdout.on("data", (c) => {
          text += c;
          const m = text.match(/Cadmata ready: (http:\/\/[^\s]+)/);
          if (m) {
            clearTimeout(timer);
            done(m[1]);
          }
        });
      });
      const { page, errors } = await pageFor(url, name === "failure");
      const packet = await (
        await page.request.post(url + "/api/v1/assemblies/display", {
          data: { path: resolve(source) },
        })
      ).json();
      assert(packet.success ?? packet.data, "Normal compilation must succeed");
      const mixed = name === "mixed" || name === "scene",
        fail = name === "failure";
      const state = await ready(
        page,
        fail ? 0 : mixed ? 2 : 1,
        fail || mixed ? 1 : 0,
        fail,
      );
      await interact(page);
      const afterInteraction = await ready(
        page,
        fail ? 0 : mixed ? 2 : 1,
        fail || mixed ? 1 : 0,
        fail,
      );
      const text = await page.locator("body").innerText();
      assert(!text.includes("Viewport unavailable"));
      if (name === "cylinder" || fail)
        assert(
          !text.includes("Occurrence ID:\nNone"),
          "Cylinder proxy selection should survive",
        );
      const shaders = packet.data.display.definitions
        .filter((d) => d.shader?.artifact)
        .map((d) => ({
          id: d.id,
          cir: d.cir.structuralIdentity,
          shader: d.shader.artifact.shaderId,
          cacheHit: d.shader.cacheHit,
          generationMilliseconds: d.shader.generationMilliseconds,
        }));
      if (name === "cylinder") {
        await page
          .getByRole("button", { name: "Refresh Display Data", exact: true })
          .click();
        await page.waitForFunction(
          () =>
            document.body.innerText.includes("Action: success") &&
            !document.body.innerText.includes("Refresh display data…"),
        );
        await ready(page, 1, 0);
        assert.deepEqual(
          (await inspect(page)).gpu.modules,
          state.gpu.modules,
          "Rebuild must reuse GPU shader module",
        );
      }
      const depth =
        name === "scene" ? await mixedDepth(page, "cadmata") : undefined;
      assert.equal(errors.length, 0, errors.join("\n"));
      records.push({
        product: "cadmata",
        depth,
        name,
        state,
        afterInteraction,
        shaders,
        text,
      });
      console.log("PASS Cadmata", name, state.fields, state.meshSurfaces);
      await page.screenshot({ path: `${output}/cadmata-${name}.png` });
      await page.close();
    } finally {
      host.kill();
    }
    await writeFile(`${output}/audit.json`, JSON.stringify(records, null, 2));
  }
  const { page, errors } = await pageFor("http://127.0.0.1:4173/local");
  await page.locator("input[type=file]").waitFor({ state: "attached" });
  await page.waitForFunction(
    () => {
      const canvas = document.querySelector(
        "[data-display-host=three-telos] canvas",
      ) as HTMLCanvasElement;
      return JSON.parse(canvas?.dataset.telosDisplay ?? "{}").meshSurfaces > 0;
    },
    null,
    { timeout: 120000 },
  );
  for (const [name, source] of failureOnly
    ? []
    : [
        ...primitiveSources,
        ["house", "fixtures/Canonical/Scene/WarmModernHouse/house.firmament"],
      ]) {
    const extra =
      name === "scene"
        ? [resolve(dirname(source), "mixed.firmament")]
        : name === "house"
          ? (await readdir(dirname(source)))
              .filter(
                (f) => f.endsWith(".firmament") && f !== "house.firmament",
              )
              .map((f) => resolve(dirname(source), f))
          : [];
    const started = Date.now();
    await page
      .locator("input[type=file]")
      .setInputFiles([resolve(source), ...extra]);
    await page.waitForFunction(
      (name) => document.body.innerText.includes(name + ".firmament"),
      name,
    );
    await page
      .locator(".statusbar .status-ready")
      .filter({ hasText: "READY" })
      .waitFor({ timeout: 120000 });
    const mixed = name === "mixed" || name === "scene";
    let state;
    if (name === "house") {
      await page.waitForFunction(
        () => document.body.innerText.includes("94 DEFS · 254 OCC"),
        null,
        { timeout: 120000 },
      );
      await page
        .getByRole("combobox", { name: "Scene camera" })
        .selectOption("Hero");
      await page.waitForTimeout(600);
      state = await inspect(page);
      assert.equal(state.fields, 0);
      assert.equal(state.meshSurfaces, 161);
    } else state = await ready(page, mixed ? 2 : 1, mixed ? 1 : 0);
    const loadMilliseconds = Date.now() - started;
    await interact(page);
    if (name === "cylinder") {
      const id = state.definitions[0].shaderId;
      await page
        .getByRole("button", { name: "Edit Source", exact: true })
        .click();
      await page.waitForTimeout(100);
      // Edit only the radius digit through the normal editor. Whole-document
      // key replay also invokes Monaco suggestions/autoindent and is not a paste.
      await page.keyboard.press("Control+Home");
      for (let line = 0; line < 4; line++)
        await page.keyboard.press("ArrowDown");
      await page.keyboard.press("End");
      await page.keyboard.press("ArrowLeft");
      await page.keyboard.press("ArrowLeft");
      await page.keyboard.press("Backspace");
      await page.keyboard.type("4", { delay: 30 });
      await page.waitForFunction(() =>
        document
          .querySelector(".statusbar")
          ?.textContent?.includes("MODEL OUT OF DATE"),
      );
      await page.getByRole("button", { name: /^Build$/i }).click();
      await page
        .waitForFunction(
          (id) => {
            const c = document.querySelector(
              "[data-display-host=three-telos] canvas",
            ) as HTMLCanvasElement;
            const d = JSON.parse(c?.dataset.telosDisplay ?? "{}");
            return d.fields === 1 && d.definitions?.[0]?.shaderId !== id;
          },
          id,
          { timeout: 30000 },
        )
        .catch(async (e) => {
          console.error(
            await inspect(page),
            await page.locator("body").innerText(),
          );
          await page.screenshot({ path: `${output}/geometry-edit-failed.png` });
          throw e;
        });
      state = { ...state, geometryEdit: await ready(page, 1, 0) };
      assert.notEqual(state.geometryEdit.definitions[0].shaderId, id);
      assert.equal(
        state.geometryEdit.occurrences[0].identity.occurrenceId,
        state.occurrences[0].identity.occurrenceId,
      );
      assert.equal(
        state.geometryEdit.modules,
        state.modules,
        "Obsolete program references should be released",
      );
    }
    if (name === "mixed") {
      const sourceText = await readFile(source, "utf8"),
        id = state.definitions.find((d) => d.shaderId).shaderId;
      await replaceDigit(page, sourceText, ".55", "1", 1);
      const appearanceEdit = await ready(page, 2, 1);
      assert.equal(
        appearanceEdit.definitions.find((d) => d.shaderId).shaderId,
        id,
      );
      assert.equal(
        appearanceEdit.occurrences.find((o) => o.material.metallic === 0.7)
          .material.baseColor[0],
        0.15,
      );
      await replaceDigit(
        page,
        sourceText.replace(".55", ".15"),
        "50,0,0,1",
        "7",
      );
      const transformEdit = await ready(page, 2, 1);
      assert.equal(
        transformEdit.definitions.find((d) => d.shaderId).shaderId,
        id,
      );
      assert.equal(transformEdit.modules, state.modules);
      assert.equal(transformEdit.pipelines, appearanceEdit.pipelines);
      state = { ...state, appearanceEdit, transformEdit };
    }
    const depth =
      name === "scene" ? await mixedDepth(page, "helios") : undefined;
    assert.equal(errors.length, 0, errors.join("\n"));
    records.push({
      product: "helios",
      depth,
      name,
      loadMilliseconds,
      state,
      text: (await page.locator("body").innerText()).slice(-10000),
    });
    console.log(
      "PASS Helios",
      name,
      state.fields,
      state.meshSurfaces,
      loadMilliseconds,
    );
    await page.screenshot({ path: `${output}/helios-${name}.png` });
    await writeFile(`${output}/audit.json`, JSON.stringify(records, null, 2));
  }
  await page.close();
  const negative = await pageFor("http://127.0.0.1:4173/local", true);
  await negative.page
    .locator(".statusbar .status-ready")
    .filter({ hasText: "READY" })
    .waitFor({ timeout: 120000 });
  await negative.page
    .locator("input[type=file]")
    .setInputFiles(resolve("fixtures/Canonical/Basics/cylinder.firmament"));
  const failureState = await ready(negative.page, 0, 1, true);
  await interact(negative.page);
  assert(
    !(await negative.page.locator(".status-selection").innerText()).includes(
      "NO SELECTION",
    ),
  );
  assert.equal(negative.errors.length, 0, negative.errors.join("\n"));
  records.push({
    product: "helios",
    name: "failure",
    state: failureState,
    text: await negative.page.locator("body").innerText(),
  });
  await negative.page.screenshot({ path: `${output}/helios-failure.png` });
  await negative.page.close();
} finally {
  await writeFile(`${output}/audit.json`, JSON.stringify(records, null, 2));
  await browser.close();
}
console.log(`PASS: ${records.length} normal product witnesses`);
