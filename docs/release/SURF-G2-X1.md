# SURF-G2-X1: normalization and geometric continuity evidence

## Executive verdict

**Meaningful progression, not G2 acceptance.** The ordinary Firmament SectionChain path now admits circular arcs to smooth G1 construction through bounded, inspectable, non-rational normalization. A generic mixed line/arc witness exports deterministic AP242, reimports as enclosed-manifold, and exposes its actual geometric curvature discontinuities. The original arc-admission blocker is removed.

No new curvature-constrained transition family is delivered. `Continuity: G2` remains a typed rejection. Planar-step, convex, concave, and rounded-plateau G2 witnesses have not passed; the phone burn-in and fresh-agent plateau-authoring exercise therefore did not run. A successful normalized G1 solid is not substituted for those acceptance requirements.

## Authority audit

| Question | Finding |
|---|---|
| Curves entering authored SectionChain | `SectionChainAuthoringParser.Convert` admits ordinary Profile lines and circular arcs. |
| Smooth G1 inputs before this change | `SmoothSectionChainBuilder.ExtractCurveData` admits lines and compatible polynomial B-splines; matching degree/multiplicities/knots are required. |
| Arc rejection | `SmoothSectionChainBuilder.ValidateCompatibility` previously emitted `section-chain-g1-degree-limit` because arcs did not have polynomial controls. |
| Existing polynomial representation | `SectionProfileCurve.PolynomialBSpline`, `BSpline3Curve`, and tensor `BSplineSurfaceWithKnots`; the normal BRep/AP242 path already lowers these without rational weights. |
| Existing derivative data | The smooth builder derives first derivatives of corresponding world-space controls from a nonuniform three-section stencil, then constructs cubic longitudinal patches. It has no parent-support second-order boundary contract. Curve first/second derivative evaluators already exist and are reused by the new inspection. |
| Existing G2 authority | `BlendBoundaryOperation` already supports a bounded rectangular crown-to-planar-shoulder polynomial family. `ReplaceRegion` checks its planar boundary contracts; `ConstructionStateReplayer` replays the operation transactionally. This is not a raised flat plateau with curved footprint. |
| Other continuity utilities | `PanelMateValidator` uses normal-curvature queries; `CurvatureQuery` handles first/second fundamental forms. The new SectionChain inspection uses the same geometric principle on its realized tensor-spline derivatives, rather than comparing raw parameter accelerations. |
| Semantic location of future G2 | Extend a typed support/boundary contract in the existing transition/construction authority. Do not manufacture G2 intent from normalized curve controls or accept a label in the parser alone. |
| Minimum remaining representation | A bounded longitudinal second-order boundary law, parent Plane/Cylinder-derived tangent/curvature constraints, selected join identities, and a deliberate treatment of line/arc footprint curvature discontinuities. Parent-face trimming and construction correspondence must accompany the law. |

SURF-X3b's `AddSectionChain` and `RemoveSectionChain` remain bounded housing operations, not arbitrary plateau grafts. The existing `BlendBoundary` generates a polynomial crown which returns to a planar shoulder on four boundaries; it has no flat elevated terminal plateau or rounded outer transition band. Reusing its G2 label would not make it the requested witness.

## Profile normalization

`SectionProfileNormalizer` runs only for smooth SectionChain construction, after semantic correspondence validation. Authored profiles remain unchanged. Prepared edges and transition surfaces both use the same normalized curves, so pcurves and shared topology are not asked to reconcile an exact circle edge with a nearby polynomial surface.

Circular arcs use quintic Hermite polynomials matching endpoint position, first derivative, and second derivative. Segment choices are the fixed set 1, 2, 4, 8, 16. The Judgment Engine first excludes candidates whose bound exceeds tolerance, then prefers the smallest admitted segment count. All sections in a corresponding track use the same segmentation and direction. Lines opposite arcs are represented exactly in that degree/knot basis. Existing polynomial tracks remain unchanged; an existing polynomial opposite an arc must already have compatible quintic knots. Arbitrary knot conversion is rejected.

The optional authored field is:

```firmament
SectionChain RoundedSections {
    Continuity: G1
    ApproximationTolerance: 0.00001mm
    // ordinary named Sections, Frames and Profiles
}
```

The default is 0.00001 mm, degree is fixed at 5, and the maximum is 16 polynomial segments per source arc. The error bound is `sqrt(2) * R * abs(segmentSweep)^6 / (720 * 64)` plus a conservative coordinate-scale floating-point allowance. This follows the componentwise two-endpoint quintic Hermite remainder. Sampled positional deviation and endpoint position/tangent residuals are separately checked and reported. No request is silently relaxed.

The [canonical fixture](../../fixtures/Canonical/SectionChain/normalized-rounded-sections.firmament) has a 60 x 40 mm R8 mixed line/arc profile at three stations, Z=0,8,16. Twelve source arcs normalize to four degree-5 spans each; there are 21 controls per normalized arc. Original semantic span and section identities remain the provenance keys. Internal polynomial knots do not become anonymous author-controlled topology.

| Measured item | Result |
|---|---:|
| Requested approximation tolerance | 0.00001 mm |
| Maximum error bound | 0.0000009004351044 mm |
| Maximum sampled position deviation | 0.0000006353890202 mm |
| Endpoint position error | 0 mm |
| Representative endpoint tangent error | 3.51e-15 degrees |
| Transition patches | 16 |
| Transition controls | 800 |
| Final faces, including planar caps | 18 |
| STEP size | 2,444,777 bytes |

Representative normalized curve `A.BottomRightCorner` control hash: `0F2C36D6D745D601BC84B724125B413B0091ACE9625DCF82902437188502D9FD`.

