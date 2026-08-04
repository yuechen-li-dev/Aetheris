# CHAMFER-FIXTURE-PRESSURE-M6 — bounded chamfer lowering report

## Outcome

The architecture generalizes across two exact outer-boundary construction families and reuses a third existing semantic-hole construction family:

1. rectangular `+Z` outer loop -> generated three-section witness -> `SectionTransition`;
2. right-cylinder `+Z` circular rim -> generated radial profile witness -> `RevolutionProfileRewrite`;
3. history-known through-hole entry -> semantic countersink stack constrained to a 90-degree, equal-radial/axial entry break -> `HoleProfileStack`.

This is meaningful bounded chamfer support, not universal edge finishing. Concave pocket loops, localized single edges, and interacting-edge junctions are rejected before BRep emission with typed errors.

## Fixture matrix

| Family | Semantic input | Construction witness/strategy | Result |
|---|---|---|---|
| Rectangular baseline | history-known box, complete `+Z` outer boundary, uniform distance | profiles at `z=0`, `z=H-d`, `z=H`; identity correspondence; `SectionTransition` | supported; authoritative prismatic BRepPlan |
| Circular convex rim | history-known right cylinder, complete `+Z` outer boundary | axis plus sharp profile `(R,-H/2),(R,H/2)` and replacement `(R,-H/2),(R,H/2-d),(R-d,H/2)`; `RevolutionProfileRewrite` | supported; authoritative revolved-profile BRepPlan |
| Internal circular entry | history-known `+Z` through hole; shaft `6`, entry `8`, angle `90` | conical entry radius `4` -> cylindrical shaft radius `3`; `HoleProfileStack` | supported through existing semantic-hole materializer; no unified AirBRepPlan yet |
| Rectangular concave pocket rim | complete inner rectangular loop | attempted outer host, opening inner loop, lowered/inset inner loop | `MissingConstructionWitness`: current section transition admits no holes/inner loops |
| Single straight convex edge | `+X/+Z` shared edge | attempted retained regions, bounded diagonal support plane, two trim boundaries | `MissingConstructionWitness`: localized retained/replacement region plan absent |
| Adjacent edge junction | two convex edges sharing a vertex | attempted two bounded support strips | `CornerPolicyRequired`: miter, setback, and explicit corner patch are all valid |

## Compiler changes

`ChamferLoweringResult<T>` is the Result-shaped boundary. Its error carries category, stable code, message, stage, and evidence. Rectangular and circular lowerers use this boundary. Deferred executable cases prove `MissingConstructionWitness` and `CornerPolicyRequired` without constructing a BRepPlan or body.

`RevolvedProfileStackEmitter` is the reusable construction primitive added by the circular fixtures. It consumes an authoritative plan for a bounded open piecewise-linear radial profile and emits one exact cylinder or cone per segment, full circular profile edges, planar end caps, stable counts, and a deterministic profile signature. It is deliberately not named after chamfer and does not replace the narrower public two-point `BrepRevolve` API.

The circular compiler generates the same witness an explicit desugared form would contain: axis, sharp profile, replacement profile, and material side. Build JSON exposes all of these. Firmament Concept Struct syntax was not expanded for radial profiles because this deterministic case does not need authored disambiguation; adding an unproven general radial-profile authoring language would be speculative. The typed witness remains the debug representation and regression oracle.

The Phase 3 parser now admits exactly one `Cylinder` plus one `EdgeFinish` in the same bounded syntax already used for the box route. It does not claim arbitrary cylinder selection or imported history.

The semantic-hole report now exposes `HoleProfileStack`, stack kind, generated witness summary, surface-family counts, STEP hash, and reimport status. This made an architectural inconsistency visible: the hole stack is exact and semantic, but it still materializes through its established specialized path rather than a shared authoritative `AirBRepPlan`. Converging that lane is follow-up work; it was not hidden behind a false plan claim.

## Geometry evidence

| Artifact | Topology V/E/F | Surfaces | Bounds | Exact volume | SHA-256 |
|---|---:|---|---|---:|---|
| rectangular top loop | 12/20/10 | 10 planes, including 4 changed chamfer faces | `[-5,-4,0]..[5,4,6]` | planar-shell path remains covered by baseline tests | `1A982C...74AE0` |
| cylinder top rim (`R=20,H=50,d=1`) | 6/5/4 | 2 planes, 1 cylinder, 1 cone | `[-20,-20,-25]..[20,20,25]` | `62770.068416275266` | `AB37BB...DE6C` |
| through-hole entry (`shaft=6,entry=8,90deg`) | 15/17/8 | 6 planes, 1 cylinder, 1 cone | `[-15,-12,-6]..[15,12,6]` | `8290.236017900337` | `F3B8AF...1E14` |

