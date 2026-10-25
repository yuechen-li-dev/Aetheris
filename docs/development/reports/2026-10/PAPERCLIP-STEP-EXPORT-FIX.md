# Paperclip STEP export fix

2026-10-24. Follow-up to [the controlled interchange investigation](PAPERCLIP-STEP-INTERCHANGE-AUDIT.md).

## Changes

- Corrected Straight/Bend WireForm side and terminal boundary winding at construction. Analytic surfaces, dimensions, topology counts and feature identities are preserved.
- Circular and hyperbolic advanced-face edges now reference their underlying analytic curves instead of forbidden 3D `TRIMMED_CURVE` wrappers. Vertices, edge sense and qualified pcurves retain the intended bounds. The public `EmitFullCircleTrimmedCurves` property remains for source compatibility and has no effect; its internal plumbing and production caller were removed.
- Fixed periodic pcurve rebinding exposed by that correction: inferred circle/ellipse parameters may move by a whole turn at a seam. The decoder aligns the pcurve domain by whole periods only when both endpoints agree within `1e-8` radians. Polynomial knot domains move by the same amount. Geometry, weights, controls, provenance and use sense are preserved; mismatched spans still fail validation.
- Stabilized symmetric SurfaceMeshIR quad diagonal selection against rounding-scale platform differences. The shortest valid diagonal remains preferred; ties within `1e-12 × max(1, diagonal length)` use a fixed split. No support or vertex changes.
- Added raw construction and raw serialized-cap winding checks, analytic-basis checks, periodic-seam tests and diagonal-tie tests. Improved the 3DM recovery test's failure diagnostics.

No import setting, SolidWorks workaround, approximation, alternate exporter route, native dependency or geometry feature was added.

## Geometry and application witnesses

The current CLI validates and builds `fixtures/Canonical/WireForm/paperclip.firmament`. Independent OCCT inspection preserves **75.1619066803201 mm³** volume, **302.218423048075 mm²** area, Y bounds **-4.5 to 20.5 mm**, nine faces and one solid. Each toroidal bend retains the intended `[0, π]` major-angle interval.

The fresh compiler output was opened in SolidWorks Design Professional for Makers 2026 SP3.0. Import Diagnostics was declined and global settings were unchanged. It displays the correct paperclip as one solid. SolidWorks Mass Properties reports **75.16 mm³** and **302.22 mm²** at its existing two-decimal display precision. Existing user files and documents were not saved or changed. This qualifies local import and mass properties, not every downstream SolidWorks operation.

An isolated local bundle is prepared at `artifacts/local/release-2-readiness/paperclip-fixed-bundle/Aetheris-win-x64/`, with `Aetheris-2.0.0-win-x64-paperclip-fixed.zip` beside it. It preserves the previously qualified Cadmata bundle and replaces only the two changed kernel assemblies and their matching symbols when present. Its real packaged CLI builds the paperclip with no diagnostics and emits byte-identical STEP to the source-built CLI (`f3d55cbfff0c9725b0ea3b845127f43f72c05a67854dde1a6c82015d4fb13c73`). Skia/PDFium remain absent. The ZIP has CRC integrity checked and SHA256 `fb8b6e879d69b6753a7ad0828e5171a5951e4b7b5062ea2fe4226ec67de6b967`. The existing running bundle and published artifacts were not modified. The new desktop bundle was not separately relaunched; this pass exercises its packaged CLI and preserves the previously qualified desktop/frontend binaries.

## Compatibility qualification

- The full gate exposed a 3DM recovery regression and imported-hole/rounded-plateau pcurve regressions. Whole-period alignment closes them; focused reruns pass all 27 recovered bodies, the real imported-hole CLI workflow and 28 related Firmament tests.
- Updated 16 Windows NIST serialization hashes. All other snapshot fields, including admission, topology and diagnostics, are unchanged. The three existing Linux-specific overrides were measured under Linux and updated; the remaining platform hashes agree.
- The isolated prior kernel first matched the frozen V8 mesh reference. All 21 current definition bodies then matched its surfaces, curves, trims, vertices and topology within the existing `1e-9` bound; the largest numeric difference is `1.4210854715202004e-14`. All 171 world placements are preserved. Four display samplings changed with legal analytic edge bases; the diagonal tie rule also changes display connectivity. The numeric display reference was refreshed only after this exact-body comparison.
- Windows/Linux current V8 mesh indices and array lengths agree exactly. Maximum position difference is `1.0125233984581428e-13 mm`; maximum normal-component difference is `2.2668732169162098e-10`. The existing `1e-9` test bounds were retained.
- Linux qualification uses the real .NET 10.0.401 test platform and 10.0.13 runtime in an ignored artifact directory, through WSL. All 32 selected kernel/interchange tests and the V8 geometry test pass. This is a bounded Linux gate, not a full Linux suite claim.

Raw logs, the prior/current V8 probe, numeric comparisons, SolidWorks observations and STEP specimens remain under ignored `artifacts/local/release-2-readiness/paperclip-interchange-audit/`.

## Scope and remaining limits

This closes the circular WireForm interchange defect. It is not a complete STEP schema conformance audit. The pre-existing partial B-spline edge wrapper branch is unchanged: simply deleting those wrappers would lose trim intervals in the current spline importer. Exact domain-bounded spline export/import needs separate qualification. The pcurve's own 2D `TRIMMED_CURVE` is legal and remains supported.

The independent-bounds discrepancy noted in the investigation is also unchanged. This pass uses independently measured geometry bounds rather than claiming that the CLI's generic analytic bounds have been repaired.

## Final gate

- `dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false`: passed, zero warnings/errors on the final incremental build.
- Fast kernel lane, `Category!=SlowCorpus`: **1,101 passed**, zero failures.
- Complete `dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1`: **6,027 passed**, **2 skipped**, zero failures across 20 assemblies. The skips are the existing optional PDF-rasterizer cases.
- Bounded Linux gate: **33 passed**, zero failures, including NIST snapshots, seam/quad checks and the V8 geometry reference.
- `git diff --check`: passed. Modified code was checked for global winding reversal, new parser/export routes, source mutation, tolerance weakening and lost pcurve provenance; none was introduced.
- Controlled build/test hosts exited; no persistent .NET build/test process is required by the fix.

**Verdict: Success for the reported paperclip blocker.** Fresh real-path STEP imports correctly in SolidWorks, its analytic geometry is preserved, the broader regressions exposed by legal edge emission are closed, and the full Windows gate passes. This does not assert that the separate spline-export and generic analytic-bounds limits above are fixed, or that the local candidate has been published.
