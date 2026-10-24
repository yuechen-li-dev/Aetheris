// Real browser + CLI/server regression. No test server or substitute PDF renderer.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const output = path.resolve(root, process.env.AETHERIS_DRAWING_EVIDENCE ?? 'artifacts/local/drawing-browser');
const cli = path.join(root, 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll');
const { chromium } = await import(process.env.AETHERIS_PLAYWRIGHT_MODULE ?? 'playwright');
const browser = await chromium.launch(process.env.AETHERIS_BROWSER_PATH ? { executablePath: process.env.AETHERIS_BROWSER_PATH } : {});
const port = Number(process.env.AETHERIS_DRAWING_PORT ?? 4188);
const url = `http://127.0.0.1:${port}`;
await fs.mkdir(output, { recursive: true });

async function command(args) {
  const child = spawn('dotnet', [cli, ...args], { cwd: root, windowsHide: true });
  let error = ''; child.stderr.on('data', b => error += b);
  await new Promise((resolve, reject) => { child.on('error', reject); child.on('exit', code => code === 0 ? resolve() : reject(new Error(error))); });
}
async function ready(server) {
  for (let n = 0; n < 100; n++) {
    if (server.exitCode !== null) throw new Error(`Drawing server exited ${server.exitCode}`);
    try { if ((await fetch(`${url}/api/project`)).ok) return; } catch {}
    await new Promise(r => setTimeout(r, 100));
  }
  throw new Error('Drawing server did not start');
}

const results = [];
try {
  for (const fixture of ['page-coordinates.pdf', 'synthetic-engineering-drawing.pdf']) {
    const notes = path.join(output, path.basename(fixture, '.pdf'));
    const source = path.join(root, 'fixtures/DrawingNotes', fixture);
    await command(['drawing', 'notes', 'create', source, '--out', notes]);
    const projectPath = path.join(notes, 'drawing-notes.json');
    const server = spawn('dotnet', [cli, 'drawing', 'notes', 'serve', projectPath, '--source', source, '--port', String(port)], { cwd: root, windowsHide: true });
    const serverLog = []; server.stderr.on('data', b => serverLog.push(b.toString()));
    const page = await browser.newPage({ viewport: { width: 1400, height: 950 }, acceptDownloads: true });
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    try {
      await ready(server);
      const fallback = await fetch(`${url}/pdfjs/wasm/jbig2_nowasm_fallback.js`);
      assert.ok(fallback.ok && fallback.headers.get('content-type')?.includes('javascript'), 'PDF.js fallback modules need JavaScript MIME types');
      await page.goto(url);
      await page.waitForFunction(() => document.querySelector('#status').textContent === 'Saved');
      const project = JSON.parse(await fs.readFile(projectPath, 'utf8'));
      for (const metadata of project.document.pages) {
        await page.locator(`[data-page="${metadata.number}"]`).click();
        await page.waitForFunction(([w, h]) => {
          const c = document.querySelector('#pageImage');
          return document.querySelector('#status').textContent === 'Saved' && c.width === Math.ceil(w * 2.5) && c.height === Math.ceil(h * 2.5);
        }, [metadata.widthPoints, metadata.heightPoints]);
        if (fixture === 'page-coordinates.pdf') {
          const pixels = await page.locator('#pageImage').evaluate(canvas => {
            const { data } = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height);
            let minX = canvas.width, minY = canvas.height, maxX = -1, maxY = -1;
            for (let y = 0; y < canvas.height; y++) for (let x = 0; x < canvas.width; x++) {
              const i = (y * canvas.width + x) * 4;
              if (data[i] > 220 && data[i + 1] < 30 && data[i + 2] < 30) { minX = Math.min(minX, x); minY = Math.min(minY, y); maxX = Math.max(maxX, x); maxY = Math.max(maxY, y); }
            }
            return [minX / 2.5, minY / 2.5, (maxX + 1) / 2.5, (maxY + 1) / 2.5];
          });
          const expected = [[40, 120, 120, 160], [40, 40, 80, 120], [280, 40, 360, 80], [120, 280, 160, 360]][metadata.number - 1];
          pixels.forEach((v, i) => assert.ok(Math.abs(v - expected[i]) < 1, `${metadata.number}: pixel bound ${pixels} != ${expected}`));
          results.push({ page: metadata.number, metadata, redBounds: pixels });
        }
      }
      await page.locator('[data-page="1"]').click();
      await page.waitForFunction(() => document.querySelector('#status').textContent === 'Saved');
      const m = project.document.pages[0], overlay = await page.locator('#overlay').boundingBox();
      const start = { x: overlay.x + 40 / m.widthPoints * overlay.width, y: overlay.y + 50 / m.heightPoints * overlay.height };
      const end = { x: overlay.x + 120 / m.widthPoints * overlay.width, y: overlay.y + 90 / m.heightPoints * overlay.height };
      await page.mouse.move(start.x, start.y); await page.mouse.down(); await page.mouse.move(end.x, end.y, { steps: 4 }); await page.mouse.up(); await page.keyboard.press('v');
      await page.waitForFunction(() => document.querySelector('#status').textContent === 'Saved');
      const saved = JSON.parse(await fs.readFile(projectPath, 'utf8'));
      const region = saved.regions.at(-1);
      assert.ok(Math.abs(region.bounds.x - 40) < 1 && Math.abs(region.bounds.y - 50) < 1);
      assert.ok(Math.abs(region.bounds.width - 80) < 1 && Math.abs(region.bounds.height - 40) < 1);
      const downloads = [];
      page.on('download', d => downloads.push(d));
      await page.locator('#crop').click();
      await page.waitForFunction(() => document.querySelector('#status').textContent.startsWith('Crop exported'));
      for (let n = 0; downloads.length < 2 && n < 100; n++) await new Promise(r => setTimeout(r, 50));
      assert.equal(downloads.length, 2);
      for (const d of downloads) await d.saveAs(path.join(notes, d.suggestedFilename()));
      const crop = JSON.parse(await fs.readFile(path.join(notes, downloads.find(d => d.suggestedFilename().endsWith('.json')).suggestedFilename()), 'utf8'));
      assert.equal(crop.documentSha256, project.document.sha256); assert.equal(crop.page, 1); assert.equal(crop.dpi, 300);
      assert.deepEqual(crop.bounds, region.bounds); assert.equal(crop.rendererVersion, 'PDF.js-6.4.299');
      await page.reload(); await page.waitForFunction(() => document.querySelector('#status').textContent === 'Saved');
      assert.equal(await page.locator('#overlay .box').count(), saved.regions.length + saved.annotations.length);
      await page.screenshot({ path: path.join(notes, 'browser.png') });
      assert.deepEqual(errors, []);
      results.push({ fixture, savedRegion: region, crop });
    } finally {
      await page.close();
      server.kill('SIGTERM');
      await new Promise(resolve => server.exitCode !== null ? resolve() : server.once('exit', resolve));
      await fs.writeFile(path.join(notes, 'server.log'), serverLog.join(''));
    }
  }
  await fs.writeFile(path.join(output, 'results.json'), JSON.stringify(results, null, 2));
  console.log('Drawing Notes browser qualification passed: real PDF rendering, rotations, inherited CropBox/UserUnit, pixel bounds, annotation save/reload and source-linked PNG crops.');
} finally { await browser.close(); }
