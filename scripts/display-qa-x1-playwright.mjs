import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const repoRoot = path.resolve(__dirname, '..');
const artifactRoot = path.join(repoRoot, 'artifacts', 'display-qa-x1');
const playwrightModuleSpecifier = process.env.AETHERIS_PLAYWRIGHT_MODULE ?? 'playwright';

const clientUrl = process.env.AETHERIS_CLIENT_URL ?? 'https://127.0.0.1:5173/';
const edgeExecutablePath = process.env.AETHERIS_EDGE_PATH
  ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';

const cases = [
  {
    id: 'ftc06',
    label: 'FTC-06',
    fixturePath: path.join(repoRoot, 'testdata', 'step242', 'nist', 'FTC', 'nist_ftc_06_asme1_ap242-e2.stp'),
    expectedImportStatus: 'Import complete.',
  },
  {
    id: 'ftc07',
    label: 'FTC-07',
    fixturePath: path.join(repoRoot, 'testdata', 'step242', 'nist', 'FTC', 'nist_ftc_07_asme1_ap242-e2.stp'),
    expectedImportStatus: 'Import complete. View materialization failed.',
  },
];

function ensureEdgeExists() {
  return fs.access(edgeExecutablePath);
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
  const responseLog = new Map();

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

    if (!url.includes('/import/step') && !url.includes('/display/prepare')) {
      return;
    }

    let bodyText;
    try {
      bodyText = await response.text();
    } catch (error) {
      bodyText = `<<failed to read response body: ${error}>>`;
    }

    responseLog.set(url, {
      url,
      status: response.status(),
      ok: response.ok(),
      bodyText,
    });
  });

  const summary = [];

  try {
    await page.goto(clientUrl, { waitUntil: 'load' });
    await page.getByRole('status').waitFor({ state: 'visible', timeout: 30000 });

    for (const testCase of cases) {
      await page.getByRole('button', { name: 'New Document' }).click();
      await page.getByText('Ready. Select a file to import.').waitFor({ state: 'visible', timeout: 30000 });

      const fileInput = page.getByTestId('step-import-file-input');
      await fileInput.setInputFiles(testCase.fixturePath);
      await page.getByRole('button', { name: 'Import STEP 242' }).click();
      await page.getByText(testCase.expectedImportStatus, { exact: true }).waitFor({ state: 'visible', timeout: 30000 });

      const viewportFrame = page.locator('.viewport-frame');
      const inspector = page.locator('.audit-panel');

      const displayLane = (await page.locator('.audit-panel p').filter({ hasText: 'Display lane:' }).innerText()).replace('Display lane:', '').trim();
      const renderPath = (await page.locator('.audit-panel p').filter({ hasText: 'Render path:' }).innerText()).replace('Render path:', '').trim();
      const analyticFaces = (await page.locator('.audit-panel p').filter({ hasText: 'Analytic faces:' }).innerText()).replace('Analytic faces:', '').trim();
      const fallbackFaces = (await page.locator('.audit-panel p').filter({ hasText: 'Fallback faces:' }).innerText()).replace('Fallback faces:', '').trim();
      const faceCount = (await page.locator('.audit-panel p').filter({ hasText: 'Face count:' }).innerText()).replace('Face count:', '').trim();
      const edgeCount = (await page.locator('.audit-panel p').filter({ hasText: 'Edge count:' }).innerText()).replace('Edge count:', '').trim();

      const importedDisplayPath = path.join(artifactRoot, `${testCase.id}-imported-display.png`);
      const statusDisplayPath = path.join(artifactRoot, `${testCase.id}-display-status.png`);
      await viewportFrame.screenshot({ path: importedDisplayPath });
      await inspector.screenshot({ path: statusDisplayPath });

      const importStatusText = await page.locator('.import-status-box').innerText();
      const responseEntries = [...responseLog.values()];
      const importResponse = responseEntries.filter((entry) => entry.url.includes('/import/step')).at(-1) ?? null;
      const displayPrepareResponse = responseEntries.filter((entry) => entry.url.includes('/display/prepare')).at(-1) ?? null;

      if (displayPrepareResponse) {
        await fs.writeFile(path.join(artifactRoot, `${testCase.id}-display-prepare-ui-response.json`), `${displayPrepareResponse.bodyText}\n`, 'utf8');
      }

      summary.push({
        caseId: testCase.id,
        fixturePath: testCase.fixturePath,
        importStatusText,
        displayLane,
        renderPath,
        analyticFaces,
        fallbackFaces,
        faceCount,
        edgeCount,
        screenshotPaths: {
          viewport: importedDisplayPath,
          status: statusDisplayPath,
        },
        importResponse: importResponse
          ? { url: importResponse.url, status: importResponse.status, ok: importResponse.ok }
          : null,
        displayPrepareResponse: displayPrepareResponse
          ? { url: displayPrepareResponse.url, status: displayPrepareResponse.status, ok: displayPrepareResponse.ok }
          : null,
      });
    }
  } finally {
    await writeJson(path.join(artifactRoot, 'playwright-summary.json'), summary);
    await writeJson(path.join(artifactRoot, 'playwright-console.json'), consoleEntries);
    await writeJson(path.join(artifactRoot, 'playwright-pageerrors.json'), pageErrors);
    await writeJson(path.join(artifactRoot, 'playwright-requestfailures.json'), requestFailures);
    await browser.close();
  }

  console.log(JSON.stringify(summary, null, 2));
}

await run();
