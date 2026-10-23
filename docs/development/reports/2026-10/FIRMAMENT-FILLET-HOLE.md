# Firmament Box top-boundary Fillet + Hole qualification

Author: Codex. Date: 2026-10-15 (America/Los_Angeles).

## Request and scope

After the FrontendX4 closeout, the user explicitly authorized fixing the rejected
Fillet + Hole combination. This supersedes the earlier diagnostic-only boundary
for that case. The original Claude source
`fixtures/Regression/FrontendX4/cases/d05-fillet-with-hole.firmament` is unchanged.

The qualified construction is one Box, one Modify, one complete +Z Boundary
Chamfer or constant-radius Fillet, and mutually disjoint top ThroughAll shaft or
counterbore holes. Finish-only top-boundary Fillet uses the same materializer.
Other combined selectors, blind holes, countersinks, Region operations, mixed
finishes and intersecting/tangent hole/finish footprints remain explicit limits.
This is a bounded engineering capability, not general feature-history composition.

## Root cause and repair

The prior combined route admitted only Chamfer and consumed a chamfer-specific
final plan. Removing its admission check would not have constructed a fillet.

The existing combined owner is now
`CombinedTopBoundaryEdgeFinishThroughHoleBuilder`, with an explicit finish kind.
Its hole and counterbore topology is shared. Chamfer realization retains its
existing surfaces and entity ordering. Fillet realization emits four exact
cylindrical strips and four elliptical intersection edges. This extends the
direct cylinder-intersection construction already used by the two-edge fillet
compiler; it does not substitute a chamfer or invent spherical corner patches.
The four vertical stock corners remain sharp. No parser dispatch, source rewrite,
new spelling or general Boolean engine was added.

For a blend radius `r`, in the upper stock band the inset is
`s(t) = r - sqrt(r*r - t*t)`, `0 <= t <= r`. The retained cross section is
`(width - 2*s) * (depth - 2*s)`. The plan integrates this section analytically,
including corner overlap, then subtracts shaft and counterbore volumes.
Tests independently integrate these sections and sample each seam on both of its
incident cylinder supports. Binding preflight and AP242 round trip check the
real emitted body. This is more than a success-code test.

Analytic preflight exposed reversed orientation flags on the existing longitudinal
hole seam bindings. Those bindings now reflect their stored curve direction.
The STEP exporter already normalizes line direction against topology; emitted
geometry compatibility is qualified separately below.

The combined correspondence now identifies all four finish replacement faces.
Counterbore wall, annular shoulder and shaft face ordering is accounted for;
the shoulder is no longer mislabeled as a shaft wall. Exact runtime body and
correspondence accompany the export. Model summaries identify the document owner.

## Admission and diagnostics

Validate and build retain their shared AST admission/build path. A hole entry
circle must lie strictly inside the inset top face and remain disjoint from
other entry circles. The larger counterbore radius determines entry clearance.
The finish radius must be positive, smaller than the stock height and smaller
than half the width and depth. Invalid requests cannot emit a partial body.

Unsupported combined selectors retain `CombinedFeatureSelectionLost`. Interacting
footprints report `CombinedFeatureInteractionUnsupported` and explain how to
restore clearance. Invalid plan dimensions retain `CombinedFeaturePlanChainInvalid`
detail. Finish failures retain the EdgeFinish source span; a hole materializer
failure retains that Hole's span. The corpus and focused tests check acceptance,
failure codes/lines, body topology, exact surface families, deterministic STEP,
semantic descendant identities, stock placement and explicit AP242 PMI.

## Corpus and compatibility decisions

Only d05's manifest disposition changes from invalid to valid. Its newly admitted
geometry baseline has 11 faces, 23 edges, 14 vertices, six planes, five cylinders
and four elliptical curves, with bounds `[-30,-20,0]` to `[30,20,10]` mm.
Other FrontendX4 geometry baselines and source specimens are unchanged by this
follow-up. The recorded numerical verification volume is a deterministic
regression fingerprint, not exact mass authority. The analytic plan volume and
sampled surface evidence have separate tests. Interacting requests replace the
old blanket rejection as negative capability witnesses.

