# Firmament assertion qualification — 2026-10-19

## Contract and ownership

`validate` and normal `build` enforce assertions after engineering materialization
and export. An unsupported measurement, insufficient numerical precision, or an
unidentified target rejects admission. A failed, non-authoritative mesh comparison
is no longer silently admitted.

The shared AST binds `Assert Volume`, `Assert Bounds`, and `Assert Topology` using
the existing field-consumption owner. Both camelCase fields and historical
PascalCase aliases work. Unknown fields, duplicates, invalid dimensions and
unsupported assertion kinds fail. Uninstantiated Template/Feature definitions are
not mistaken for executed body assertions. A dedicated frontend that does not bind
an authored assertion must reject it rather than erase it.

`FirmamentMeasurements` owns the common CLI/assertion measurements of the exported
STEP BRep. JudgmentEngine selects an admitted exact volume recognizer first, then
a qualified SDF interval. The existing mass-properties tessellator remains an
explicit diagnostic comparison. No display-mesh estimate is promoted to authority.

`aetheris measure part.firmament --json` is an explicit inspection boundary. It
still parses, binds and constructs geometry, but reports `MeasuredNotAsserted` and
`assertionsEvaluated: false`. It can inspect a failed numerical assertion without
pretending that validation succeeded. Its suggested assertions expose measured
values and numerical uncertainty; they are not manufacturing acceptance criteria.

## Reproduced defects and repairs

| Finding | Observed baseline | Repair / qualification |
| --- | --- | --- |
| Non-authoritative volume failure | Build discarded failed assertions unless `MeasurementAuthoritative` was true | Every failed/unverified assertion rejects normal compilation |
| Midpoint-only comparison | A numerical midpoint could fit a tolerance even when its uncertainty did not | Entire measured interval must fit expected ± tolerance |
| Volume field spelling | Regex owner recognized old PascalCase line-shaped fields | Shared AST fields, casing aliases, closed field consumption |
| Assertion target | One exported body was measured regardless of the requested name | Resolve admitted material symbols and require the identified exported product; executor Profile names do not replace enclosing Struct identities |
| SDF cone classification | Eight outside corners could hide a negative interior | Conservative center-distance ± half-diagonal interval for the finite-cone distance function |
| Bounds | No source assertion contract | Exact rectangular-stock or planar straight-edge extrema only; unsupported curved extrema fail |
| Topology | No source assertion contract | Counts come from exported/reimported BRep topology |
| Through-hole plate volume | General prism recognition declined the exported cylindrical seam | Reuse the drawing owner's complete rectangular-plate query: stock area minus disjoint circular holes, times thickness |
| Stackup authoring | Regex Between/Require fields, ignored extra fields and generated locations | Shared AST binding, closed fields, camelCase aliases and mapped source spans |
| Stackup failure output | Assembly STEP export replaced the original failure with generic text, even under `--json` | Preserve compilation diagnostics and structured JSON before attempting export |
| SDK severity | Validation severity `error` projected as Info | Project `error` as Fatal alongside `fatal` |

## SDF evidence and limits

The bounded estimator classifies cells with analytic field intervals. Interior
cells contribute their full volume; mixed cells contribute [0, cell volume]. It
refines the largest unresolved cell deterministically, with explicit region and
depth limits. It does not claim sampling convergence as a certified error bound.
The implementation includes roundoff guards; it is not formal outward-rounded
interval arithmetic.

For a radius-10 mm sphere, with `--sdf --error 50 --compare-mesh`:

| Measurement | Volume, mm³ |
| --- | ---: |
| Independent analytic 4πr³/3 | 4188.790204786391 |
| SDF midpoint | 4188.237190246582 |
| SDF lower / upper | 4109.301567069637 / 4267.172813423527 |
| SDF half-width | 78.93562317694523 |
| Old display-mesh estimate | 4070.552360820173 |

This reached the 1,000,001-region cap, so `budgetExhausted: true` is required:
the requested ±50 mm³ was not reached. The midpoint improves this example's error
from about 2.82% to 0.0132%, but the defensible SDF interval is about ±1.88%.
Increasing the cap from 100,001 narrowed the original ±252.26 mm³ envelope without
loosening the specimen's assertion. A ±200 mm³ assertion passes; a tight or zero
tolerance cannot pass merely because the midpoint looks plausible.

Compiler SDF volume admission currently covers single Box/Cylinder/Cone/Sphere/Torus
primitives whose retained field is tied to that same exported primitive. Display
qualification alone does not establish occupied-volume parity. Legacy Boolean
mirrors are excluded: their execution/placement compatibility paths need their own
parity qualification before they can certify exported material. Generic field
Boolean interval arithmetic is tested separately, including a spherical cavity.

## ToleranceStackup: no solver rearchitecture required

The existing Assembly compiler resolves a unique dimensional relation path and
sums nominal values and worst-case asymmetric intervals. Reversed traversal swaps
and negates lower/upper contributions. This pass changes authoring admission and
diagnostic propagation, preserving that solver and its contributor provenance.

Canonical spelling:

```firmament
Assert ToleranceStackup AxialReach {
    between: [BearingModule.FixedSupport.Housing.Datum, BearingModule.Rotor.Shaft.Shoulder]
    require: clearance >= 44.90mm
}
```

