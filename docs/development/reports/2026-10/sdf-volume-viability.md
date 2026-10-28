# SDF volume viability — 2026-10-28

The existing bounded estimator is now useful at 0.1% occupied-volume uncertainty
for the ten constructive analytic reference fields below. This improves numerical
integration without changing geometry, broadening field admission or treating a
display mesh as volume authority.

## What changed

Previously every mixed boundary cell contributed `[0, cell volume]`. Refinement
reduced cell size but spent most of its budget on a thick uncertain boundary band.
The radius-10 sphere could not meet even ±50 mm³ within one million regions.

`SdfTape.EvaluateRegionEnclosure` now reuses the existing interval instruction
evaluation and supplies a local affine envelope where analytic primitive branches
permit it. Sphere and cylindrical/frustum walls use convex norm remainder bounds;
box and cap branches are planar; torus branches use a two-sided Hessian bound.
Inverse transforms pull gradients back into world coordinates, including nonuniform
scale and reflection. CSG propagates an envelope only when interval dominance
proves which branch is active. Negation swaps the two remainder bounds.

For a cell centered at `c`, the contract is
`L(x) - lowerError <= field(x) <= L(x) + upperError`, with
`L(x) = value + gradient·(x-c)`. Consequently:

- `L(x) + upperError <= 0` defines a subset of occupied material.
- `L(x) - lowerError <= 0` defines a superset of occupied material.

Integrating those two plane cuts through the box bounds occupied volume. The
normalized weighted-uniform integral uses inclusion/exclusion, complement symmetry
and an exact central-slab branch to avoid cancellation near axis-aligned cuts.
Very small normal components contribute their full variation to uncertainty.
This is analytic integration of field envelopes, not fitting an empirical error
bar to sampled points. Primitive volume formulas appear only in qualification.

