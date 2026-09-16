# Web SDK troubleshooting

If Vite returns 404 for runtime files, add `aetherisCad()` from `@aetheris/cad/vite`. For another bundler, copy `dist/runtime` unchanged to a public URL and pass that directory as `wasmUrl`.

Serve over HTTP(S), not `file://`. Preserve `.wasm` files and serve them as `application/wasm`. Runtime assets must remain same-origin unless the asset host supplies correct CORS headers.

If `compile` returns `model: null`, render the structured diagnostics. Do not parse exception strings. If a rebuild fails, the session still exposes its prior valid mesh and revision.

`worker: true` is intentionally unavailable in X1. Omit it or pass `false`. SQLite materials, external file import, sheet metal, and FEA are likewise outside the admitted browser capability set.
