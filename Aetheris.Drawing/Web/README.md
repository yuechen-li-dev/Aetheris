# Drawing Notes browser dependency

`package.json` and `package-lock.json` pin PDF.js and its integrity hash. Build
restores it automatically with `npm ci --ignore-scripts --omit=optional` when
missing or when the manifests change. Source builds require Node/npm; published
tools do not. The optional native Node canvas package is never installed.

The downloaded `node_modules/` distribution and the former `pdfjs/` vendor folder
are ignored by Git. Do not commit downloaded distributions or archives here.

The project explicitly embeds only the minified browser module and worker,
runtime CMaps/fonts/decoders/ICC profiles, and their licenses. It excludes source
maps, legacy builds, viewers, examples, types, package metadata and Node tooling.
The two modules retain their existing `/pdfjs/build/pdf.mjs` and
`/pdfjs/build/pdf.worker.mjs` URLs. All runtime assets are served locally, with no
CDN requests. Browser WebAssembly decoders are portable assets, not OS libraries.

Update both manifests and `DrawingPdf.RendererVersion` together when upgrading.
After a Release CLI build, run `node scripts/test-drawing-notes-browser.mjs` to
check the real PDF, annotations and crop exports. Set `AETHERIS_PLAYWRIGHT_MODULE`
and `AETHERIS_BROWSER_PATH` if needed for local Playwright/browser installations.
