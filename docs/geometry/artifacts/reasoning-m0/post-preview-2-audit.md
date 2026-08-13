# Post-Preview-2 historical-gap audit

The historical findings were re-tested before changes.

| Finding | Pre-change result | M0 disposition |
|---|---|---|
| Panel-only `validate` succeeds but `build` indexes an empty solid list | Still present. CLI validation returned valid with two warnings; CLI build threw `Index was out of range`. | Fixed as a current defect. Build now returns typed `NotImplemented` diagnostic `firmament-panel-only-step-materialization-unsupported`. It does not emit a misleading solid STEP file. |
| Assembly `<Panel>` identity is not linked to compiled `PanelIr` | Changed architecture, partially applicable. `AssemblyInstanceKind.Panel` exists and programmatic assembly members carry Panel semantic edges/provenance; textual assembly occurrence identity remains a product/definition identity rather than an automatic cross-document `PanelIr` object reference. | No brittle cross-compiler object link added. Public patch identity/provenance now gives future assembly integration a stable contract. Existing Panel mate tests remain the ground truth for linked programmatic occurrences. |
| Public parametric surface types are reachable only through unstable/internal paths | The expression tree and `ParametricSurfaceIr` were public but owned by `Aetheris.Surfacing` and coupled to materialization semantics. | Fixed by deliberate ownership. Domain-neutral domain/expression/first-jet/patch/query/evidence contracts now live in the public `Aetheris.Geometry` assembly. `ParametricSurfaceIr` is a Surfacing wrapper over the public patch. |

Evidence fixture: `historical-panel-only.firmament`. The CLI commands used were:

```text
dotnet run --project Aetheris.CLI -- validate docs/geometry/artifacts/reasoning-m0/historical-panel-only.firmament
dotnet run --project Aetheris.CLI -- build docs/geometry/artifacts/reasoning-m0/historical-panel-only.firmament --out docs/geometry/artifacts/reasoning-m0/historical-panel-only.step
```

The first post-fix command remains valid; the second exits cleanly with the typed unsupported-shell-export diagnostic. No STEP artifact is authored because a Panel is not silently redefined as a solid.
