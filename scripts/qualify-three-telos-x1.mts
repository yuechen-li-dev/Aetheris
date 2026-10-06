import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { mkdir, writeFile } from "node:fs/promises";
const { chromium } = await import(pathToFileURL(resolve(process.argv[2])).href);
const output = "artifacts/local/three-telos/x1";
await mkdir(output, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
try {
  const page = await browser.newPage({
    viewport: { width: 1600, height: 1100 },
    deviceScaleFactor: Number(process.argv[3] ?? 1),
  });
  const errors: string[] = [];
  let captureCanonical = true;
  page.on("pageerror", (error: Error) => errors.push(String(error)));
  page.on(
    "response",
    async (response: { url(): string; json(): Promise<unknown> }) => {
      if (response.url().endsWith("/import/step"))
        await writeFile(
          output + "/ctc03-import.json",
          JSON.stringify(await response.json(), null, 2),
        );
      if (captureCanonical && response.url().endsWith("/display/prepare"))
        await writeFile(
          output + "/ctc03-display.json",
          JSON.stringify(await response.json(), null, 2),
        );
    },
  );
  await page.goto("http://127.0.0.1:5173");
  await page.getByRole("tab", { name: "STEP 242 Viewer", exact: true }).click();
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
  await page
    .getByRole("button", { name: "Inspect Datum A", exact: true })
    .waitFor({ timeout: 120000 });
  await page.screenshot({ path: output + "/default.png" });
  const datum = page.getByRole("button", {
    name: "Inspect Datum A",
    exact: true,
  });
  await datum.click();
  await page.waitForFunction(() =>
    document
      .querySelector('[aria-label="Inspect Datum A"]')
      ?.classList.contains("is-selected"),
  );
  await page.screenshot({ path: output + "/selected.png" });
  const before = await datum.boundingBox();
  await page.mouse.move(
    before.x + before.width / 2,
    before.y + before.height / 2,
  );
  await page.mouse.down();
  await page.mouse.move(
    before.x + before.width / 2 + 74,
    before.y + before.height / 2 - 37,
    { steps: 12 },
  );
  await page.mouse.up();
  await page.waitForTimeout(200);
  const after = await datum.boundingBox();
  if (
    Math.abs(after.x - before.x - 74) > 2 ||
    Math.abs(after.y - before.y + 37) > 2
  )
    throw new Error("PMI drag displacement failed");
  await page.screenshot({ path: output + "/dragged.png" });
  await page.getByRole("button", { name: "DIM", exact: true }).click();
  if ((await page.locator(".pmi-callout--dimensions").count()) !== 13)
    throw new Error("13 dimension inventory lost");
  await page.getByRole("button", { name: "NOTES", exact: true }).click();
  if (
    (await page.locator(".pmi-callout--engineeringAnnotations").count()) !== 8
  )
    throw new Error("8 note inventory lost");
  await page.getByRole("button", { name: "DATUM", exact: true }).click();
  await page.getByRole("button", { name: "GD&T", exact: true }).click();
  await page.getByRole("button", { name: "DIM", exact: true }).click();
  if ((await page.locator("[data-pmi-id]").count()) !== 8)
    throw new Error("category filtering lost");
  await page.waitForTimeout(200);
  await page.screenshot({ path: output + "/filtered.png" });
  const canvas = page.locator("[data-display-host=three-telos] canvas");
  const rect = await canvas.boundingBox();
  await page.mouse.move(rect.x + rect.width * 0.7, rect.y + rect.height * 0.3);
  await page.mouse.down();
  await page.mouse.move(
    rect.x + rect.width * 0.7 + 90,
    rect.y + rect.height * 0.3 + 30,
    { steps: 8 },
  );
  await page.mouse.up();
  await page.mouse.wheel(0, -250);
  await page.waitForTimeout(200);
  await page.screenshot({
    path: output + "/zoom-dpr" + (process.argv[3] ?? "1") + ".png",
  });
  await page
    .getByRole("button", { name: "Refresh Display Data", exact: true })
    .click();
  await page.waitForTimeout(500);
  if (await page.locator("[data-display-host=transitional-webgl]").count())
    throw new Error("authoring selected legacy");
  const webglRequests = await page.evaluate(() =>
    performance
      .getEntriesByType("resource")
      .map((entry) => entry.name)
      .filter((name) => /react-three|LegacyAetheris/.test(name)),
  );
  if (webglRequests.length)
    throw new Error("normal authoring loaded fallback graphics");
  captureCanonical = false;
  await page.getByRole("button", { name: "New Document", exact: true }).click();
  await page.getByRole("tab", { name: "Modeling Demo (Experimental)" }).click();
  await page.getByLabel("Width", { exact: true }).fill("40");
  await page.getByLabel("Height", { exact: true }).fill("30");
  await page.getByLabel("Depth", { exact: true }).fill("8");
  await page.getByRole("button", { name: "Create Box", exact: true }).click();
  await page
    .getByRole("button", { name: "Apply Translation", exact: true })
    .waitFor();

  await page.getByLabel("X", { exact: true }).fill("5");
  await page
    .getByRole("button", { name: "Apply Translation", exact: true })
    .click();
  await page.getByRole("button", { name: /t=\[5.00, 0.00, 0.00\]/ }).waitFor();
  const picked = page.waitForResponse((response: { url(): string }) => response.url().endsWith('/pick'));
  await canvas.click({ position: { x: rect.width / 2, y: rect.height / 2 } });
  const hit = (await (await picked).json()).data.hits[0];
  if (!hit || hit.entityKind !== 'Face') throw new Error('normal modeling pick failed');

  await page
    .getByRole("button", { name: "Refresh Display Data", exact: true })
    .click();
  await page.waitForTimeout(250);
  await page.screenshot({
    path: output + "/normal-authoring-dpr" + (process.argv[3] ?? "1") + ".png",
  });
  const report = {
    status: "PASS",
    scope:
      "actual Cadmata application; ordinary canonical CADMATA-PMI-X1 STEP import",
    dpr: Number(process.argv[3] ?? 1),
    normalAuthoring:
      "actual Create Box / Apply Translation / face selection / Refresh Display Data",
    modelingHit: { entityKind: hit.entityKind, faceId: hit.faceId },
    defaultPmi: 8,
    dimensions: 13,
    notes: 8,
    drag: { before, after },
    errors,
    webglRequests,
  };
  if (errors.length || (await page.getByRole("alert").count()))
    throw new Error(JSON.stringify(errors));
  await writeFile(
    output + "/application-dpr" + (process.argv[3] ?? "1") + ".json",
    JSON.stringify(report, null, 2),
  );
  console.log(JSON.stringify(report, null, 2));
} finally {
  await browser.close();
}
