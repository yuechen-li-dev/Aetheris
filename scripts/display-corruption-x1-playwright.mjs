import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, '..');
const artifactRoot = path.join(repoRoot, 'artifacts', 'display-corruption-x1');
const playwrightModuleSpecifier = process.env.AETHERIS_PLAYWRIGHT_MODULE ?? 'playwright';

const clientUrl = process.env.AETHERIS_CLIENT_URL ?? 'https://127.0.0.1:4173/';
const edgeExecutablePath = process.env.AETHERIS_EDGE_PATH
  ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const fixturePath = path.join(repoRoot, 'testdata', 'step242', 'nist', 'CTC', 'nist_ctc_01_asme1_ap242-e1.stp');

function responseArtifactName(url) {
  if (url.includes('/import/step')) {
    return 'ctc01-import.json';
  }

  if (url.includes('/display/prepare')) {
    return 'ctc01-display-prepare.json';
  }

  return null;
}

async function ensureEdgeExists() {
  await fs.access(edgeExecutablePath);
}

async function writeJson(targetPath, value) {
  await fs.writeFile(targetPath, `${JSON.stringify(value, null, 2)}\n`, 'utf8');
}

async function loadChromium() {
  const specifier = path.isAbsolute(playwrightModuleSpecifier)
    ? pathToFileURL(playwrightModuleSpecifier).href
    : playwrightModuleSpecifier;
  const module = await import(specifier);
  return module.chromium;
}

async function waitForReady(page) {
  await page.goto(clientUrl, { waitUntil: 'load' });
  await page.getByRole('status').waitFor({ state: 'visible', timeout: 30000 });
  await page.getByText('Document: Ready', { exact: true }).waitFor({ state: 'visible', timeout: 30000 });
  await page.getByText('Ready. Select a file to import.', { exact: true }).waitFor({ state: 'visible', timeout: 30000 });
}

async function waitForImportComplete(page) {
  const statusBox = page.locator('.import-status-box');
  await statusBox.waitFor({ state: 'visible', timeout: 30000 });
  await page.waitForFunction(() => {
    const element = document.querySelector('.import-status-box');
    return Boolean(element && !element.textContent?.includes('Importing STEP'));
  }, null, { timeout: 30000 });
  return statusBox.innerText();
}

async function dragViewport(viewportFrame, from, to) {
  const box = await viewportFrame.boundingBox();
  if (!box) {
    return;
  }

  const start = {
    x: box.x + (box.width * from.x),
    y: box.y + (box.height * from.y),
  };
  const end = {
    x: box.x + (box.width * to.x),
    y: box.y + (box.height * to.y),
  };

  await viewportFrame.page().mouse.move(start.x, start.y);
  await viewportFrame.page().mouse.down();
  await viewportFrame.page().mouse.move(end.x, end.y, { steps: 20 });
  await viewportFrame.page().mouse.up();
  await viewportFrame.page().waitForTimeout(500);
}

