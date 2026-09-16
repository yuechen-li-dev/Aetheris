import { createReadStream, existsSync } from 'node:fs';
import { cp, stat } from 'node:fs/promises';
import { extname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const runtimeDirectory = fileURLToPath(new URL('./runtime/', import.meta.url));
const publicPath = '/aetheris-runtime/';
const mime = new Map([['.js', 'text/javascript'], ['.json', 'application/json'], ['.wasm', 'application/wasm'], ['.pdb', 'application/octet-stream'], ['.dat', 'application/octet-stream']]);

export function aetherisCad() {
  let outputDirectory;
  return {
    name: 'aetheris-cad-runtime',
    enforce: 'pre',
    config: () => ({ define: { __AETHERIS_RUNTIME_BASE__: JSON.stringify(publicPath) }, optimizeDeps: { exclude: ['@aetheris/cad'] } }),
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
    async writeBundle() { await cp(runtimeDirectory, join(outputDirectory, 'aetheris-runtime'), { recursive: true }); }
  };
}

export default aetherisCad;
