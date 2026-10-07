import { resolve, join } from "node:path";
import { pathToFileURL } from "node:url";
import { mkdir, writeFile } from "node:fs/promises";
const [modulePath, output] = process.argv.slice(2).map(path => resolve(path));
const { chromium } = await import(pathToFileURL(modulePath).href);
const browser = await chromium.connectOverCDP("http://127.0.0.1:9237");
try {
  await mkdir(output, { recursive: true });
  const page = browser.contexts()[0].pages()[0];
  await page.screenshot({ path: join(output, "packaged-diagnostic.png") });
  await writeFile(join(output, "packaged-page.txt"), await page.locator("body").innerText());
  const session = await browser.newBrowserCDPSession();
  await writeFile(join(output, "gpu-info.json"), JSON.stringify(await session.send("SystemInfo.getInfo"), null, 2));
} finally { await browser.close(); }
