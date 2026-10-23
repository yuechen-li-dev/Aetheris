# STEP-LEGACY-CLOSEOUT-X2

Verdict: **Meaningful progression**, pending human visual review and four isolated spherical-shell orientation failures on AP242 reimport. This is not Preview 4 acceptance.

Continuation: [STEP-LEGACY-FINAL-X3](STEP-LEGACY-FINAL-X3.md) repairs the four spherical reimports, adds actual viewport trim comparison, fixes cylindrical sector/chart overfill, and records the remaining suspicious/broken spline threads. The X2 measurements below remain historical evidence, not current visual acceptance.

## Authority and tolerance

BRep, its topology, bound geometry, trim evidence, and engineering recovery provenance own interchange correctness. Viewport triangles and Telos buffers are disposable projections. Engineering `Status`/`GeometryStatus` are independent of `DisplayStatus` (`NotAssessed`, `Complete`, `RefinementLimited`, `Partial`) and `DisplayReasons`. A good preview never promotes a degraded BRep.

The user's ordinary-part recovery allowance is at most **0.1 mm**, using sampled evidence rather than exact rational mathematics. Production normalization operates on a copy of authoritative geometry and bindings, keeps topology and resolved orientation identities, and rederives pcurves when supports change. The base pcurve tolerance remains 0.001 mm; existing measured-recovery local allowances remain bounded by the separate engineering recovery budget. Display chord tolerance cannot enter this path.

Rational input may be retained, evaluated, and displayed. Trusted production export requires admitted analytic/non-rational supports and pcurves; failed recovery produces `step-production-normalization-unsupported`. Compatibility/debug export remains a distinct legacy route. The Cadmata STEP export and CLI production metadata-preserving canonical route select the trusted policy. Production tests cover rejection without an explicit budget, copy preservation, independent pcurve validation, non-rational output, and topology roundtrip.

## Display repairs

The U-joint's final missing patch is source face **#12119**, root **7851**, imported face **67**, a degree-1/degree-3 spline support with a 2×18 net. Two distinct 3D edges (213 and 197) had recovered linear pcurves with coincident UV endpoints. Their lifted residuals were 0.000562/0.000366 mm, but their UV polygon collapsed. Projection of the authoritative edge samples onto the existing support exposes a finite narrow lens, with approximately 0.000212 mm residual.

For collapsed loops composed entirely of recovered spline pcurves, the display sampler locally oversamples the actual bounded 3D edges and projects them onto the retained support. Source pcurves are never overridden. Engineering geometry, pcurves, and qualification remain unchanged. Face isolation now produces 60 triangles; the U-joint displays **209/209** faces and remains engineering Degraded for its separate reported trim/orientation findings. No missing face was capped or fabricated.

High-knot rectangular spline supports use bounded anisotropic sampling seeded by native knots. Quarter/mid/three-quarter sag checks refine the direction that needs it, across the original opposite-axis knot spans. Negligible normal-only changes do not force excessive subdivision. Rectangle snapping requires measured insignificant trim noise. The grid retains the existing 65,536-triangle patch cap and falls back to the bounded conforming path when its initial grid cannot fit. Default viewport chord quality follows fitted model scale (including spline control nets), bounded between 0.01 and 0.25 mm; mesh export policy remains independent. This is static scale-aware preview quality, not a camera-dependent pixel guarantee.

The spring improves from approximately **77,883 triangles with a refinement-limit warning** to **13,872 triangles**, **77.67 ms** materialization in the final sweep, complete with no display warning. The actual Cadmata capture shows separated helical turns and attached boundary lines. Before/after product captures are retained locally. Display improvements do not provide manufacturing evidence.

Coarser automatic preview sampling exposed a planar mounted-bearing boundary sampling failure at source face #5836. A failed coarse planar sampling attempt retries the same authoritative curves once at the established default chord. The mounted bearing remains **349/349** displayed.

## Exact pcurves and connector normalization

Pcurve population now admits exact/non-recovery faces with missing coedge bindings. Valid source pcurves remain preferred, followed by exact analytic derivation and bounded recovery. Exact derivation has `DerivedAnalytic` provenance and does not claim recovered 3D geometry. Unsupported derivation stays explicitly unqualified. The pure analytic box retains its compact byte-exact canonical golden.

The connector's original 0.007–0.009 mm reimport mismatch came from a full circle whose native trim interval was reconstructed from an approximate source seam vertex. Export with pcurve evidence now retains the declared circular interval. The source support reductions fit within their existing 0.0000254 mm target (measured errors about 0.00001879 and 0.00002383 mm). Its non-rational AP242 reimport is Qualified: **97 faces, 263 edges, 168 vertices** unchanged; **263** matched edges; worst sampled edge gap **0.00017728 mm**; bounds gap **1.36e-14 mm**. The base pcurve tolerance was not raised.

Low-degree rational directions use bounded anisotropic continuation rather than refining both directions blindly. The control-net cap is 1,024 per axis and 8,192 total, with bounded span subdivision. This admits existing long, narrow vendor nets: the threaded screw's 2×657 support recovers with approximately **0.02358 mm** sampled deviation within 0.1 mm. It normalizes successfully with all **147 faces / 405 edges / 268 vertices** retained.

## Final corpus and remaining engineering boundary