async function run() {
  await ensureEdgeExists();
  await fs.mkdir(artifactRoot, { recursive: true });

  const chromium = await loadChromium();
  const browser = await chromium.launch({
    executablePath: edgeExecutablePath,
    headless: true,
  });

  const context = await browser.newContext({
    ignoreHTTPSErrors: true,
    viewport: { width: 1440, height: 960 },
  });

  const page = await context.newPage();
  const consoleEntries = [];
  const pageErrors = [];
  const requestFailures = [];
  const responseEntries = [];

  page.on('console', (message) => {
    consoleEntries.push({
      type: message.type(),
      text: message.text(),
      location: message.location(),
    });
  });

  page.on('pageerror', (error) => {
    pageErrors.push({
      name: error.name,
      message: error.message,
      stack: error.stack ?? null,
    });
  });

  page.on('requestfailed', (request) => {
    requestFailures.push({
      url: request.url(),
      method: request.method(),
      failure: request.failure(),
    });
  });

  page.on('response', async (response) => {
    const url = response.url();
    if (!url.includes('/api/v1/documents/')) {
      return;
    }

    const artifactName = responseArtifactName(url);
    if (!artifactName) {
      return;
    }

    let bodyText;
    try {
      bodyText = await response.text();
    } catch (error) {
      bodyText = `<<failed to read response body: ${error}>>`;
    }

    responseEntries.push({
      url,
      status: response.status(),
      ok: response.ok(),
      artifactName,
      bodyText,
    });
  });

  try {
    await waitForReady(page);
    await page.getByRole('button', { name: 'New Document' }).click();
    await page.getByText('Ready. Select a file to import.').waitFor({ state: 'visible', timeout: 30000 });

    const fileInput = page.getByTestId('step-import-file-input');
    await fileInput.setInputFiles(fixturePath);
    await page.getByRole('button', { name: 'Import STEP 242' }).click();

    const importStatusText = await waitForImportComplete(page);
    const viewportFrame = page.locator('.viewport-frame');
    const inspector = page.locator('.audit-panel');

    await viewportFrame.screenshot({ path: path.join(artifactRoot, 'ctc01-aetheris-default.png') });
    await inspector.screenshot({ path: path.join(artifactRoot, 'ctc01-aetheris-status.png') });

    await dragViewport(viewportFrame, { x: 0.7, y: 0.55 }, { x: 0.3, y: 0.35 });
    await viewportFrame.screenshot({ path: path.join(artifactRoot, 'ctc01-aetheris-angle-1.png') });

    await dragViewport(viewportFrame, { x: 0.35, y: 0.35 }, { x: 0.6, y: 0.75 });
    await viewportFrame.screenshot({ path: path.join(artifactRoot, 'ctc01-aetheris-angle-2.png') });

    const summary = {
      fixturePath,
      clientUrl,
      importStatusText,
      displayLane: await page.locator('.audit-panel p').filter({ hasText: 'Display lane:' }).innerText(),
      displayStatus: await page.locator('.audit-panel p').filter({ hasText: 'Display status:' }).innerText(),
      renderPath: await page.locator('.audit-panel p').filter({ hasText: 'Render path:' }).innerText(),
      analyticFaces: await page.locator('.audit-panel p').filter({ hasText: 'Analytic faces:' }).innerText(),
      fallbackFaces: await page.locator('.audit-panel p').filter({ hasText: 'Fallback faces:' }).innerText(),
      faceCount: await page.locator('.audit-panel p').filter({ hasText: 'Face count:' }).innerText(),
      edgeCount: await page.locator('.audit-panel p').filter({ hasText: 'Edge count:' }).innerText(),
      screenshots: {
        default: path.join(artifactRoot, 'ctc01-aetheris-default.png'),
        status: path.join(artifactRoot, 'ctc01-aetheris-status.png'),
        angle1: path.join(artifactRoot, 'ctc01-aetheris-angle-1.png'),
        angle2: path.join(artifactRoot, 'ctc01-aetheris-angle-2.png'),
      },
      responseArtifacts: responseEntries.map((entry) => ({
        url: entry.url,
        status: entry.status,
        ok: entry.ok,
        artifact: path.join(artifactRoot, entry.artifactName),
      })),
    };

    for (const entry of responseEntries) {
      await fs.writeFile(path.join(artifactRoot, entry.artifactName), `${entry.bodyText}\n`, 'utf8');
    }

    await writeJson(path.join(artifactRoot, 'ctc01-playwright-summary.json'), summary);
    await writeJson(path.join(artifactRoot, 'ctc01-playwright-console.json'), consoleEntries);
    await writeJson(path.join(artifactRoot, 'ctc01-playwright-pageerrors.json'), pageErrors);
    await writeJson(path.join(artifactRoot, 'ctc01-playwright-requestfailures.json'), requestFailures);

    console.log(JSON.stringify(summary, null, 2));
  } finally {
    await browser.close();
  }
}

await run();
