# Build Your Own CAD Frontend

A minimal Aetheris CAD frontend needs four projections and one authority:

```text
@aetheris/cad
  -> ModelSession
      -> semantic tree
      -> property schema
      -> DisplayMesh
      -> diagnostics and artifacts
```

Firmament and `ModelSession` are the authority. Your framework, renderer, and layout are choices.

## Minimum path

1. Create `Aetheris` and compile Firmament source.
2. Render `model.tree`; keep semantic IDs as selection keys.
3. Build render geometry from `model.mesh.definitions` and instances from `model.mesh.occurrences`.
4. Send ray hits through `resolveSelection`. Send tree selections through `selectionForEntity`.
5. Generate property controls from `model.properties` and preserve their units.
6. Commit a property with `setProperty` or authoritative text with `setSource`.
7. Treat diagnostics as values. On failure, continue displaying the last valid snapshot.
8. Download manufacturing output from `exportSTEP`.

Optional pieces include an editor runtime adapter, source history, named views, display modes, a command palette, file pickers, and theme persistence. None of them should own engineering semantics.

Do not duplicate the Firmament feature graph, infer semantic identity from triangles, rewrite arbitrary source with regular expressions, mutate a browser B-rep, or hide property overrides. If a public capability is missing, add the smallest general SDK contract and qualify it independently.

Helios uses React and Three.js, but neither is required. Its useful reusable pattern is the authority boundary, not its component framework.