The frozen vendor corpus remains unchanged: **26 files, 25 AP203 / 1 AP214, 58 roots, 5,552 parsed/bound/displayed faces, zero missing display faces**. Whole-file engineering status is **8 Qualified / 18 Degraded**. The stricter findings exposed by exact-face pcurve population are not hidden by display completeness: **473 missing coedge pcurves** remain in explicitly unqualified bodies. By root, **45 Complete / 13 RefinementLimited** display reports retain bounded warnings while covering every face.

Of **31 engineering-qualified body normalization attempts, 27 pass and four fail**. Every successful candidate contains no rational B-spline STEP entities. All four failures are in the bearing (mcm-12), roots **1044, 4327, 4420, 4460**. Each retains 2 faces / 2 edges / 2 vertices, bounds and sampled edges agree within approximately 1.1e-13 mm, and reimported pcurves validate. Reimport nevertheless reports `unqualified-orientation: Shell 1: Ambiguous` / `step-orientation-shell-ambiguous`: the canonical resolver cannot establish a nonzero material-side signed-volume witness for this two-face spherical trim representation.

These are orientation failures, not rational recovery failures or tolerance failures. Their reimported bodies remain Degraded and normalization results remain failed. No source `SAME_SENSE` value or screenshot is promoted to material-side authority. A minimal isolated reproduction is retained at `artifacts/local/step-closeout-x2/sphere-roundtrip/`; the exported bearing body is `final-current/mcm-12/normalized-1044.step`. The next engineering task is canonical spherical trim/orientation roundtrip qualification, with independent evidence for complementary hemisphere regions. Further tolerance relaxation would not address this failure.

Normalization comparisons use topology counts, bounds, independent reimport qualification and 33 native-parameter samples per endpoint-matched edge. They do not constitute a universal surface-interior proof. Support recovery has its own sampled matched-parameter checks and explicit provenance; synthetic production regression checks additional interior samples independently.

## Actual viewport and review

The real current-source Cadmata frontend/server, using Edge WebGPU, passed upload, orbit redraw, selection and authoritative edge submission for bearing, U-joint, spring, connector, threaded screw, mounted bearing and collar. Submitted line counts are respectively **374 / 540 / 6 / 263 / 405 / 848 / 557**. Compound lines carry stable BRep edge and occurrence IDs with the same transforms as face buffers; cached geometry buffers are reused across occurrences and selection updates. Picking identity does not depend on triangle numbering. Existing PMI regression coverage remains separate; these vendor witnesses do not establish new PMI semantics.

Thirty browser animation-frame intervals after orbit measured about 8.1 ms p95 on this machine. This is browser cadence evidence, not a GPU benchmark. Larger parts remain expensive: U-joint 736,959 triangles / 2.724 s; connector 169,834 / 1.110 s; screw 630,945 / 1.526 s; collar 1,214,561 / 3.908 s. Those costs are retained explicitly rather than claiming universal preview performance.

Agent inspection found the spring and principal U-joint silhouettes coherent. Collar/thread detail remains a visual review focus; diagnostic double-sided flat shading can emphasize internal overlapping surfaces. Such observations are not human acceptance. Vendor reference images were not supplied or independently fetched. The final contact sheet was presented to the user with the required **Looks Correct / Suspicious / Broken** classification request. Human review remains pending; no classifications are fabricated. Actual topology captures also require visual alignment sign-off.

## Evidence and reproduction

Generated evidence is ignored under `artifacts/local/step-closeout-x2/`:

- `final-current/corpus-summary.json`, per-specimen diagnostics, meshes, normalized STEP, two views per specimen and `contact-sheet.png`.
- `comparison/` and `u-joint-final/`: isolated U-joint before/after, support/UV/edge/triangulation inputs.
- `product-current/`: seven production viewport and orbit/selection captures with `product-summary.json`.
- `spring-before-after.png`, `u-joint-before-after.png`, `connector-normalization.json`, `human-review.json` and `closeout-summary.json`.
- CLI inspection JSON for connector, spring and U-joint.

Reproduce the corpus with `scripts/qualify-step-legacy.ps1 -CorpusDirectory <local McMaster directory> -OutputDirectory artifacts/local/step-closeout-x2/final-current`; render via `scripts/render-step-legacy.py`. The Cadmata qualification script consumes the real upload UI against a foreground host; it installs no service. Raw vendor files are not redistributed. The tracked manifest preserves the X0 baseline.

## Regression and cleanup

Final Release solution build passed (10 existing warnings, zero errors). Fast kernel iteration passed **1,042** tests. The full serial solution gate passed **4,366 .NET tests, zero failures/skips**, including SlowCorpus, NIST, orientation, assembly, recovered-bolt and AP242 roundtrip coverage. Frontend typecheck/build passed; **87 frontend tests** and **12 Telos tests** passed with zero failures/skips. Actual Cadmata qualification passed **7/7** witnesses. Repository layout and whitespace checks passed.

Three NIST canonical SHA entries were deliberately refreshed after pcurve serialization changed; topology counts, qualification and diagnostic snapshots were unchanged. The canonical box golden remains unchanged. Existing source evidence and earlier X0/X1 changes were preserved. Frozen legacy Zig was skipped because neither its compiler nor build/test infrastructure changed.

Cleanup audited independent tolerance use, bounded allocation/refinement, rational export guards, BRep immutability, stable overlay identity and absence of specimen-specific dispatch. Public import documentation now states the authority split. The repository information-architecture guard and whitespace check passed. The full suite's generated ellipse production fixture is restored after validation. No background service, startup hook, commit, or raw vendor content is introduced.

Acceptance remains withheld for the four precisely isolated orientation roundtrip failures and required human visual review.
