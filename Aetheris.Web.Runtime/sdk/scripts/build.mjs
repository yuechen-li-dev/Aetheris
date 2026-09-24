import { cp, mkdir, rm, copyFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const sdk = resolve(here, '..');
const runtime = resolve(sdk, '..');
const repo = resolve(runtime, '..');
const production = process.argv.includes('--production');
const project = join(runtime, 'Aetheris.Web.Runtime.csproj');
const run = args => {
  const result = spawnSync('dotnet', args, { cwd: repo, stdio: 'inherit' });
  if (result.status !== 0) process.exit(result.status ?? 1);
};
run(['build', project, '-c', 'Release', '--nologo']);
const app = join(runtime, 'bin', 'Release', 'net10.0', 'wwwroot');
const dist = join(sdk, 'dist');
await rm(dist, { recursive: true, force: true });
await mkdir(dist, { recursive: true });
await cp(join(app, '_framework'), join(dist, 'runtime', '_framework'), { recursive: true });
if (production) {
  const published = join(repo, 'artifacts', 'local', 'helios-sdk', 'aot-publish');
  run(['publish', project, '-c', 'Release', '-p:RunAOTCompilation=true', '-p:WasmStripILAfterAOT=false',
    '-o', published, '--nologo', '-m:1']);
  await cp(join(published, 'wwwroot', '_framework'), join(dist, 'runtime-aot', '_framework'), { recursive: true });
}
await copyFile(join(sdk, 'src', 'index.js'), join(dist, 'index.js'));
await copyFile(join(sdk, 'src', 'worker.js'), join(dist, 'worker.js'));
await copyFile(join(sdk, 'src', 'runtime.js'), join(dist, 'runtime.js'));
await copyFile(join(sdk, 'src', 'index.d.ts'), join(dist, 'index.d.ts'));
await copyFile(join(sdk, 'src', 'vite.js'), join(dist, 'vite.js'));
await copyFile(join(sdk, 'src', 'vite.d.ts'), join(dist, 'vite.d.ts'));
console.log(`Built ${production ? 'production AOT + page runtime' : 'development runtime'} @aetheris/cad at ${dist}`);
