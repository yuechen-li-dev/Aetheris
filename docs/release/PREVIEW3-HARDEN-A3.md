# PREVIEW3-HARDEN-A3 — Normal CAD friction test and geometry/STEP qualification

> Development hardening report. This is release evidence, not the user manual. The public contract is [the Preview 3 support matrix](../public/reference/supported-features.md) and [Firmament V2 geometry guide](../public/firmament/geometry.md).

## Outcome

A3 reached Success for the bounded Firmament **V2** CAD surface. The investigation intentionally excludes the sunset V1 FrictionLab authoring surface. Fresh V2 sources built through the public CLI, emitted AP242, and reimported as enclosed manifolds for analytic primitives, normal holes, a practical mounting plate, and formed/flat sheet metal.

This is a qualified bounded CAD workflow, not an unrestricted general CAD claim. In particular, V2 has no general loft/helix/freeform route, no arbitrary boolean promise, and Sheet Metal accepts its own planar `Hole Name` form rather than Model counterbore/countersink syntax.

## Execution discipline

The sources below were authored as new A3 witnesses, then run with `aetheris build --json`, `aetheris analyze --json`, and where appropriate `aetheris verify --json` and `aetheris sheetmetal flatten --json`. Artifacts are generated under ignored `artifacts/a3`; the checked-in source fixtures are the reproducible corpus.

| Fresh V2 witness | Intent | Result |
|---|---|---|
| [`a3-sphere-step-qualified.firmament`](../../fixtures/Canonical/Primitives/sphere.firmament) | 7.5 mm analytic sphere | one sphere face, enclosed manifold, bounds ±7.5 mm |
| [`a3-pointed-cone-step-qualified.firmament`](../../fixtures/Canonical/Primitives/pointed-cone.firmament) | 6 mm radius, 18 mm pointed cone | plane + cone, enclosed manifold, apex retained |
| [`a3-torus-step-qualified.firmament`](../../fixtures/Canonical/Primitives/torus.firmament) | 12/3 mm major/minor-radius torus | one torus face, enclosed manifold, bounds x/z ±15 mm and y ±3 mm |
| [`a3-blind-shaft-hole.firmament`](../../fixtures/Canonical/Features/Holes/blind-hole.firmament) | blind 8 mm shaft hole in 60 × 40 × 20 mm block | one cylindrical shaft face; enclosed, orientation-consistent reimport |
| [`a3-counterbore-hole.firmament`](../../fixtures/Canonical/Features/Holes/counterbore.firmament) | through 6 mm shaft with 12 × 5 mm counterbore | two cylinders, nine faces, enclosed manifold |
| [`a3-countersink-hole.firmament`](../../fixtures/Canonical/Features/Holes/countersink.firmament) | through 6 mm shaft with 14 mm, 90° countersink | cylinder + cone, eight faces, enclosed manifold |
| [`a3-mounting-plate.firmament`](../../fixtures/Regression/CanonicalGeometry/a3-mounting-plate.firmament) | four through holes, boundary chamfer, datum and diameter PMI | `CombinedHoleEdgeFinish`, 14 faces/4 cylinders, AP242 PMI evidence, reimported manifold |
| [`a3-two-hole-l-bracket.firmament`](../../fixtures/Canonical/SheetMetal/l-bracket-multiple-holes.firmament) | two-hole L bracket | formed STEP: 3 regions, 1 bend, 2 cuts; flat STEP/SVG: valid exact contours and DFM pass |

The negative companion [`countersink-wrong-domain.firmfixture`](../../fixtures/Invalid/SheetMetal/countersink-wrong-domain.firmfixture) fails with `sheetmetal-hole-domain-syntax`, directing the author to canonical Sheet Metal `Hole Name` syntax. There is no silent feature loss.

## Geometry and feature matrix

| Requested normal-CAD case | V2 result | Classification | Qualification boundary |
|---|---|---|---|
| Box / cylinder / frustum | clean | Supported | documented native V2 primitives |
| Sphere | clean | Bounded | parser-backed analytic single solid; exact sphere AP242 face |
| Pointed cone | clean | Bounded | `Cone` with one zero radius; exact cone + plane AP242 faces |
| Normal torus | clean | Bounded | parser-backed analytic single solid; exact torus AP242 face and corrected full-period bounds |
| Raised pad / boss | clean for prismatic profile composition | Bounded | `Compose Add` profile route; no universal named boss or arbitrary union |
| Pocket | clean for prismatic profile composition and semantic slot | Bounded | `Compose Remove` and admitted slot routes; no arbitrary pocket topology |
| Through and blind shaft holes | clean | Supported | semantic `Hole<Shaft>` on admitted Model host faces |
| Counterbore / countersink | clean in Model domain | Bounded | semantic through-hole stack routes; reimported manifold evidence |
| Chamfer / fillet | clean for admitted named selections | Bounded | boundary chamfer and qualified convex profile fillet; not arbitrary topology |
| Sheet-metal circular opening | clean | Bounded | canonical Sheet Metal `Hole Name` produces formed and flat exact-contour evidence |
| Sheet-metal countersink / counterbore | rejected loudly | DeferredFeature | no Sheet Metal feature family; Model-domain form is explicitly diagnosed |
| Hemispherical recessed feature | no native V2 route | DeferredPostPreview3 | lacks an admitted spherical subtraction/composition path |
| Loft / helix / freeform surface | no native V2 route | DeferredPostPreview3 | no public parser, materializer, or AP242 qualification path |

