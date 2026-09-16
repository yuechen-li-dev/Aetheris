# Helios by Aetheris

Helios is the deliberately bounded reference CAD editor for the public `@aetheris/cad` Web SDK. Firmament remains the model authority; Helios is an authoring, inspection, and presentation surface.

## Run

From this directory:

```powershell
npm run sdk:install
npm run dev
```

`sdk:install` builds and packs the public SDK, then installs that tarball as an ordinary package dependency. Helios imports only `@aetheris/cad` and `@aetheris/cad/vite`; it does not import repository internals.

## Architecture

- `src/sdk/` is the replaceable runtime boundary. `WebSdkCadRuntime` is the X1 browser/WASM implementation.
- `src/viewport/` consumes only `DisplayMesh`, preserving SDK definitions and occurrences. Ray hits go through `resolveSelection`; tree selections go through `selectionForEntity`.
- `src/model-tree/` and `src/inspector/` render public semantic records and property schemas.
- `src/source/` exposes authoritative Firmament text and normalized diagnostics.
- `src/commands/` contains bounded, deterministic source-authoring helpers. It never constructs geometry.
- `src/themes/` defines the reusable Mars and Sirius token sets.

The browser runtime runs on the page thread in X1. Large rebuilds may temporarily block UI. Sheet metal is intentionally disabled because the public capability matrix does not qualify it for browser/WASM.

## Qualification

```powershell
npm test
npm run build
```

Chromium is the qualified baseline. The live qualification flow covers the bracket, semantic selection in both directions, valid and invalid property edits, last-valid-geometry retention, source rebuild, assembly hierarchy/occurrences, Mars/Sirius, fit/orbit/pan/zoom, and STEP export invocation.

See [the public reference-editor guide](../../docs/public/web-sdk/reference-editor.md) and [Build Your Own CAD Frontend](../../docs/public/web-sdk/build-your-own-cad-frontend.md).