STEP SHA-256: `55223460C946AB245A0A44CDD857EA13F081F9C8DC2FD7E82648DCBAD34E666F`. Repeated repository builds and the freshly packaged CLI outside the checkout produced this same value. The measured transition/stitch/validation phases were approximately 432/130/28 ms in one local run; these are observational timings, not deterministic data or a complete end-to-end benchmark. Existing timing buckets exclude normalization and the new differential inspection.

## Geometric evidence and the next blocker

`geometricJoins` inspects realized tensor B-spline patches using analytic first/second derivatives. It reports position mismatch, normal angle, and the Frobenius residual between second fundamental forms expressed in a common orthonormal world tangent basis. Thus different parameter speeds do not by themselves create a curvature mismatch. Curvature status is conditional on successful G0 and G1 checks. Sampling includes endpoints, 33 regular parameters and applicable knots; it is explicitly not a global continuity certificate. Terminal caps are outside this join report.

| Boundary family on canonical fixture | G0 | Normal angle | Curvature residual | Status |
|---|---:|---:|---:|---|
| Internal section B, corresponding spans | 0 mm | 0 degrees | 0 /mm | G2 within sampled tolerance for this straight extrusion |
| Neighboring R8 line/arc transition patches | 0 mm | 0 degrees | 0.125 /mm | G1 |

The second result is the decisive next blocker. A straight source span has zero curvature, while its tangent circular neighbor has curvature 1/8 per mm. Endpoint-preserving normalization retains the jump. A longitudinal quintic law alone cannot remove it from the realized footprint seams. The new tests measure this on the actual surface patches, not just on a drawing or a continuity label.

The [G2 rejection fixture](../../fixtures/Invalid/SectionChain/g2-not-admitted.firmament) returns `section-chain-continuity-invalid:G2` and emits no STEP. The [unachievable tolerance fixture](../../fixtures/Invalid/SectionChain/normalization-tolerance-unmet.firmament) returns `section-chain-normalization-tolerance-unmet` and emits no STEP. This is not yet a planar-step/G2 implementation.

The next implementation must define which parent boundaries must be G2 and derive their support data, then resolve both longitudinal curvature matching and the footprint seam behavior within an explicit error policy. Returning a G1 rounded band or the existing rectangular crown as a completed plateau would conceal these remaining requirements.

## Locality, representation, and failure behavior

- Normalization is surfacing-only. Existing exact non-surfacing circular profiles still use their analytic path, covered by the boundary regression suite. Line-only smooth geometry and explicit G0 generation are unchanged.
- The existing shared-edge, pcurve, loop/orientation, self-intersection and BRep validation path remains authoritative. No post-build sewing, repair tolerance increase, or separate executor was added.
- Normalization errors return no body. Invalid tolerances, degenerate arcs, unsupported curve kinds, and incompatible track representations fail typed. G2 requests do not fall back to G1.
- A changed common segment count changes the whole corresponding track. `SectionChainEditor` therefore reports all transitions/tangent fields as dependent and does not claim unchanged caps in that case. Without a grid change, existing G1 stencil locality remains intact. This is conservative dependency reporting, not a new local BRep graft guarantee.
- No new `ConstructionState` operation is introduced. Existing replay/locality tests pass; new normalization is reached when an existing SectionChain operation materializes its semantic chain.

## Validation and reproduction

Run [qualify-surf-g2-x1.ps1](../../scripts/qualify-surf-g2-x1.ps1) after a Release build. It runs build/repeat/inspect/validate/reimport through the CLI, checks the normalization error policy and diagnosed curvature jump, and requires transactional failure for the two invalid fixtures. Outputs default to ignored `artifacts/local/surf-g2-x1/qualification/`.

- Release solution build: passed, zero warnings/errors.
- Full serial solution regression: all 20 test-project commands exited zero; 3,570 tests passed. FrictionLab still reports no discoverable tests. No unrelated failure occurred in this serial run.
- Focused normalization tests: 16 passed, including dense radial-error sampling, endpoint/internal-knot derivatives, common segmentation, deterministic STEP, line/polynomial preservation, invalid tolerance/arc/type/topology, and widened edit dependencies. Two of these tests were added after the serial run and passed separately.
- Focused Firmament/analytic-boundary tests: 47 passed, including the new source-to-BRep normalization and geometric-join checks.
- A 64 x 44 mm R9, 20 mm-high variation also reimported enclosed-manifold with the same 18-face topology.
- Fresh NuGet tool package installed outside the checkout: `section-chain validate`, `inspect`, `build`, and STEP `analyze` succeeded; STEP hash matched the repository build.
- Documentation/link checks, repository layout guard and `git diff --check` are part of closure.

The initial G0 circular-section test probe exposed an existing ruled-arc pcurve approximation mismatch, approximately 0.000300 mm against a 0.00001 mm pcurve tolerance. G0 bypasses the new normalizer. That probe was not turned into a G0 capability claim or patched by relaxing tolerance; existing supported G0 tests remain green.

## Acceptance coverage

Profile normalization, error/provenance inspection, deterministic export, manifold reimport, and geometric seam inspection are delivered. New G2 boundary conditions, Plane/Cylinder support extraction, planar-step/convex/concave/rounded-plateau witnesses, Round-versus-G2 comparison, impossible-curvature/width tests, G2 transactional locality, and G2 parameter variation remain unimplemented.

The iPhone geometry and previous +2 mm variant were not modified. Only the old witness comment and public limitation text were refreshed because the original arc-admission diagnosis is now historical. Phone G2 burn-in, the fresh-agent plateau authoring test, and requalification of the phone length variant await the four general witnesses. No G2 before/after image or product claim is supplied.