The bearing witness remains nominal 45 mm, minimum 44.92 mm, maximum 45.10 mm.
Reversed traversal yields [-45.10, -44.92] mm. Missing/ambiguous paths and an
unsatisfied minimum reject compilation. `aetheris asm inspect assembly.firmament
--json` exposes the same solver's numbers and complete signed contributor chain,
including failing results where the compiler retains IR. No duplicate solver or
statistical RSS interpretation is introduced. Relations must be explicitly
declared or supplied by admitted Interface fit evidence; proximity is not a chain.

The through-hole plate witness uses 80×50×8 mm stock and an 8 mm diameter hole.
The independently computed occupied volume is 32000−128π mm³, with unchanged
stock bounds [-40,-25,0]–[40,25,8] mm. Its source asserts that volume within
1e-8 mm³, bounds, and one shell. No mesh or construction-history formula supplies
the measurement: the existing BRep query first accounts for every exported face,
loop and edge and rejects intersecting, blind or overlapping holes.

## Historical fillet specimens

These six files remain unchanged under `fixtures/Regression/CanonicalGeometry/`:

- `profile-fillet-convex-two-segment-top.firmament`
- `profile-fillet-convex-two-segment-bottom.firmament`
- `profile-fillet-reflex-two-segment-top.firmament`
- `profile-fillet-reflex-two-segment-bottom.firmament`
- `profile-edgefinish-chimera-fillet.firmament`
- `profile-edgefinish-chimera-reflex-sphere-compat.firmament`

Their geometry is constructible, but their authored volume assertions lack an
admitted exact recognizer or matching retained SDF. Ordinary validation/build now
rejects them with `firmament-v2-assert-volume-measurement-unavailable`. This is an
intentional compatibility correction, not a geometry failure or a proved design
mismatch. The first complete regression run exposed eight old success assertions
across these six sources.

Their geometry tests now explicitly require that rejection, inspect the unchanged
specimen through `CompileForMeasurement`, and compare STEP bytes with an admitted
control that masks only assertion syntax. Existing manifold, orientation, surface
type and determinism checks remain. No expected volume, tolerance, topology count,
or source specimen was edited to make the new assertion gate pass. General curved
fillet volume authority remains a specific qualification gap.

## Qualification

Final results:

| Check | Result |
| --- | --- |
| Release solution build | Passed; 0 errors, 32 warnings |
| Focused assertion/stackup/fillet qualification | 57 passed |
| Focused CLI measurements and validate/build/inspect agreement | 5 passed |
| Focused bounded SDF tests | 6 passed |
| Fast Core lane | 1,087 passed |
| Complete solution lane | 5,695 passed, 0 failed, 20 suites |
| Complete Core suite, including slow corpus | 1,220 passed |
| Complete Firmament suite | 2,966 passed |
| Complete CLI suite | 631 passed |
| Sheet metal / editable V8 | 164 / 7 passed |
| Whitespace / source preservation | `git diff --check` passed; existing canonical and geometry-regression source files unchanged |
| Runtime-host cleanup | No dotnet, testhost, MSBuild or VBCSCompiler processes remained after qualification |

Raw logs and CLI JSON remain under ignored `artifacts/local/assert-*` paths.
The sphere interval and through-hole plate values above were also checked through
the built CLI. Assembly inspection of `template-block-pair.firmament` reports
TemplateFitTransition nominal/minimum/maximum 2 mm and the Interface-derived
contributor with its Template/Record/occurrence provenance. CLI regression tests
raise the requirement to 3 mm and verify rejection plus the authored assertion span
through all three normal commands. Part diagnostics retain their authored line
even when an unused Record is erased before lowering.

Reproduction commands (serial, with MSBuild node reuse and shared compilation off):

```powershell
$env:MSBUILDDISABLENODEREUSE='1'
$env:DOTNET_PROCESSOR_COUNT='2'
dotnet build Aetheris.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build -m:1 --filter 'Category!=SlowCorpus'
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll measure fixtures/Regression/Assertions/box.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll measure fixtures/Regression/Assertions/sphere.firmament --sdf --error 50 --compare-mesh --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm inspect fixtures/Canonical/Assembly/template-block-pair.firmament --json
```

The code-quality pass checks shared validate/build admission, no diagnostic
fatality allowlist, no new regex dispatch/rewrite, preserved geometry authority,
closed AST fields, and explicit numerical budgets. The exact-volume query reuses
the existing recognizers without running their mesh fallback. Existing full mass
queries retain their independent surface-area/centroid completion behavior.

## Verdict

**Success for this bounded assertion pass.** Volume, bounds and topology assertions
now gate the real engineering path, the CLI exposes the numbers and uncertainty,
and ToleranceStackup uses shared AST admission while preserving its existing
worst-case solver. Geometry qualification remains intact. Unsupported measurement
families reject honestly; this pass does not claim a general curved-BRep volume
integrator, Boolean-mirror parity, or dedicated-domain assertion support.

## Follow-up: historical fillet assertion cleanup

The subsequent UI measurement pass replaces the six unsupported historical volume
assertions with a provable single-shell topology assertion. These fixtures now
compile through normal admission; their existing detailed geometry regressions
remain. `HistoricalFilletGeometryBuildsButAnUnprovableVolumeAssertionStillRejects`
retains a separate rejection witness and STEP parity comparison. This supersedes
the earlier decision to leave these six fixtures rejected, without claiming a
new exact curved-fillet volume capability.
