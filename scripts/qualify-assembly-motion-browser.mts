import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { resolve, extname, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// Bounded integration harness: real CLI oracle, public SDK, real WASM, headless
// Edge. The ephemeral static fixture server and browser close in finally.
const repo = resolve(fileURLToPath(new URL('..', import.meta.url)));
const modulePath = process.argv[2];
if (!modulePath) throw new Error('Pass an explicit Playwright module path.');
const { chromium } = await import(pathToFileURL(resolve(modulePath)).href);
const fixture = resolve(repo, 'fixtures/Canonical/AssemblyInterfaces/articulated-motion.firmament');
const source = await readFile(fixture, 'utf8');
const holeSource = await readFile(resolve(repo, 'fixtures/Regression/LanguageService/hole-variant.firmament'), 'utf8');
const emptyModifySource = await readFile(resolve(repo, 'fixtures/Regression/LanguageService/empty-modify.firmament'), 'utf8');
const documents = {
  'motion.firmament': source,
  'articulated-subassemblies.firmament': await readFile(resolve(repo, 'fixtures/Canonical/AssemblyInterfaces/articulated-subassemblies.firmament'), 'utf8'),
};
const times = [0, 1.25, .231, 1.749, 2, -.75, 3.25];
const expected = times.map(time => JSON.parse(execFileSync('dotnet', [resolve(repo, 'Aetheris.CLI/bin/Release/net10.0/aetheris.dll'),
  'asm', 'motion', fixture, '--clip', 'Cycle', '--time', String(time), '--json'], { cwd: repo, encoding: 'utf8', maxBuffer: 8 * 1024 * 1024 })));
const server = createServer(async (request, response) => {
  try {
    const url = new URL(request.url!, 'http://localhost');
    if (url.pathname === '/') { response.setHeader('Content-Type', 'text/html'); response.end('<!doctype html><title>Assembly Motion qualification</title>'); return; }
    const file = resolve(repo, '.' + decodeURIComponent(url.pathname));
    if (!file.startsWith(repo + sep)) throw new Error('outside repository');
    const bytes = await readFile(file);
    const types: Record<string, string> = { '.js': 'text/javascript', '.mjs': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
    response.setHeader('Content-Type', types[extname(file)] ?? 'application/octet-stream');
    response.setHeader('Cache-Control', 'no-store');
    response.end(bytes);
  } catch { response.statusCode = 404; response.end('not found'); }
});
await new Promise<void>(done => server.listen(0, '127.0.0.1', done));
const address = server.address();
if (!address || typeof address === 'string') throw new Error('no port');
let browser;
try {
  browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
  const page = await browser.newPage();
  await page.goto(`http://127.0.0.1:${address.port}/`);
  const result = await page.evaluate(async ({ source, documents, times, expected, holeSource, emptyModifySource }) => {
    const { Aetheris } = await import('/Aetheris.Web.Runtime/sdk/src/index.js');
    const cad = await Aetheris.create({ wasmUrl: new URL('/Aetheris.Web.Runtime/bin/Release/net10.0/wwwroot/', location.href) });
    try {
      const built = await cad.compile(source, { sourceName: 'motion.firmament', projectDocuments: documents });
      const model = built.model;
      if (!model) throw new Error(JSON.stringify(built.diagnostics));
      if (model.motions.length !== 1) throw new Error('Missing compiled clip');
      const definitions = model.mesh.definitions;
      let maximum = 0; let compared = 0;
      for (let index = 0; index < times.length; index++) {
        const pose = await model.sampleMotion('Cycle', times[index]);
        const native = new Map(expected[index].transforms.map((t: { id: string; matrix: number[] }) => [t.id, t.matrix]));
        for (const actual of pose.transforms) {
          const matrix = native.get(actual.id) as number[] | undefined;
          if (!matrix) throw new Error('Unknown occurrence ' + actual.id);
          actual.matrix.forEach((v: number, i: number) => { maximum = Math.max(maximum, Math.abs(v - matrix[i])); compared++; });
        }
        if (model.mesh.definitions !== definitions) throw new Error('Sampling rebuilt shared geometry');
      }
      if (maximum > 1e-9) throw new Error('Native/WASM pose mismatch ' + maximum);
      const endpoint = await model.sampleMotion('Cycle', 2, { preserveEndpoint: true });
      if (endpoint.state.Shoulder !== 720 || endpoint.state['Articulated.First.Elbow'] !== 360)
        throw new Error('Unwrapped final endpoint was lost');
      const revision = model.revision;
      const edited = await model.setSource(source.replace('repeat: Loop', 'repeat: Unknown'));
      if (edited.success) throw new Error('Invalid Motion admitted');
      const retained = await model.sampleMotion('Cycle', 1.25);
      if (retained.revision !== revision) throw new Error('Last-valid graph revision changed');
      await model.dispose();
      const schema = await cad.language.schema();
      const holeSchema = schema.constructs.find((c: {name: string}) => c.name === 'Hole');
      if (holeSchema?.fields.find((f: {name: string}) => f.name === 'CounterboreDepth')?.variants?.join(',') !== 'Counterbore')
        throw new Error('Raw schema lost Hole variant applicability');
      for (const [variant, relief] of [['Shaft', ''], ['Counterbore', 'counterboreDiameter: 14mm; counterboreDepth: 3mm; '],
        ['Countersink', 'countersinkDiameter: 14mm; countersinkAngle: 90deg; ']]) {
        const authored = holeSource.replace('<Shaft>', `<${variant}>`).replace('            end:', '            ' + relief + 'end:');
        const completion = await cad.language.complete(authored, authored.indexOf('            on:'), { sourceName: 'bracket.firmament', sourceRevision: '1' });
        const part = (await cad.compile(authored, { sourceName: 'bracket.firmament' })).model;
        if (!part) throw new Error('Hole variant rejected ' + variant);
        try {
          const projection = await part.describeConstruct('Base.CenterMount');
          for (const fields of [completion.fields, projection.fields]) {
            if (fields.some((f: {name: string}) => f.name === 'CounterboreDepth') !== (variant === 'Counterbore')
              || fields.some((f: {name: string}) => f.name === 'CountersinkAngle') !== (variant === 'Countersink'))
              throw new Error('Wrong variant field projection ' + variant);
          }
          if (variant === 'Shaft') {
            const invalid = authored.replace('            end:', '            counterboreDepth: 3mm; end:');
            const failed = await part.setSource(invalid);
            const error = failed.diagnostics.find((d: {code: string}) => d.code.includes('canonical-field-unknown'));
            if (failed.success || !error || error.source?.start !== invalid.indexOf('counterboreDepth')
              || failed.diagnostics.some((d: {code: string}) => d.code === 'firmament-v2-canonical-document-malformed'))
              throw new Error('Wrong-field error projection: ' + JSON.stringify({ expectedStart: invalid.indexOf('counterboreDepth'), failed }));
          }
        } finally { await part.dispose(); }
      }
      const empty = await cad.compile(emptyModifySource, { sourceName: 'bracket.firmament' });
      if (!empty.model) throw new Error('Empty Modify rejected: ' + JSON.stringify(empty.diagnostics));
      await empty.model.dispose();
      return { status: 'pass', runtime: 'real browser WASM via public SDK', samples: times.length, compared, maximumError: maximum,
        sharedDefinitionsRetained: true, lastValidGraphRetained: true, holeVariants: 3, emptyModify: 'pass', locatedFieldError: 'pass' };
    } finally { await cad.dispose(); }
  }, { source, documents, times, expected, holeSource, emptyModifySource });
  const output = resolve(repo, 'artifacts/local/v8-relations');
  await mkdir(output, { recursive: true });
  await writeFile(resolve(output, 'motion-browser.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
} finally {
  await browser?.close();
  await new Promise<void>((done, reject) => server.close(error => error ? reject(error) : done()));
}
