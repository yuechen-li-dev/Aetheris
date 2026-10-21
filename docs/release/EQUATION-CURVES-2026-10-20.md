# Equation-driven curves — typed Function foundation

## Outcome and language decisions

The implementation extends the existing pure Function binder. No Equation alias,
second expression interpreter, whole-document parser route, host-code evaluator
or geometry solver was introduced. Functions admit Point2/Point3 and typed Record
inputs/results alongside their existing scalar types. Record schemas remain owned
by the existing compile-time value catalog. Curve2/Curve3 bind typed point laws to
finite dimensionless parameter domains.

Bare Sin/Cos arguments are radians. Explicit rad arguments are radians and explicit
deg arguments are degrees. Lengths reject. The shared numeric evaluator owns these
intrinsics; curve and Pattern mathematics use the same semantics.

Curve2 owns its construction plane. A Profile with no explicit placement inherits
the shared plane of its equation curves; conflicting explicit/mixed planes reject.
Curve3 currently uses definition-local World. At(t) evaluates the original point
law, checks the declared interval and retains Point2/Point3 dimensionality. It does
not query the tessellation or silently reinterpret t as arc length.

## Geometry admission

Exact polynomial lowering uses coefficients, not a fitted sample cloud, for degree
at most three. Supported full circles/ellipses are recognized structurally from
matching affine Sin/Cos phases and rates. A nearly complete turn is not silently
closed under an epsilon comparison. Other Exact requests reject explicitly.

Approximate requires maxDeviation in Length units. Its first native representation
is a polyline. Outward-rounded interval jets enclose derivatives over each complete
subinterval; the chord-error bound includes endpoint evaluation uncertainty.
Trigonometric endpoint enclosures include range-reduction and Taylor remainders.
Independent interior samples test the established bound, but do not establish it.
Failure to bound a law, singularity, non-finite geometry or budget exhaustion is an
error, never a partially emitted curve.

Bounds: 128 curves, a shared 4096-segment approximation budget, 24 subdivision levels,
existing expression/dependency limits and 10000 AST binding work items per
Function/point evaluation. Manufacturing tolerance is not a representation budget;
unsupported tolerance consumption fails instead of erasing the requirement.

closed requires endpoint agreement and inserts no segment. periodic additionally
requires first-derivative agreement, initially proved for polynomials and recognized
exact full conics. Open curves and explicit baseline geometry remain distinct.
The ordinary Close pipeline checks closure; it does not draw a missing chord.

## Ownership, identity and provenance

ConceptFunctionBinding performs typed AST argument substitution and record/point
projection. FirmamentNumericEvaluation remains scalar arithmetic/unit authority;
its curve analysis adds coefficient and interval derivative proofs. The curve owner
locally lowers checked Curve2 nodes to existing construction guides and Profile
pipelines. Existing profile validity, extrusion, topology and STEP owners remain
responsible for engineering construction.

Stable curve identities derive from named Concept members. Generated guide names
are deterministic and collision-checked. Inspection retains domains, frames,
equations, representation, source declaration spans, local segments, error bounds
and the generated-guide correspondence. The ordinary bound Profile path restores
curve source provenance on its resolved segments. Keyed At(t) samples retain the
existing Points/Pattern identity model.

CLI inspect --json exposes equationCurves. New binding errors use the existing
structured compile-time diagnostic chain, including original span mapping.
An invalid ellipse open-chain consumer initially exposed an exception in the older
profile owner; local curve admission now rejects it with a TraceLoop suggestion.
That failure remains covered by a regression test. The quality pass also reproduced
and closed Record/Point constructor projection bypasses: all fields/coordinates
must be admitted before member projection. Scalar Function normalization rejects
typed tolerance inputs instead of stripping their requirements. Curve parameters
shadow unrelated ordinary, tolerated and Record Statics.

## Qualification

Final evidence is under ignored artifacts/local/equation-*.log and
artifacts/local/equation-curves/. The final qualification uses the completed logs from the frozen implementation.
Earlier runs were interrupted while additional invalid-input defects were repaired;
those partial runs are not counted as full-suite evidence.

- Release solution build: passed.
- Focused Function/curve/point/Pattern checks: 86 passed.
- Fast kernel lane: 1087 passed.
- Full serial solution gate: 5808 passed across 20 projects, zero failures/skips.
- CLI: 5 complete equation fixtures pass validate/build/inspect and export STEP; additional XZ and spaced-degree controls pass. All 38 preexisting Pattern specimens retain admission (28 admitted, 10 rejected), with validate/build/export agreement. All 56 frozen guitar leaf STEP hashes and 99 material occurrence identities/transforms are unchanged.

The exact arch is built in XY and XZ, exported and reimported through STEP. Tests
sample actual imported B-spline edges at their midpoints: X=40 mm, crown height
20 mm, with extrusion bounds Z=0..5 mm in XY or Y=-5..0 mm in XZ. They also check
closed/oriented topology and exact nominal profile area. The typed Record arch
retains the same curve controls. The sine arch exercises a materialized bounded
approximation; the spatial helix exercises keyed original-equation queries driving
an 8 mm physical Box dimension. Circle/ellipse tests retain analytic geometry.

The existing generic STEP analyze boundingBox summary is based on topological
vertices and does not establish a curved crown's tight extrema. The existing
measure volume recognizer also does not admit this cubic-profile extrusion.
Neither output is used as false evidence of the arch's dimensions or volume;
qualification uses imported curve evaluation and resolved profile area instead.
Those measurement/reporting gaps are outside this curve-language pass.

## Explicit limits and follow-ons

- No general Curve3 sweep, arbitrary frame consumer, arc-length inversion or
  automatic material creation is implied by declaring a spatial curve.
- Exact lowering is bounded; general analytic recognition, partial conic trimming,
  arbitrary polynomial degree and a universal transcendental exporter are absent.
- Approximation admits arithmetic, Sin/Cos and bounded integer Pow exponents.
  Other scalar expressions may evaluate at a point yet lack an admitted curve bound.
- General periodicity, higher-order continuity contracts, derivative query APIs,
  implicit equation solving and general-purpose collection execution remain absent.
- New Function result shapes do not grant every legacy Static/Template initializer
  a new consumer. The qualified path is typed Function composition into curves,
  Points/Series and supported direct geometry expressions.
- UI curve authoring and visualization controls were not expanded in this pass.

Public authoring documentation: [equation curves](../public/firmament/equation-curves.md)
and [syntax](../public/firmament/syntax.md). Complete authored sources are in
fixtures/Canonical/EquationCurves; negative and cross-route checks are in
EquationCurveTests. No user decision is required for the bounded implementation.

## Code-quality closeout and verdict

The modified owners were checked for duplicate evaluators/schemas, hidden parser
fallback, global mutable compilation state, unconsumed constructor fields and
unit/tolerance erasure. No new regex, document-dispatch route, host-code execution
or fatality allowlist was added. Record schemas and numeric units remain with their
existing owners. Documentation links and git diff --check pass. Final test hosts,
MSBuild hosts and compiler servers have exited; no task-owned runtime host remains.

**Success.** Typed reusable laws drive real planar material and spatial keyed
geometry inputs through the normal compiler. Exact and approximate representations
are explicit, malformed intent fails, existing corpus admission remains consistent,
and the frozen engineering geometry control is unchanged. The spatial sweep and
other bounded consumer limits above remain explicit rather than implied support.