For d05 the analytic volume is `23454.80828946461 mm³`. The existing coarse
`verify` triangulation reports `23460.317970712236 mm³` as `NumericalWithBound`;
that estimate is not promoted to an exact geometry claim. No mass evaluator or
tessellation tolerances were changed to obtain acceptance.

The first full regression run identified two X0 assertions that required the
now-supported original `combined-fillet-hole` witness to fail. Those assertions
now use the new `combined-fillet-hole-intersection` negative witness; the
original remains unchanged and has an explicit normal-CLI acceptance test.

The new readable multi-hole witness is
`fixtures/Regression/Fillet/top-boundary-with-counterbore.firmament`.
The public syntax, geometry, support matrix and diagnostic guides describe the
qualified combination and its remaining limits. The language decision register
preserves the historical #24 deferral and records this bounded superseding result.

## Upstream reconciliation

Fetched `origin/master` and fast-forwarded local master from `7525f37f` to
`1585f637` (`Language outline: declaration graph for visual editors (G0)`).
The upstream change had no overlapping files and merged without content conflicts.
All pre-existing uncommitted frontend changes were retained. No local commit or
remote push was created. An empty stale Git index lock was preserved under
ignored local evidence after verifying its age and the absence of Git workers.

The merged solution build, full .NET lane and upstream SDK language transport
tests are part of qualification; this is not an untested post-qualification merge.

## Qualification

Final qualification ran after the upstream fast-forward and the X0 expectation
updates. Generated logs and exported bodies live under ignored
`artifacts/local/fillet-hole/`. Durable hashes and results are in the
[evidence manifest](../../../release/FIRMAMENT-FILLET-HOLE.evidence.json).

| Check | Measured result |
| --- | --- |
| Serial Release solution build | Passed, zero errors; existing warnings remain |
| Core fast lane (`Category!=SlowCorpus`) | 1,076 passed |
| Focused frontend/semantic/outline qualification | 92 passed |
| CLI X0 honesty tests | 13 passed |
| Complete serial .NET solution regression | **5,140 passed, 0 failed, 0 skipped**, 20 executing assemblies |
| Claude FrontendX4 authoring corpus | **32/32 passed**, including unchanged chimera/reference geometry |
| Frozen showcase AP242 artifacts | **26/26 byte-identical**, source hashes checked against the existing X1 baseline |
| Existing scoped X2 positive/negative dogfood replay | 9/9 retain their intended outcome |
| Upstream SDK language transport tests | 3/3 passed; includes outline transport; these are client contract tests, not browser execution |
| Final independent normal-CLI witnesses | Original d05, shaft/counterbore and original X0 models validate/build/import/verify; intersecting witness rejects with code and line preserved |
| Runtime host cleanup | Zero dotnet, testhost, MSBuild, VBCSCompiler or aetheris processes remain |
| Whitespace check | `git diff --check` passed |

The FrictionLab project has no discoverable tests; it is not included as an
executing test assembly. No full-suite evidence is inferred from the fast lane.
The first full-run failures and the final passing run are kept separately in
local evidence.

## Code-quality review and verdict

Reviewed the modified admission, construction and diagnostic code for new parser
routes, source rewrites, fatal-code allowlists, duplicate field schemas, mutable
global state and geometry changes outside the qualified family. None were added.
The finish-kind dispatch is deterministic; no strategy-scoring framework is
needed. The same bounded builder admits and realizes finish-only and combined
top-boundary fillets, and the existing AST binders still own fields and spelling.
The plan/type rename reflects the two admitted finish families, with repository
callers migrated together. Source failures retain their causal messages and
the appropriate enclosing feature span.

**Success within the stated engineering boundary.** The original source now
constructs both the requested fillet and hole. Validate/build agree, exact
supports survive AP242, supported explicit PMI survives, the full merged
regression passes, and the frozen showcase geometry stays byte-identical.
General feature-chain composition and interacting hole/fillet trimming are
still separate work; they are not hidden behind successful partial geometry.