## Combination matrix

| Combination | Status | Evidence |
|---|---|---|
| Four holes + outer chamfer + PMI | Clean | A3 mounting plate reports four hole feature witnesses, one chamfer descendant, one datum and one diameter PMI record; STEP reimport succeeds. |
| Counterbore stack | Clean after fix | Fresh A3 witness reports counterbore + shaft, two cylinders, and an enclosed 9-face reimport. |
| Countersink stack | Clean | Fresh A3 witness reports cone + shaft, one cone/one cylinder, and an enclosed 8-face reimport. |
| Profile fillet and semantic capsule slot | Clean | Existing V2 canonical bounded witnesses build to STEP through real CLI routes. |
| L bracket + two circular openings + flatten | Clean | Fresh A3 witness reports 1 bend/2 cuts; flatten writes valid exact-contour STEP and SVG, DFM Pass. |

## Findings and disposition

| Finding | Classification | Resolution |
|---|---|---|
| V2 counterbore emitted disconnected STEP `EDGE_LOOP` ordering even though build reported a reimport | ReleaseBlocker | `Step242Exporter` now preserves coherent authored order and otherwise deterministically reconnects a small seam-equivalent cycle. If no cycle exists, preflight remains authoritative and rejects it. The public canonical counterbore has a reimported-manifold regression. |
| Full periodic torus inspection used seam vertices as its bounds | MustFix | `StepAnalyzer` now contributes exact full-torus analytic extents; regression asserts the true 26 × 6 × 26 mm bounds for the qualified torus orientation. |
| Public V2 geometry page omitted working parser-backed analytic V2 primitives | DocsFix | Public support and geometry pages now state the bounded `model` / `solid` Sphere, pointed-Cone, and Torus route and link fresh fixtures. |
| Sheet Metal counterbore/countersink could be confused with Model holes | DocumentForPreview | The explicit V2 diagnostic and public matrix now state that only planar Sheet Metal `Hole Name` openings are admitted. |

The loop repair is intentionally bounded to 16 coedges only after authored order is found disconnected. That makes the repair deterministic and avoids factorial recovery work on malformed large loops; unsupported topology continues to fail instead of being cosmetically exported.

## Result semantics

`build` success is not considered sufficient evidence here. Each positive geometry witness has a real AP242 artifact and `analyze` reports one body, one shell, `enclosed-manifold`, and the expected analytic surface family. `verify` additionally reported enclosed and orientation-consistent mass topology for sphere, torus, blind shaft, counterbore, countersink, mounting plate, and sheet-metal formed output. No external CAD assistant was requested; this report claims kernel STEP reimport, not third-party CAD certification.

## Regression coverage

- `FirmamentV2CanonicalGrammarTests.CanonicalCounterbore_ReimportsAsAnEnclosedManifold` guards the public canonical counterbore path.
- `StepAnalyzerTorusBoundsTests` creates, exports, imports, and verifies full analytic torus bounds.
- Updated surgery STEP golden hashes prove deterministic repaired-loop output for orthogonal-union and cylinder-keyway routes, and now require enclosed, orientation-consistent reimport for both.
- The fresh A3 source corpus preserves user-intent cases instead of retaining generated STEP output.
- The negative Sheet Metal fixture preserves the countersink wrong-domain diagnostic.

## Validation

Focused validation completed before the final full suite:

- `dotnet build Aetheris.CLI/Aetheris.CLI.csproj -c Release --no-restore`
- focused CLI and kernel tests for the new counterbore reimport and torus bounds
- real CLI build/analyze/verify matrix for the fresh corpus, plus Sheet Metal flatten to STEP and SVG
- deterministic rerun comparison for the counterbore artifact

- Release build: `dotnet build Aetheris.slnx -c Release --no-restore` passed with 0 warnings and 0 errors.
- Full serial .NET suite (`Category!=SlowCorpus`): 2,972 passed, 0 failed across the discoverable test assemblies. The sunset FrictionLab test project has no matching tests in that default filter.
- Counterbore deterministic rerun: both AP242 artifacts had SHA-256 `A3CD89525DFB88B7E79450431FA965AE7428CF9D6F78825F150D82A7F13215A7`.
- `git diff --check`: passed.
