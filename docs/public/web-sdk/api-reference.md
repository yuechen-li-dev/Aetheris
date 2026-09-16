# Web SDK API reference

The stable preview contract is `aetheris/web-editor-contract/1`.

- `Aetheris.create(options?)` initializes the runtime. Options: `worker` (only `false`/omitted in X1), `wasmUrl`, and a diagnostics callback.
- `cad.info()` and `cad.capabilities()` report package/runtime/contract versions and support flags.
- `cad.compile(source, { sourceName?, signal? })` returns `{ model, diagnostics }`; compiler failures are values, not thrown private exceptions.
- `model.tree`, `properties`, `mesh`, `diagnostics`, `revision`, and `changes` are the current valid snapshot.
- `model.entity(id)` and `model.property(id)` inspect public records.
- `model.setProperty(id, { value, unit })` validates and rebuilds a source-backed scalar property.
- `model.setSource(source)` replaces source and rebuilds transactionally.
- `model.rebuild()` rebuilds the current source plus overrides.
- `model.resolveSelection(...)` and `selectionForEntity(...)` translate selection in both directions.
- `model.exportSTEP()` returns `Uint8Array`; `exportSTEPBlob()` returns a browser `Blob`.
- `model.dispose()` and `cad.dispose()` release managed sessions.

Unexpected failures throw `AetherisError` with stable `code`, safe `message`, and optional developer `details`. See the shipped `index.d.ts` for exact readonly record types.
