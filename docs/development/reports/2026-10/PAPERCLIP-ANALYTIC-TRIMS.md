# Exact analytic paperclip trims

2026-10-24. Follow-up to the paperclip interchange fix and browser-first candidate.

## Cause and repair

The supplied `maximum-paperclip.step` was 53,884 bytes, versus 6,555 bytes for the earlier AP242 specimen. Its solid still had three toroidal faces, four cylinders, two planar caps, and exclusively circular/linear 3D edges. Import had populated missing UV trims through the general periodic-surface fitter, producing 21 cubic spline definitions and 646 Cartesian points. Export serialized those fitted trims, including straight isoparametric boundaries.

`BrepPcurveRecovery` now recognizes cylinder generators and rings, plus the major/minor circles of regular ring tori, before fitting. Support/curve identities establish the affine UV parameterization using rounding-scale geometric checks independent of the import tolerance. Signed angular spans come from circle orientation and are retained across seams and multiple turns. The existing independent lift validator remains authoritative. Other curves retain the general recovery path; existing source-supplied trims are preserved.

No 3D geometry, construction winding, tolerance policy, parser route or export option was changed. Public WireForm documentation now explains analytic trim derivation and source-trim retention.

## Evidence

Ignored evidence: `artifacts/local/release-2-readiness/analytic-paperclip-trims/`.

- Eight analytic tests cover four edge/support families in translated, rotated frames with both orientations, negative starting parameters and spans exceeding a full turn. A ninth rejects off-support and oblique cases, including a radius mismatch smaller than the import tolerance.
- The compiler-backed paperclip test exercises two import/export cycles. It preserves nine faces, 15 edges, eight vertices, all 30 pcurves and explicit seams, with no spline/polyline representation. Reconstruction deviation remains below `1e-10 mm`.
- Real CLI build plus `canon` emits a **10,955-byte** STEP with 28 linear UV uses and two planar conic uses. Maximum reported lift deviation is approximately `3.92e-14 mm`.
- Independent OCCT comparison against the supplied file reports both shapes valid, one solid and nine faces, identical bounds, and matching volume/area within `1e-9`. Result: approximately **75.16190668032 mm³**, **302.21842304808 mm²**. The user's input files were not modified.
- The updated packaged Cadmata server served production assets, imported the paperclip as `Qualified`, and exported it through the real definition-export endpoint: **10,955 bytes**, no splines, explicit seam curves. Its STEP is byte-identical to the CLI output (SHA-256 `82cf4b33274646807dfdb139728030959f36673d0c0a34ff99777be59a329a2c`), so the independent geometry comparison covers both paths. The server was stopped afterward. This is API/package evidence; no new SolidWorks or native-window interaction was claimed.
- The first complete core run passed 1,262 checks and exposed three expected NIST serialization-hash changes. Snapshot comparison confirmed every non-hash field unchanged. Only CTC-03, FTC-09 and STC-09 changed; their Windows hashes and independently measured Linux overrides were refreshed.
- Bounded Linux qualification: **40 core/interchange checks and one compiler-backed paperclip round-trip passed**, including the three NIST overrides. Fast Windows kernel lane: **1,110 passed**.
- Solution build passed with zero errors and eight warnings. Full serial Windows solution qualification passed: **6,038 passed, zero failed, two existing optional PDF-renderer skips**, including the complete kernel corpus, Firmament, server and V8 geometry suites. `git diff --check` passed.

## Local candidate

`Aetheris-2.0.0-win-x64-analytic-trims.zip`, **97,259,503 bytes**, SHA-256:

`3a00de470864fee62a8765b5689616a1d1236658db364c882faa6eb4be9aa280`

This local modified-source candidate copies the preceding qualified browser-first bundle, replaces the changed core assembly with its matching 2.0.0 assembly version, and updates the WireForm guide. It retains the existing browser-first launch and carries no private WebView2 runtime. Package HTTP/import/export checks use this candidate. No public release or hosted CI run was performed.

Regenerated output is `paperclip-cadmata-export.step`. Already supplied spline pcurves are deliberately retained during import; this fix prevents generating redundant fits when trims are missing. It does not add a general imported-spline simplifier, extend torus support to horn/spindle cases, or claim that all analytic intersections have linear UV curves.

## Verdict

**Success.** The motivating compiler-to-Cadmata export no longer fits splines to analytic paperclip trims. Geometry and seam spans are independently qualified, the full Windows gate and bounded Linux gate pass, and the replacement archive contains the qualified core assembly. Owned test servers and build/test hosts exited. The user's pre-existing browser server was left running; use a fresh extraction of the new candidate to launch the fixed application. Nothing was published remotely.
