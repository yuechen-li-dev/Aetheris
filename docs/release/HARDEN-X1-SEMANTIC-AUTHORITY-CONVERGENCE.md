# HARDEN-X1 — Semantic authority convergence

## Executive verdict

**Accepted.** Sequential feature supports, primitive Box feature composition, flat material accounting, and invalid Compose cardinality now resolve through their strongest available semantic representations. The canonical witnesses build through the real CLI, STEP-reimport as enclosed manifolds, and expose no raw authoring exception.

## Authority map

| Concern | Authority | Stale or duplicated path removed |
|---|---|---|
| Relative feature support | Current ordered Compose operations plus semantic profile footprint | Boss/Pocket no longer read only the original Base `To` level |
| Named support | Stable Base/Boss operation name and `.Top` support | No raw BRep face index or topology-order rebinding |
| Compose Base role | Explicit semantic cardinality validation | `SingleOrDefault` no longer enforces authored cardinality |
| Box Hole → EdgeFinish | Ordered `AirHoleFeature` plan followed by the combined final BRep plan | Counterbore is no longer rejected by the primitive combined lane while its Profile route succeeds |
| Flat material area | Validated final `ExactBlankContour` | No `base + tabs - notches - every ProfileDelta` reconstruction |

## Bug A — current-state support

The old Boss parser retained the unique Base operation as `stock`, then assigned every Boss `[stock.To, stock.To + Height]`. A second relative Boss consequently succeeded semantically while occupying the first Boss interval.

Relative `Top` now considers current additive semantic supports that overlap or contain the authored feature footprint, selects the highest support level, and preserves all equal-highest candidates. One candidate attaches; zero fails not-found/disconnected; multiple fail `feature-support-ambiguous:Top` with stable candidate names. Explicit `Base.Top`, `Stock.Top`, and `<BossName>.Top` bypass relative selection and retain named support intent.

The flagship [boss-stack-current-top fixture](../../fixtures/Canonical/Features/Composition/boss-stack-current-top.firmament) proves:

- Base top Z: 10 mm.
- Middle Boss: support `Top`, Z 10–16 mm, height 6 mm.
- Upper Boss: support `Top`, Z 16–20 mm, height 4 mm.
- Final bounds: X −20..20, Y −15..15, Z 0..20 mm.
- One body, one shell, 16 planar faces, enclosed manifold after STEP reimport.
- Slab intervals are ordered without overlap or submergence.

Replacing Upper with `On: Base.Top` produces Z 10–14 mm, proving explicit original-base support remains distinct. The invalid [ambiguous support fixture](../../fixtures/Invalid/FeatureComposition/feature-support-top-ambiguous.firmament) reports `feature-support-ambiguous:Top:feature=Bridge:candidates=Left,Right`.

## Bug A2 — Compose cardinality

Compose previously appended `compose-requires-exactly-one-base-operation`, then called `SingleOrDefault` on the same malformed sequence. Multiple Base roles therefore escaped as `InvalidOperationException: Sequence contains more than one matching element`.

The parser now materializes the Base candidates once, validates their count, and only reads element zero when the count is exactly one. The invalid [multiple-Base fixture](../../fixtures/Invalid/FeatureComposition/compose-multiple-base.firmament) reports `compose-role-cardinality:Base:expected=1:actual=2`; CLI build returns failure and writes no STEP file. Compose cardinality and feature-support diagnostics are classified as fatal authored semantics by validation.

The scoped raw-exception audit found the same Base assumption in counterbore footprint containment. It is now unreachable after typed cardinality validation. Test-only assertions and already-validated internal topology cardinalities were not mechanically changed.

## Bug B — primitive feature composition

The Box combined route already ordered `HostBRepPlan → HoleChangedBRepPlan → EdgeFinishChangedBRepPlan`, but it hard-rejected every stack other than `SimpleShaft`. The ordinary Air hole materializer already planned and materialized Counterbore, so the rejection duplicated a weaker feature-family gate.

The final combined plan now consumes either a through Shaft or through Counterbore from the same `AirHoleFeature` stage. Counterbore carries shaft radius, entry radius, and relief depth into one final exact topology containing the shaft cylinder, relief cylinder, annular shoulder, and the later top-boundary chamfer. Primitive Box semantics remain intact.

The [10 × 15 × 20 mm Box witness](../../fixtures/Canonical/Features/Composition/box-counterbore-chamfer.firmament) has a 4 mm through shaft, 7 mm counterbore, 3 mm relief depth, and 1 mm top chamfer. CLI/STEP evidence:

- Bounds preserved at 10 × 15 × 20 mm.
- One body, one shell, 13 faces, 26 edges, 16 vertices.
- 11 planar and 2 cylindrical surfaces.
- Removed hole volume 329.0818304635 mm³; final analytic volume 2647.2181561925 mm³.
- STEP SHA-256 `56753B44A4220E7B82F0E2BB83DF75F2EDF97CDE9E7906B33D45E8AFCC42D908`.
- STEP reimport succeeds as an enclosed manifold.

