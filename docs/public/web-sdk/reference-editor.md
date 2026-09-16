# Helios reference editor

Helios is a reference CAD editor built entirely on the public Aetheris Web SDK. It demonstrates that a useful frontend can stay thin: Firmament owns engineering meaning, and the browser owns discoverability, selection, inspection, authoring assistance, and presentation.

Run it from `demos/Aetherion.Helios` with `npm run sdk:install` followed by `npm run dev`. The setup command consumes a locally packed `@aetheris/cad` tarball, which qualifies the public package rather than repository-relative SDK source.

## Data flow

```text
UI intent
  -> Firmament source or typed property override
  -> ModelSession.setSource / setProperty
  -> transactional SDK rebuild
  -> tree + properties + DisplayMesh + diagnostics
  -> Helios projection
```

There is no browser feature graph, solid model, B-rep mutation layer, or private runtime access.

## Public API mapping

| Editor surface | Public SDK authority |
|---|---|
| Runtime status | `Aetheris.create`, `info`, `capabilities` |
| Open/compile | `Aetheris.compile` |
| Model tree | `ModelSession.tree`, `entity` |
| Property inspector | `properties`, `property`, `setProperty` |
| Source editor | `source`, `setSource`, `rebuild` |
| Viewport | `mesh.definitions`, `mesh.occurrences` |
| Viewport to model | `resolveSelection` |
| Tree to viewport | `selectionForEntity` |
| Diagnostics | compile and rebuild result diagnostics |
| STEP download | `exportSTEP` |

The viewport creates one Three.js `BufferGeometry` per SDK definition and one mesh object per occurrence. Occurrence transforms are applied exactly as supplied. GPU conversion from `Float64Array` to `Float32Array` happens only at the renderer boundary.

## Editing policy

The X1 SDK represents supported property edits as typed effective overrides. Helios therefore keeps the authoritative source visible and labels the inspector values as overrides. Replacing source clears overrides according to the SDK contract.

Property edits commit only through **Apply** or Enter. A rejected edit remains in the field, the SDK keeps the last valid revision and geometry, and the diagnostic stays visible for correction. Source edits commit with Rebuild or Ctrl/Cmd+Enter.

The Hole toolbar command is deliberately narrow. It appends one readable, canonical Firmament `Modify` block for an existing body and then relies on the ordinary source rebuild. It does not infer or alter B-rep geometry.

## Selection authority

On a viewport click, Three.js supplies only the definition, occurrence, and triangle index. Helios asks `resolveSelection` for the semantic entity and face, then stores that identity as editor selection. The tree and inspector consume the same identity. In the reverse direction, `selectionForEntity` returns occurrence/range bindings for viewport highlighting.

## Runtime boundary

`CadRuntime` is intentionally small: initialize, open, source/property rebuild, STEP export, and dispose. `WebSdkCadRuntime` delegates directly to the public package. A future local, hosted, or native engine can implement the same editor-facing boundary without changing Firmament authority. Those transports are not part of X1.

## Browser and capability limits

Chromium is the X1 browser baseline. Firefox is not claimed. The public runtime is main-thread WASM because `worker: true` returns `worker-unavailable`. Sheet metal, FEA, external STEP import, and Forge subprocess capabilities remain unavailable in browser X1 and Helios does not fake them.

Helios is intentionally bounded. It is an example frontend, not the only Aetheris frontend and not a feature-for-feature commercial CAD replacement.
