import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { writeFile } from "node:fs/promises";
const { chromium } = await import(pathToFileURL(resolve(process.argv[2])).href);
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
try {
  const page = await browser.newPage({
    viewport: { width: 1100, height: 900 },
  });
  const errors: string[] = [];
  page.on("pageerror", (error: Error) => errors.push(String(error)));
  await page.goto("http://127.0.0.1:4173/tests/three-telos-witness.html");
  await page.waitForFunction(
    () =>
      (
        window as unknown as {
          heliosTelosQualification?: { compiled: boolean };
        }
      ).heliosTelosQualification?.compiled,
    null,
    { timeout: 120000 },
  );
  await page.locator("[data-display-host=three-telos] canvas").waitFor();
  await page.getByRole("button", { name: "Top", exact: true }).click();
  await page.waitForTimeout(300);
  const canvas = page.locator("[data-display-host=three-telos] canvas");
  const bounds = await canvas.boundingBox();
  if (!bounds) throw new Error("no canvas");
  await canvas.click({
    position: { x: bounds.width / 2, y: bounds.height / 2 },
  });
  await page.waitForFunction(() =>
    Boolean(
      (
        window as unknown as {
          heliosTelosQualification: { selected?: unknown };
        }
      ).heliosTelosQualification.selected,
    ),
  );
  const before = await page.evaluate(
    () =>
      (window as unknown as { heliosTelosQualification: unknown })
        .heliosTelosQualification,
  );
  if (!JSON.stringify(before).includes("face(+Z)"))
    throw new Error(
      "canonical top face selection failed: " + JSON.stringify(before),
    );
  const beforeNavigation = await canvas.screenshot();
  await page.mouse.move(bounds.x + bounds.width / 2, bounds.y + bounds.height / 2);
  await page.mouse.down();
  await page.mouse.move(bounds.x + bounds.width / 2 + 90, bounds.y + bounds.height / 2 + 35, { steps: 8 });
  await page.mouse.up();
  await page.waitForTimeout(150);
  const afterOrbit = await canvas.screenshot();
  if (beforeNavigation.equals(afterOrbit)) throw new Error('Helios orbit did not redraw');
  await page.mouse.wheel(0, -250);
  await page.waitForTimeout(150);
  if (afterOrbit.equals(await canvas.screenshot())) throw new Error('Helios zoom did not redraw');
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await page.waitForFunction(
    () =>
      (window as unknown as { heliosTelosQualification: { revision: number } })
        .heliosTelosQualification.revision > 0,
  );
  await page.getByRole("button", { name: "Top", exact: true }).click();
  await page.waitForTimeout(300);
  await canvas.click({
    position: { x: bounds.width / 2, y: bounds.height / 2 },
  });
  const after = await page.evaluate(
    () =>
      (window as unknown as { heliosTelosQualification: unknown })
        .heliosTelosQualification,
  );
  const alerts = await page.getByRole("alert").allTextContents();
  if (errors.length || alerts.length)
    throw new Error(JSON.stringify({ errors, alerts }));
  await page.screenshot({
    path: "artifacts/local/three-telos/helios-wrapper.png",
  });
  const report = { status: "PASS", before, after, errors, alerts };
  await writeFile(
    "artifacts/local/three-telos/helios-wrapper.json",
    JSON.stringify(report, null, 2),
  );
  console.log(JSON.stringify(report, null, 2));
  if (process.argv[3]) {
    const fixture = resolve("fixtures/three-telos/cadmata.html").replaceAll(
      "\\",
      "/",
    );
    await page.goto(process.argv[3] + "/@fs/" + fixture);
    await page.locator("[data-display-host=three-telos] canvas").waitFor();
    await page.waitForTimeout(300);
    const cadmataCanvas = page.locator(
        "[data-display-host=three-telos] canvas",
      ),
      rect = await cadmataCanvas.boundingBox();
    if (!rect) throw new Error("Cadmata canvas missing");
    await cadmataCanvas.click({
      position: { x: rect.width / 2, y: rect.height / 2 },
    });
    const ray = await page.evaluate(
      () => (window as unknown as { cadmataTelosRay: unknown }).cadmataTelosRay,
    );
    if (!ray) throw new Error("Cadmata canonical pick ray missing");
    await page.getByRole("button", { name: "Select", exact: true }).click();
    await page.waitForTimeout(300);
    const cadmataAlerts = await page.getByRole("alert").allTextContents();
    if (cadmataAlerts.length || errors.length)
      throw new Error(JSON.stringify({ cadmataAlerts, errors }));
    await page.screenshot({
      path: "artifacts/local/three-telos/cadmata-wrapper.png",
    });
    const cadmataReport = {
      status: "PASS",
      ray,
      errors,
      alerts: cadmataAlerts,
      scope: "real wrapper, bounded DisplayScene fixture",
    };
    await writeFile(
      "artifacts/local/three-telos/cadmata-wrapper.json",
      JSON.stringify(cadmataReport, null, 2),
    );
    console.log(JSON.stringify(cadmataReport, null, 2));
  }
} finally {
  await browser.close();
}
