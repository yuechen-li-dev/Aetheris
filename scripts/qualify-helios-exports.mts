import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { mkdir, readFile, writeFile } from 'node:fs/promises';

const { chromium } = await import(pathToFileURL(resolve(process.argv[2])).href);
const output = resolve('artifacts/local/helios-exports'); await mkdir(output, { recursive: true });
const browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 960 }, acceptDownloads: true });
  page.setDefaultTimeout(180000);
  const errors: string[] = []; page.on('pageerror', (error: Error) => errors.push(error.message));
  await page.goto('http://127.0.0.1:4173/local'); await page.locator('.status-ready').waitFor();
  console.log('Helios initialized');
  const input = page.locator('input[type="file"]');
  const trigger = page.getByRole('button', { name: 'Export ▾', exact: true });
  const ready = async () => { await page.waitForFunction(() => !document.querySelector<HTMLButtonElement>('.export-picker > button')?.disabled); };
  const download = async (name: string) => {
    const pending = page.waitForEvent('download');
    await page.getByRole('button', { name: /^Download/ }).click();
    const file = await pending; const failure = await file.failure(); if (failure) throw new Error(failure);
    if (file.suggestedFilename() !== name) throw new Error(`Unexpected filename ${file.suggestedFilename()} instead of ${name}`);
    await file.saveAs(resolve(output, name));
  };
  await input.setInputFiles(resolve('fixtures/Regression/DrawingGeneration/inspection-plate.firmament')); await ready();
  await trigger.click();
  const options = page.getByRole('checkbox');
  if (await options.count() !== 8) throw new Error('Expected eight supported export formats');
  for (const option of await options.all()) await option.check();
  await page.screenshot({ path: resolve(output, 'picker.png') });
  await download('inspection-plate-exports.zip'); console.log('All eight part formats downloaded');
  // A single format is a direct file, not a ZIP; reuse the current accepted model.
  await trigger.click();
  for (const option of await options.all()) await option.uncheck();
  await page.getByRole('checkbox', { name: /STEP AP242/ }).check();
  await download('inspection-plate.step'); console.log('Direct STEP download passed');
  const validPart = await readFile(resolve('fixtures/Regression/DrawingGeneration/inspection-plate.firmament'), 'utf8');
  await page.evaluate(async (source: string) => {
    const { WebSdkCadRuntime } = await import('/src/sdk/CadRuntime.ts');
    const runtime = new WebSdkCadRuntime();
    try {
      const built = await runtime.open(source, 'direct.firmament');
      if (!built.model) throw new Error('Direct SDK model did not build');
      const bytes = await runtime.exportSTEP();
      if (!new TextDecoder().decode(bytes).startsWith('ISO-10303-21;')) throw new Error('Legacy STEP method lost its contract');
      const failed = await runtime.setSource(source.replace('diameter: 4mm', 'diametr: 4mm'), 'direct.firmament');
      if (failed.success) throw new Error('Invalid SDK revision was accepted');
      try { await runtime.exportArtifacts(['step']); } catch (error) {
        if ((error as { code?: string }).code === 'export-stale-revision') return;
        throw error;
      }
      throw new Error('SDK exported retained geometry after a failed rebuild');
    } finally { await runtime.dispose(); }
  }, validPart);
  console.log('SDK STEP compatibility and failed-rebuild export guard passed');

  await input.setInputFiles(resolve('fixtures/Canonical/WebSdk/shared-block-assembly.firmament')); await ready();
  await trigger.click();
  if (await page.getByRole('checkbox', { name: /STL/ }).isEnabled()) throw new Error('Assembly STL must explain its single-part boundary');
  await page.getByRole('checkbox', { name: /glTF/ }).check(); await page.getByRole('checkbox', { name: /OpenUSD/ }).check();
  await download('shared-block-assembly-exports.zip'); console.log('Assembly STEP/GLB/USDA downloaded');

  await input.setInputFiles(['scene.firmament', 'mixed.firmament'].map(name => resolve('fixtures/Canonical/DisplayProjection', name))); await ready();
  await trigger.click();
  if (await page.getByRole('checkbox', { name: /STEP AP242/ }).isEnabled()) throw new Error('Scene STEP must be unavailable');
  await page.getByRole('checkbox', { name: /glTF/ }).check(); await page.getByRole('checkbox', { name: /OpenUSD/ }).check();
  await download('scene-exports.zip'); console.log('Scene GLB/USDA downloaded');

  await input.setInputFiles(resolve('fixtures/Regression/Visualization/concept-structs.firmament')); await ready();
  await trigger.click(); for (const option of await options.all()) if (await option.isEnabled()) await option.uncheck();
  await page.getByRole('checkbox', { name: /PDF drawing/ }).check();
  let downloads = 0; page.on('download', () => downloads++);
  await page.getByRole('button', { name: /^Download/ }).click();
  await page.getByText(/drawing-definition-incomplete/).first().waitFor(); await ready();
  if (downloads) throw new Error('Incomplete drawing unexpectedly downloaded');
  await page.screenshot({ path: resolve(output, 'drawing-rejection.png') });
  await page.keyboard.press('Escape');
  const source = await readFile(resolve('fixtures/Regression/Visualization/concept-structs.firmament'), 'utf8');
  await input.setInputFiles({ name: 'concept-structs.firmament', mimeType: 'text/plain', buffer: Buffer.from(source.replace('distance: 1.5mm', 'distnace: 1.5mm')) });
  await page.waitForFunction(() => document.querySelector('.viewport-caption')?.textContent?.includes('Last good model'));
  await ready(); await trigger.click(); for (const option of await options.all()) if (await option.isEnabled()) await option.uncheck();
  await page.getByRole('checkbox', { name: /STEP AP242/ }).check();
  await page.getByRole('button', { name: /^Download/ }).click(); await ready();
  if (downloads) throw new Error('Invalid draft downloaded retained geometry');
  if (errors.length) throw new Error(JSON.stringify(errors));
  await writeFile(resolve(output, 'browser.json'), JSON.stringify({ success: true, partFormats: 8,
    assemblyFormats: 3, sceneFormats: 2, unsupportedDrawingRejected: true, invalidDraftExportRejected: true, errors }, null, 2));
  console.log('Real Helios export qualification passed');
} catch (error) { console.error(error); throw error; }
finally { await browser.close(); }