The analyzer now includes full-circle curve extrema in body bounds. This corrects the prior collapsed seam-vertex bounds for periodic circular topology. It also exactly integrates admitted piecewise-linear revolved profiles as cylinder/frustum intervals; no voxel approximation is involved.

All three retained artifacts reimport through `Step242Importer` as enclosed manifolds. Parameter changes produce different deterministic hashes and analytic dimensions. Zero and oversized circular distances fail before STEP creation. There is no legacy fallback.

## Independent CAD Assistant smoke

`cylinder-top-rim.step` loaded in CAD Assistant shaded-with-edges. Top-oblique and rotated side views showed a continuous annular conical band, a smaller planar cap, and an unchanged cylindrical wall. There was no four-edge approximation or relabel-only body.

`hole-entry.step` did not complete independent OCCT visualization. CAD Assistant remained at 50% and raised `OSD_Exception_ACCESS_VIOLATION` when the stalled operation was cancelled. This is a backend interoperability/visual-verification failure. It does not invalidate the separate Aetheris reimport and analytic topology evidence, but the fixture is not credited with an independent visual pass.

**A0 correction:** the follow-up investigation proved this was an Aetheris countersink BRep defect, not an OCCT defect. The source used disconnected/misordered periodic loops, incorrect inward face sense, and a conical support surface that did not contain its trim boundaries. The corrected `hole-entry-fixed.step` (`48376D...09B9`) opens and renders in CAD Assistant. See [HOLE-ENTRY-OCCT-INTEROP-A0](hole-entry-occt-interop-a0.md).

## False assumptions discovered

- A single universal chamfer algorithm is the wrong abstraction. Rectangular loops are section transitions; circular rims are profile rewrites and revolutions; hole entries are subtractive profile stacks.
- The previous two-point revolve limitation did not require generic BRep edge surgery. A bounded piecewise-linear revolution construction was sufficient and reusable.
- A support plane alone is not a single-edge construction witness. Retained and replacement face regions plus topology ownership are the missing information.
- Inner-loop section geometry is not merely a correspondence extension. BRepPlan must represent a face with holes and preserve host-versus-pocket material ownership.
- Junction difficulty is policy, not offset math: multiple valid corner patches exist.
- Successful Aetheris reimport is not equivalent to independent OCCT interoperability; the hole artifact exposed that gap.

## Fillet readiness

The Result boundary is ready for fillet lowerers. `SectionTransition` can inform constant-profile planar bevel-like families, but true fillets require tangent arc profiles and cylindrical/rolling-ball or swept blend surfaces. `RevolutionProfileRewrite` directly generalizes to circular-rim fillets once the profile emitter admits analytic arc segments and toroidal/surface-of-revolution bands. `HoleProfileStack` similarly generalizes when its radial stack admits tangent arc transitions.

The shared hard problem is corner/junction resolution and retained/replacement region topology. Fillets add continuity constraints and new curve/surface families; they do not remove the need for explicit corner policy.

## Recommended next step

Implement `LocalizedPlanarReplacement` as an authoritative retained/replacement region BRepPlan for one history-known straight convex edge, with explicit support plane, trim boundaries, material side, and end policy. Do not start with multi-edge corners. In parallel, converge `HoleProfileStack` onto a shared BRepPlan seam and investigate why CAD Assistant crashes on its AP242 hole artifact. After one-edge replacement is proven, add a `SupportSurfaceDependencyGraph` plus explicit corner-patch policy for interacting edges.

## Validation record

- `dotnet restore Aetheris.slnx`: succeeded; all projects up to date.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: succeeded with no compiler errors. The JavaScript project reported its existing package-audit warnings.
- Firmament requested filter (`Chamfer|Air|Concept|Construction|BRepPlan|SectionTransition|Revolve|Hole|Step`): 413 passed, 0 failed.
- CLI requested filter (`Chamfer|Air|Concept|Step|Analyze`): 235 passed, 0 failed.
- Focused M6 tests: Firmament 18 passed; CLI 7 passed.
- `dotnet run --project Aetheris.CLI -- --help`: succeeded.
- `git diff --check`: succeeded.
- Real CLI build/analyze ran for all five valid fixtures. All were enclosed manifolds and independently reimported; hashes for both parameter variants differ from their canonical cases.
- Three invalid fixtures exited 1, contained their expected diagnostic, and created no STEP file.

An additional Core test run with the broad substring `Step` selected the entire NIST snapshot audit: 415 passed and 17 failed because canonical SHA snapshots differed while topology counts remained equal. None of this milestone's changes touch NIST canonical serialization or its snapshots. The milestone-required Firmament and CLI suites are clean, but the unrelated snapshot drift is recorded rather than hidden.
