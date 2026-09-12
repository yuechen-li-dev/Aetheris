# LANG-STOCK-FRAME-X1 — stock-frame authority

## Verdict

**Accepted.** Authored native `Box` stock now has one explicit placement authority: `FirmamentStockFrame.ForBox`. Its origin is XY-centered at Bottom (`0,0,0`); `Bottom` is Z=0 and `Top` is Z=Height. Features use the same body-local XY coordinates and consume that frame's host interval. An explicit authored transform, where admitted by its owning route, applies to this complete frame.

## Authority audit and migration

| Route | Before | After | Compatibility impact |
|---|---|---|---|
| Primitive Box | Kernel-centered Box translated to Z=0..H | unchanged; kernel-to-stock adapter remains explicit | none |
| Counterbore / semantic Hole | `ConceptIr == null` chose -H/2..+H/2 | consumes `FirmamentStockFrame.ForBox` (0..H) | none for current V2 |
| Hole + EdgeFinish | repeated the same conditional host construction | consumes the same frame | none for current V2 |
| Profile / Compose | own authored support planes | unchanged; Base.Top remains its existing semantic authority | none |
| Concept-backed Box | 0..H incidentally | 0..H by the same explicit authority | parity is now intentional |
| Non-Concept Box | -H/2..+H/2 in feature routes | 0..H by the same explicit authority | removes accidental current-route divergence |

The previous defect was not a STEP problem: primitive execution already placed a Box at Z=0..H, while `SemanticHoleInspection`, semantic-hole export, and the combined hole/finish route independently constructed a centered host only when `ConceptIr` was absent. The BRep and STEP were therefore internally valid but in the wrong world frame. STEP still serializes the realized BRep without corrective translation.

`FirmamentStockFrame` is the semantic adapter between the centered kernel primitive and authored stock. It carries the stock origin, dimensions, bounds, and Bottom/Top support planes; AIR hole materializers receive its host rather than deriving placement from dimensions, feature kind, or Concept IR. Structured hole inspection includes `stockFrame` for diagnostics.

## Burn-in evidence

| Witness | Before world Z bounds | X1 world Z bounds | Result |
|---|---:|---:|---|
| stock-block | 0..12 | 0..12 | unchanged |
| mount-counterbore | -6..6 | 0..12 | fixed |
| hole-finish | -6..6 | 0..12 | fixed |
| finish-hole | -6..6 | 0..12 | fixed |
| explicit Compose boss-stack | 0..21 | 0..21 | preserved (separate Compose authority) |

The four Box witnesses are built, reimported, and analyzed under `artifacts/local/stock-frame-x1/`; each reports X=-30..30, Y=-20..20, Z=0..12 and an enclosed manifold. The counterbore retains its center axis at X=0/Y=0, top entry at Z=12, and counterbore floor at Z=8. Repeated counterbore export is byte-identical under the current deterministic exporter policy.

## Bounded related audit

Cylinder, cone, sphere, and torus still use their existing primitive-local conventions. This X1 change does not alter mathematical sweep cylinders or those primitives' specialized realizers. Their generic primitive execution already has an explicit default-local-frame adapter; no ConceptIr-dependent branch analogous to the Box-hole defect was found in the audited current native V2 feature routes.

Legacy V1 remains a compatibility dialect with its own lowering and fixture contracts. No legacy centered behavior was silently preserved in current Firmament V2; legacy routes were not changed by this focused repair.

## Reproduction

```powershell
dotnet run --project Aetheris.CLI -c Release -- build fixtures/Regression/LanguageBurnIn/mount-counterbore/mount-counterbore.firmament --output artifacts/local/stock-frame-x1/stock-frame-x1-counterbore.step --json
dotnet run --project Aetheris.CLI -c Release -- analyze artifacts/local/stock-frame-x1/stock-frame-x1-counterbore.step --json
dotnet run --project Aetheris.CLI -c Release -- wireframe artifacts/local/stock-frame-x1/stock-frame-x1-counterbore.step --out artifacts/local/stock-frame-x1/stock-frame-x1-counterbore.wireframe.svg --view iso --density 8
```

The same directory holds the requested Box, counterbore-chamfer, and boss-stack STEP/wireframe evidence. Validation also includes the Release solution build, focused Firmament tests, burn-in replay, and `git diff --check`.
