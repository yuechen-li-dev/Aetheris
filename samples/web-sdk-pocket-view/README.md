# Aetheris Web SDK Pocket View

A tiny, framework-free CAD frontend built as an external consumer of `@aetheris/cad`.
It compiles the canonical editable bracket, lists its semantic model nodes, draws a
simple canvas wireframe from public display-mesh records, reports mesh counts, and
downloads an AP242 STEP file.

The sample intentionally uses only the public Web SDK contract. Firmament and the
returned `ModelSession` remain authoritative; the canvas is just a projection.

## Run

The package dependency points at the locally packed preview tarball under
`artifacts/local/helios-sdk/` so this checkout can test the exact release candidate.

```powershell
cd samples/web-sdk-pocket-view
npm install
npm run dev
```

Open the URL printed by Vite. Runtime assets are copied and served by the public
`aetherisCad()` Vite plugin.

## Check

```powershell
npm run check
```

This type-checks the frontend against the shipped declarations and performs a
production Vite build, including the canonical fixture import and runtime assets.

The X1 package is AGPL-3.0-only. See `docs/public/web-sdk/` for capabilities,
limitations, and integration guidance.
