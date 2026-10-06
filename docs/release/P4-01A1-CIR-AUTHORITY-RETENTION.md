# P4-01A1 — CIR authority retention

**Verdict: Success for retention. Production CIR rendering remains a separate gate.**

The admitted V2 primitive executor already builds an evaluable CIR mirror.
It previously discarded that root while returning the mirror's audit summary.
The executor now retains that same root in `NativeGeometryCirMirrorState`;
`FirmamentBuildAndExport` carries it beside the canonical BRep through
`FirmamentStepExportResult.Cir`. The existing legacy bounded-CSG mirror also
retains its already-built root. No second source parser or geometry inference
is involved.

`FirmamentCirRetention` separates runtime authority from transport:

- `RuntimeRoot` is the existing evaluable `SdfNode`, excluded from JSON.
- `Schema` is `aetheris/cir-retention/1`.
- `DefinitionId` uses the existing deterministic STEP-content identity for
  Parts; Assembly and Scene projection bind it to their existing definition ID.
- `Qualification`, `FallbackReason`, `StructuralIdentity`, `FieldSource` and
  millimetre bounds come from the existing `CirVisualTsLowerer` admission.
- `FieldSource` is typed Visual TypeScript, not WGSL or a new CIR AST format.

Assembly materialization carries CIR once per definition in
`AssemblyExecutedGeometry.DefinitionCir`. Retained compilation copies mutable
metadata arrays before returning cached results. Canonical BRep, STEP and
construction correspondence continue to own engineering geometry and selection.
Cylinder, Sphere, Cone and Torus now also retain their existing native BRep
instead of requiring display to reimport their export.

The regression cases compile normal V2 Box, Cylinder, Sphere, Cone and Torus,
verify stable definition identity, evaluate the retained root, and verify JSON
contains neither runtime root nor BRep. An existing legacy CSG fixture proves
root retention without extending V2 CSG syntax. Unsupported mirror/lowering
paths retain explicit mesh eligibility and reasons.

## Next boundary

Qualification does not bind a GPU program. The canonical display definition
remains `displayPath: mesh`, with `shader-artifact-not-bound` for a qualified
field. The normal Cadmata cylinder packet now carries one qualified field
definition, but its observed GPU pipeline is still `telos-mesh/1`.

The next owner-layer repair is an artifact provider/cache over this retained
representation, adopting the existing managed WGSL backend through an ordinary
versioned dependency and binding the existing Telos field ABI. This milestone
does not promote a witness-only provider or reference a sibling checkout from
production.

## Validation and reproduction

Eight `DisplayProjectionTests` cases cover retention, existing CSG,
Scene/project transport and missing-resource refusal. Full gate and real-product
results are recorded in the [P4-01A rerun](P4-01A-DISPLAY-PROJECTION-CLOSEOUT.md).

```powershell
dotnet test Aetheris.Kernel.Firmament.Tests -c Release --no-build --filter FullyQualifiedName~DisplayProjectionTests
pwsh -File scripts/audit-display-projection-closeout.ps1 -OutputDirectory artifacts/local/display-projection-closeout/compiler-a2
```

Raw evidence remains ignored under
`artifacts/local/display-projection-closeout/compiler-a2/compiled-contracts.json`.
