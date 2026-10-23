# Checks and measurement

## What `validate` and `build` check

`validate` runs the full engineering compilation in memory and writes nothing. A
valid result means the part would build. `build` does the same and writes the
artifact. Both evaluate requirements and assertions.

- `--parse-only` stops after static admission and reports `ParsedNotBuilt`. That
  proves the source parses and binds, not that it builds. Domains without a
  static path reject the option as `NotValidated` instead of quietly running a
  full build.
- `--explain` adds developer detail: which front end admitted the file, lexical
  scopes, bound arguments.
- `--json` gives a machine-readable report. See [diagnostics](../diagnostics.md).

Two domains differ:

- **Analysis.** `validate` binds geometry, material, selections and settings and
  reports `BoundNotSolved`; binding does not promise the solver converges.
  `build` (or the dedicated `fea --out-dir`) solves and writes the analysis
  artifacts to the output directory.
- **Drawings.** `validate` and `build` dispatch to the drawing compiler, which
  writes a drawing artifact directory; `drawing compile --out-dir` is the
  dedicated command. [Automatic drawing generation](../../drawings.md) needs a
  supported dimension plan; `--reference-only` explicitly asks for an incomplete
  visualization.

Asking an analysis or drawing build for a `.step` output rejects. Export the
geometry from its own source.

## Assertions

Assertions are available in mechanical parts built from primitives, extruded
Profiles and Compose. Other domains, including Revolve, reject them rather than
accepting the block and ignoring it.

Assertions check the final exported engineering body. They do not alter geometry,
add PMI, or specify a manufacturing tolerance. Normal `validate` and `build` both
enforce them. A failed assertion or an assertion that cannot be verified rejects
the build; it is never a successful build with a hidden `passed: false` result.

**Complete file — exact dimensions and topology:**

```firmament
Model CheckedBox {
    units: mm
    Box Body { size: [10mm, 8mm, 6mm] }
    Assert Volume Body { expected: 480mm^3; tolerance: 0mm^3 }
    Assert Bounds Body {
        min: [-5mm, -4mm, 0mm]
        max: [5mm, 4mm, 6mm]
        tolerance: 0mm
    }
    Assert Topology Body { faces: 6; edges: 12; vertices: 8; shells: 1 }
}
```

`Assert Volume` accepts finite nonnegative `expected` and `tolerance` literals in
`mm^3`, plus an optional string `note`. Exact BRep recognizers take precedence.
Otherwise, a compiler-retained analytic SDF qualified against the exported primitive supplies an adaptive occupied
volume interval. The entire interval must lie inside `expected ± tolerance` to
pass. A disjoint interval fails; an overlapping but inconclusive interval rejects
as **unverified**, including when the deterministic subdivision budget runs out.
The midpoint is an estimate, not an exact volume. Display-mesh mass estimates do
not gate an assertion. A part without an admitted exact recognizer or complete
SDF reports that limitation instead of silently accepting its volume assertion.
The SDF assertion route currently covers single Box, Cylinder, Cone, Sphere and
Torus primitives. A display-only or legacy Boolean mirror is insufficient evidence
that the field matches exported material, and is not promoted to volume authority.

`Assert Bounds` compares exact world-space minima/maxima, using the optional
nonnegative length `tolerance` (default 0mm). Exact bounds currently cover qualified
rectangular plate/box stock with Z-through holes and planar straight-edge bodies.
It rejects other families rather than treating sampled points or a Boolean field's
conservative enclosing box as the actual extrema. `Assert Topology` checks one or
more nonnegative integer `faces`, `edges`, `vertices`, `shells` counts in the exported
BRep. Counts describe kernel topology, so use them deliberately when topology itself
is the regression contract. All three families accept `note`. Historical PascalCase
field aliases remain accepted; new source uses camelCase.

The target must name the uniquely identified exported product. Requesting an earlier
body in a multi-body program does not measure the last body under the earlier name.
## Tolerance stack-ups

Assembly `Assert ToleranceStackup` checks the worst-case minimum along a unique
dimensional relation path:

```firmament
// Inside an Assembly with these semantic endpoints and dimensional relations:
Assert ToleranceStackup AxialReach {
    between: [BearingModule.FixedSupport.Housing.Datum, BearingModule.Rotor.Shaft.Shoulder]
    require: clearance >= 44.90mm
}
```

The assembly compiler sums nominal values and asymmetric tolerance intervals,
reversing their signs when traversing a relation backwards. Unresolved endpoints,
missing or ambiguous paths, and a minimum below the requirement reject compilation.
This checks declared dimensional relations; it does not infer a tolerance chain
from spatial proximity, perform statistical RSS analysis, or create PMI. Use typed
dimensions and explicit PMI for manufacturing requirements. `Between`, `Require`
and `Clearance` remain compatibility aliases. Unknown or duplicate fields fail.
See [assembly authoring](../../firmament/assembly-authoring-foundation.md).

## Measuring

To discover measured values, including while fixing a failed assertion:

```sh
aetheris measure checked-box.firmament --json
aetheris measure sphere.firmament --sdf --error 50 --compare-mesh --json
```

`measure` admits and builds the geometry but explicitly reports `MeasuredNotAsserted`
and `assertionsEvaluated: false`. It prints measurement methods, volume interval and
uncertainty, exact bounds where available, topology counts, and copyable assertion
templates. `--error` is a requested absolute volume error in mm³, subject to the
bounded work budget (at most 1,000,001 field regions and 30 subdivision levels).
`budgetExhausted` discloses when the requested error could not be reached; the
reported interval remains usable at its actual uncertainty. Suggested tolerances cover numerical uncertainty; select the
design's acceptance range independently. `--compare-mesh` exposes the older mass
estimate for diagnosis, never as a substitute assertion authority.

For assembly values use `aetheris asm inspect assembly.firmament --json`; its
`assemblyIr.toleranceStackups` contains nominal/minimum/maximum, requirement,
pass/fail, and the signed contributor chain. Unlike `measure`, assembly inspection
evaluates declared stackup assertions and returns failure when one fails.