Analytic plane/cube volume fractions have established numerical literature; see
[Scardovelli and Zaleski, JCP 164 (2000), 228–237](https://www.sciencedirect.com/science/article/pii/S0021999100965677).
Here the two enclosing planes carry the curvature uncertainty explicitly, rather
than treating one reconstructed plane as the exact curved interface.

The estimator refines the largest remaining volume uncertainty, uses compensated
global sums, and retains the existing strict inside/outside classification,
region/depth budgets and roundoff guards. Unsupported/singular envelopes and
unresolved CSG seams keep `[0, cell volume]`. A thin shell is not removed merely
because its cell corners are outside. No new JudgmentEngine policy is needed for
these deterministic transformations; the existing measurement owner still chooses
exact BRep versus admitted SDF.

## Controlled evidence

Release, .NET 10.0.401, Windows x64, Ryzen 7 7700X. Each path receives full-workload
warmup; three timed repetitions alternate execution order; tiered compilation is
disabled. Same bounds, fields, 0.1% requested half-width, 1,000,001-region cap and
depth 30. `tools/Aetheris.SdfVolumeQualification` freezes the former estimator from
`e1310ce2` as a qualification-only control, sharing the unchanged interval math.
Times are local warmed medians, not cold CLI latency or portable benchmarks.

All references lie in every measured interval. All ten new intervals meet the
target; all ten interval-only controls exhaust their budgets without meeting it.

| Field | New half-width mm³ | Old half-width mm³ | New regions | New ms | Old ms |
|---|---:|---:|---:|---:|---:|
| Sphere, r=10 | 4.188 | 78.936 | 35,805 | 46 | 572 |
| Scaled/rotated/translated sphere | 6.283 | 214.627 | 81,797 | 104 | 618 |
| Cylinder | 1.571 | 26.457 | 42,453 | 56 | 705 |
| Frustum | 0.653 | 18.216 | 52,707 | 62 | 621 |
| Torus | 0.790 | 36.827 | 861,033 | 1,302 | 627 |
| Plate with three through holes | 13.890 | 509.607 | 68,833 | 321 | 2,536 |
| Crossed ribs | 5.736 | 195.052 | 248,921 | 889 | 1,967 |
| Thin spherical shell, r=9.5–10 | 0.597 | 213.287 | 929,655 | 2,360 | 1,144 |
| Overlapping-sphere union | 0.938 | 19.972 | 72,593 | 166 | 1,129 |
| Overlapping-sphere intersection | 0.109 | 2.707 | 77,035 | 175 | 1,127 |

The difficult torus/shell cases cost more time than the old *inconclusive* result.
This is not a universal speedup claim. Temporary allocation also remains material:
about 85 MiB for the sphere, 620 MiB for the plate, and 3.75 GiB cumulatively for
the thin shell in a run. These are allocated bytes over the call, not simultaneous
resident memory or a leak. Per-cell interval/envelope scratch allocation is a
worthwhile next optimization before repeatedly integrating such cases interactively.

A second probe requests **0.01%** with the same work cap. Six cases meet it; four
truthfully report exhaustion. All ten still contain their independent references.
This probe uses one timed repetition; its timing is not used in the comparison.

| Exhausted field | Requested half-width mm³ | Actual half-width mm³ |
|---|---:|---:|
| Torus | 0.078957 | 0.657370 |
| Crossed ribs | 0.573600 | 1.856746 |
| Thin shell | 0.059743 | 0.551188 |
| Sphere intersection | 0.010891 | 0.012296 |

The single-region and thin-feature regressions additionally retain inconclusive
results at deliberately insufficient budgets. No increase in the default work
budget or relaxation of admission was used to make the target pass.

## Real compiler behavior and boundaries

`aetheris measure fixtures/Regression/Assertions/sphere.firmament --sdf --error 50
--json` now meets its target in 2,809 regions (actual half-width 49.964 mm³), versus
the previous exhausted million-region result. At `--error 4`, it meets the target
in 36,985 regions. `sphere-tight.firmament` passes both CLI validate and build with
a ±4 mm³ volume assertion; the assertion requests ±1 mm³ integration uncertainty.
The test also proves measurement/assertion evaluation leaves STEP text unchanged.

The CLI adds `affineCutRegions` and `intervalFallbackRegions`, counting visited
cells including subsequently refined parents. Existing method/authority fields and
the assertion rule requiring the **entire** interval to lie within tolerance remain.
Exact admitted measurements still take precedence unless `--sdf` is requested.

This qualifies the integrator for the supported analytic tape operations. It does
not promote every existing Boolean/display CIR mirror to final-material authority.
The production admission flag still covers qualified exported primitives. Imported
CTC-01 still lacks a qualified field for its complete trimmed solid. Deriving one
from its suspect STL would destroy independence, so no such fallback was added.
The actual CLI probe of `fixtures/Canonical/PMI/nist-ctc01-passthrough.firmament`
confirms `geometryBuilt: true`, volume `Unavailable`, `authoritative: false` and
exit 1, with the missing qualified CIR field explained in the diagnostic.
Extending final-operation CIR and independently qualified imported-body occupancy
is the next coverage task, separate from this numerical integration repair.

The enclosure contract assumes supported analytic field intervals/envelopes and
finite enclosing bounds. Floating-point padding is included, but this is not
formally outward-rounded interval arithmetic. Volume uncertainty is not a positional
tolerance, and correct field volume does not certify a watertight STL or correct
local geometry. Tighter targets may still exhaust the explicit work budget.

Code review also isolated an existing scalar cone projection shortcut at local
generator squared length ≤1e-18. At that scale its signed value can jump across
the wall, invalidating the distance/Lipschitz interval premise. Bounded measurement
now rejects it explicitly with `sdf-volume-cone-scale-unsupported`, and the affine
API declines that wall envelope. A regression demonstrates the discontinuity.
Ordinary engineering cones and their geometry are unchanged; repairing that
legacy scalar shortcut itself is outside this integration pass.

## Qualification

Focused tests cover independent axis/triangle/tetrahedron integrals, complementary
cuts, nearly axis-aligned cuts, transformed/reflected fields, both cylinder caps,
both frustum caps, expanding/contracting frusta, inner cavity negation, thin-feature
fallback, deterministic budgets, tight convergence and compiler/CLI propagation.
The sampled field-envelope tests check implementation behavior at corners and
interior points; the inequalities above, rather than sampling, motivate the bounds.

Final qualification:

- Release solution build: zero errors; existing nullable/analyzer/WASM trimming
  warnings remain (38 reported on the final build).
- Focused interval/envelope/bounded-volume lane: 25 passed.
- Focused compiler assertion lane: 17 passed; actual tight-assertion CLI validate
  and build both exit 0. CLI measurement JSON carries the new counters.
- Fast Core lane: 1,168 passed. Final full Core corpus lane: 1,356 passed.
- Final full solution gate: **6,316 passed, zero failed, two optional external
  PDF-renderer skips**, across twenty test assemblies. The full gate also covers
  canonical geometry/interchange and the editable V8.
- Managed/WASM SDK rebuilt; all nineteen JavaScript SDK tests passed.
- `git diff --check` passed; task-owned build/test/benchmark hosts exited.

Commands used the serial Release lane (`-m:1 -nr:false`, shared compilation off
for builds). The full gate used `--no-build -- RunConfiguration.MaxCpuCount=1`.
An initial complete green run is retained as `full-tests-initial.log`; the final
run includes the additional discontinuous-cone rejection regression.

Generated baseline/comparison JSON, actual CLI JSON/STEP and build/test logs are
under ignored `artifacts/local/sdf-volume/`. Public behavior is documented in
`docs/public/reference/language/checks.md`.

Code-quality review kept interval dispatch shared, primitive envelopes in the
field owner, plane integration in Continuum, and admission in the existing
measurement owner. There are no new dependencies, sampled authority, global
mutable caches or production strategy routes. Point evaluation and geometry
generation remain unchanged. The frozen old estimator exists only in the
qualification executable.

**Verdict: Meaningful progression.** The uncertainty-reduction bottleneck is
removed for the tested analytic field families, and the actual compiler/CLI can
use tight numerical intervals. The next blocker is precisely the final-body
field qualification seam: `RuntimeRootIsExportedPrimitive` currently admits
qualified primitives, while imported trimmed BReps lack a qualified occupancy
field. Extending those lowerers/queries is required before making SDF the general
volume check for CTC-01 or every edited engineering body. This pass establishes
the integrator's viability without bypassing that correctness boundary.