Qualification covers Box Shaft/Counterbore, Hole-only, EdgeFinish-only, and Hole → EdgeFinish routes, plus Profile/Compose Shaft/Counterbore and EdgeFinish witnesses. EdgeFinish → Hole remains intentionally order-sensitive: the declared sequence is not claimed to commute, and the combined production route continues to apply Hole before EdgeFinish.

## Bug C — final flat material region

Authored lowering already constructed the exact final flange contour, including ProfileDelta members, but set `ApproximateArea` using reconstructed arithmetic with `- profileDeltas.Sum(DeltaArea)`. That assumed every ProfileDelta removed material and produced 3410 mm² for an additive 90 mm² tab.

`PlanarContourKernel.Area` now analytically integrates final outer and inner loops for lines, circular arcs, and full circles. Authored planar regions use that exact result. `SheetMetalFlatPatternIr.MaterialArea` reads the validated final `ExactBlankContour`; it returns unavailable when exact authority is absent. `BoundingArea` remains a separate envelope quantity. CLI text and JSON report both names explicitly.

Evidence:

- [Add witness](../../fixtures/Canonical/SheetMetal/profiledelta-add-area.firmament): 3500 + 15×6 = **3590 mm²** for the final Wall contour.
- [Remove witness](../../fixtures/Canonical/SheetMetal/profiledelta-remove-area.firmament): 3500 − 15×6 = **3410 mm²**.
- [Tab + hole witness](../../fixtures/Canonical/SheetMetal/profiledelta-add-hole-area.firmament): the same additive tab plus an 8 mm through hole subtracts exactly `π × 4² mm²` from the final blank.
- Inner-loop integration witness: 200 − 12 = **188 mm²**.
- Full tabbed blank: `MaterialArea = 8982.699081698724 mm²`; `BoundingArea = 9492.699081698724 mm²`.

Existing manufacturing flat STEP generation already consumes `ExactBlankContour`, so it shares this authority. DFM exact-blank validation, SVG, flat STEP, and inspection now agree. Region `ApproximateArea` keeps its compatibility name but, for authored exact planar regions, contains the exact contour measurement.

## Artifacts and reproduction

Generated evidence follows repository policy and remains under ignored `artifacts/local/harden-x1/`:

- `harden-x1-boss-stack.step` and `.wireframe.svg`.
- `harden-x1-box-counterbore-chamfer.step` and `.wireframe.svg`.
- `harden-x1-tabbed-sheet-flat.step` and `.svg`.

Reproduce with:

```powershell
dotnet run --project Aetheris.CLI -c Release -- build fixtures/Canonical/Features/Composition/boss-stack-current-top.firmament --output artifacts/local/harden-x1/harden-x1-boss-stack.step --json
dotnet run --project Aetheris.CLI -c Release -- build fixtures/Canonical/Features/Composition/box-counterbore-chamfer.firmament --output artifacts/local/harden-x1/harden-x1-box-counterbore-chamfer.step --json
dotnet run --project Aetheris.CLI -c Release -- sheetmetal flatten fixtures/Canonical/SheetMetal/profiledelta-add-area.firmament --step artifacts/local/harden-x1/harden-x1-tabbed-sheet-flat.step --svg artifacts/local/harden-x1/harden-x1-tabbed-sheet-flat.svg --json
dotnet run --project Aetheris.CLI -c Release -- wireframe artifacts/local/harden-x1/harden-x1-boss-stack.step --out artifacts/local/harden-x1/harden-x1-boss-stack.wireframe.svg --view iso --density 8
dotnet run --project Aetheris.CLI -c Release -- wireframe artifacts/local/harden-x1/harden-x1-box-counterbore-chamfer.step --out artifacts/local/harden-x1/harden-x1-box-counterbore-chamfer.wireframe.svg --view iso --density 8
```

## Validation

- Release solution build: clean, zero warnings/errors.
- Focused Firmament Boss/Pocket/Hole/Counterbore/EdgeFinish/combined suite: 361 passed.
- Focused Sheet Metal ProfileDelta/flat/CTC/manufacturing suite: 39 passed.
- Flagship CLI build, inspect, STEP reimport, manifold, and wireframe qualification: passed.
- Full serial solution suite: 3,235 passed, 0 failed across all discovered test assemblies; the FrictionLab assembly reports no discoverable tests.
- Canonical fixture qualification: 134 passed after the final tab + hole witness was added (133 in the full corpus pass plus the final witness qualified through the same CLI route).
- Repository layout guard, local Markdown-link check, and `git diff --check`: passed.
