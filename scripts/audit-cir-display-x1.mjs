// CPU compatibility evidence for X1. This does not qualify a GPU render path.
// Uses the installed Three implementation, including private parsers for diagnosis
// only. No production code may depend on those private APIs.
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const repo = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const [heliosRoot, fragmentPath] = process.argv.slice(2);
if (!heliosRoot || !fragmentPath) {
  throw new Error('Usage: node scripts/audit-cir-display-x1.mjs <HeliosCAD root> <X0 fragment.wgsl>');
}
const threeRoot = join(repo, 'aetheris.client', 'node_modules', 'three');
const load = relative => import(pathToFileURL(join(threeRoot, relative)).href);
const [{ ShaderMaterial, MeshStandardMaterial, LineBasicMaterial },
  { default: StandardNodeLibrary }, { default: WGSLNodeFunction }, { LineMaterial }] = await Promise.all([
  load('build/three.module.js'),
  load('src/renderers/webgpu/nodes/StandardNodeLibrary.js'),
  load('src/renderers/webgpu/nodes/WGSLNodeFunction.js'),
  import(pathToFileURL(join(repo, 'aetheris.client/node_modules/@react-three/drei/node_modules/three-stdlib/lines/LineMaterial.js')).href),
]);
const library = new StandardNodeLibrary();
const materials = [new MeshStandardMaterial(), new LineBasicMaterial(), new ShaderMaterial(), new LineMaterial()];
const materialResults = materials.map(material => {
  const converted = library.fromMaterial(material);
  const result = { type: material.type, convertedType: converted?.type ?? null };
  material.dispose();
  converted?.dispose();
  return result;
});
let moduleParseError = null;
try { new WGSLNodeFunction(await readFile(resolve(fragmentPath), 'utf8')); }
catch (error) { moduleParseError = error.message; }
// A control distinguishes an incompatible module from a broken import/parser.
const functionControl = new WGSLNodeFunction('fn identity(value: f32) -> f32 { return value; }');
const [cadmata, background, helios, sdk, runtime, threePackage] = await Promise.all([
  readFile(join(repo, 'aetheris.client/src/viewer/AetherisViewport.tsx'), 'utf8'),
  readFile(join(repo, 'aetheris.client/src/viewer/ThemeBackground.tsx'), 'utf8'),
  readFile(join(resolve(heliosRoot), 'src/viewport/Viewport.tsx'), 'utf8'),
  readFile(join(repo, 'Aetheris.Web.Runtime/sdk/src/index.d.ts'), 'utf8'),
  readFile(join(repo, 'Aetheris.Web.Runtime/Aetheris.Web.Runtime.csproj'), 'utf8'),
  readFile(join(threeRoot, 'package.json'), 'utf8'),
]);
const report = {
  qualification: 'CPU compatibility audit only; no product GPU rendering qualified',
  threeVersion: JSON.parse(threePackage).version,
  materialResults,
  compiledModule: { path: resolve(fragmentPath), moduleParseError, functionControl: functionControl.name },
  hosts: {
    cadmata: { defaultCanvas: /<Canvas[\s\S]*?gl=\{\{ alpha: false, antialias: true \}\}/.test(cadmata),
      shaderBackground: /new ShaderMaterial\(/.test(background), thickLines: /<Line\b/.test(cadmata) },
    helios: { path: resolve(heliosRoot), webgl: /new THREE.WebGLRenderer\(/.test(helios),
      semanticEdges: /describeEdgeSelection/.test(helios) && /definitionEdges/.test(helios) },
    sdk: { browserWasm: /<RuntimeIdentifier>browser-wasm<\/RuntimeIdentifier>/.test(runtime),
      displayMesh: /readonly mesh: DisplayMesh/.test(sdk),
      cirProgram: /VertexWgsl|FragmentWgsl|cirProgram|CirDisplay/.test(sdk) },
  },
};
const output = join(repo, 'artifacts/local/cir-display-x1');
await mkdir(output, { recursive: true });
await writeFile(join(output, 'compatibility-audit.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report, null, 2));
