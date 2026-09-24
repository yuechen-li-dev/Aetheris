import { createReadStream, existsSync } from 'node:fs';
import { cp, stat } from 'node:fs/promises';
import { extname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const runtimeDirectory = fileURLToPath(new URL('./runtime/', import.meta.url));
const aotRuntimeDirectory = fileURLToPath(new URL('./runtime-aot/', import.meta.url));
const publicPath = '/aetheris-runtime/';
const aotPublicPath = '/aetheris-runtime-aot/';
const mime = new Map([['.js', 'text/javascript'], ['.json', 'application/json'], ['.wasm', 'application/wasm'], ['.pdb', 'application/octet-stream'], ['.dat', 'application/octet-stream']]);

export function aetherisCad() {
  let outputDirectory; let production = false;
  return {
    name: 'aetheris-cad-runtime',
    enforce: 'pre',
    config: (_, environment) => {
      production = environment.command === 'build' && environment.mode === 'production';
      if (production && !existsSync(aotRuntimeDirectory))
        throw new Error('Production requires the AOT SDK package. Run npm run sdk:install:production.');
      return { define: { __AETHERIS_RUNTIME_BASE__: JSON.stringify(publicPath),
        __AETHERIS_WORKER_RUNTIME_BASE__: JSON.stringify(production ? aotPublicPath : publicPath) },
        optimizeDeps: { exclude: ['@aetheris/cad'] } };
    },
    configResolved: config => { outputDirectory = config.build.outDir; },
    configureServer(server) {
      server.middlewares.use(publicPath, async (request, response, next) => {
        const relative = decodeURIComponent((request.url ?? '').split('?')[0]).replace(/^\/+/, '');
        const path = resolve(runtimeDirectory, relative);
        const runtimeRoot = resolve(runtimeDirectory);
        if (path !== runtimeRoot && !path.startsWith(runtimeRoot + sep)) return next();
        if (!existsSync(path) || !(await stat(path)).isFile()) return next();
        response.setHeader('Content-Type', mime.get(extname(path)) ?? 'application/octet-stream');
        createReadStream(path).pipe(response);
      });
    },
    configurePreviewServer(server) {
      server.middlewares.use((request, response, next) => {
        const path = (request.url ?? '').split('?')[0];
        if (path.startsWith(aotPublicPath) || path.startsWith(publicPath)) {
          // Manifest-fingerprinted runtime assets are immutable. Boot files keep
          // a short freshness window so a newly deployed manifest is observed.
          response.setHeader('Cache-Control', /\.[a-z0-9]{10,}\.(?:wasm|dll|js|dat)$/.test(path)
            ? 'public, max-age=31536000, immutable' : 'public, max-age=300');
        }
        next();
      });
    },
    async writeBundle() {
      await cp(runtimeDirectory, join(outputDirectory, 'aetheris-runtime'), { recursive: true });
      if (production) await cp(aotRuntimeDirectory, join(outputDirectory, 'aetheris-runtime-aot'), { recursive: true });
    }
  };
}

export default aetherisCad;
