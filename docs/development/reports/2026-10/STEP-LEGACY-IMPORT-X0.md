# STEP-LEGACY-IMPORT-X0

Verdict: **Meaningful progression; Preview 4 legacy STEP fidelity is not accepted.**

The local user-provided McMaster corpus contains 26 specimens: 25 AP203 and one AP214. No vendor STEP data is redistributed. The tracked inventory is `testdata/step242/manifests/mcmaster-legacy-x0.json`; it records names, hashes, schemas, categories, and outcomes. All raw CAD and generated reports/captures stay local. Eleven sources use inches (`25.4 mm/unit`); fifteen use millimetres. Units are read from STEP rather than guessed from bounds. No AP242 control files were supplied; existing AP242/NIST tests provide regression coverage.

The baseline imported every specimen before geometry/importer changes and generated isometric and opposite-isometric captures of the actual Aetheris display mesh. These diagnostic images are independent of the Cadmata product witness and do not substitute for human review. All source face entities bound, but 39 faces across five specimens produced no display triangles. Enclosed topology did not prevent visibly distorted spline materialization, including the compression spring.

## Changes

- Body-level qualification now records expected/bound faces, shell connected components, boundary/non-manifold edges, ordered loop closure, pcurve evidence, and explicit reasons. Parser/API success retains useful geometry; it does not imply qualification. Geometry and display assessment remain distinct.
- CLI JSON exposes qualification and source schema. Cadmata STEP assembly/compound responses retain partial display geometry with per-definition qualification and source face identities, and the UI shows a compact status.
- Partial tessellation no longer discards trim rejection warnings attached to successful empty patches. No geometry is added or repaired in the renderer.
- Polynomial pcurve export preserves exact endpoints during line parameter remapping, avoiding an out-of-domain floating-point exception. Rational weights survive the same remap. The real pawl is an import/export/reimport witness with preserved topology and bounds and sampled edge deviation near `1e-12 mm`.

## Recorded outcomes

`Qualified` in the machine report means the implemented BRep and display-completeness checks passed. It is a candidate for normalization and visual review, **not** a completed fidelity or release acceptance claim. `Inspectable` retains useful geometry with incomplete evidence; `Degraded` records detected contradictions or missing display faces; `Failed` produces no usable imported body. Normalization failures and pending human review are recorded separately in the manifest. The single-body upload remains Inspectable until display assessment; the compound/assembly upload includes that assessment.

| Metric | Baseline | Final |
|---|---:|---:|
| Files imported | 26 | 26 |
| Source / bound faces | 5,552 / 5,552 | 5,552 / 5,552 |
| Unbound source faces | 0 | 0 |
| Displayed faces | 5,513 | 5,513 |
| Missing display faces | 39 | 39 |
| Structurally enclosed/manifold rigid roots | 58 | 58 |
| Structurally open rigid roots | 0 | 0 |

Final mechanical outcomes are **3 Qualified candidates, 14 Inspectable, 9 Degraded, 0 Failed**. No missing-face geometry repair is claimed. Baseline did not have the new structured qualification API, so the old harness's status labels are not treated as a before/after fidelity metric. Human visual review remains pending; the distorted spring is an observed machine-inspection failure, not a human-reviewed pass.

The missing-display cluster is `mcm-05` (2), `mcm-10` (2), `mcm-12` (28), `mcm-15` (3), and `mcm-18` (4). Other degraded specimens have failed trim recovery/validation or unqualified orientation evidence despite nonempty display patches. The new report consumes the existing resolved face-orientation authority and does not reinterpret `SAME_SENSE`.

| Repaired cluster | Before | Cause / change | After |
|---|---|---|---|
| Empty-patch diagnostics: gear, collar, U-joint, mounted bearing | A successful empty patch lost its trim warning; display completeness was not explicit | Preserve success diagnostics and project actual per-face display coverage | Source face IDs and missing-patch reasons survive; compound geometry is retained and visibly Degraded in Cadmata |
| Pawl AP242 export (`mcm-13`) | Export threw a polynomial-pcurve domain exception | Affine endpoint arithmetic rounded one ULP beyond the domain; preserve exact mapped endpoints and rational weights | Export/reimport succeeds: 22 faces, 54 edges, bounds deviation `5.03e-15 mm`, sampled edge deviation `1.03e-12 mm` |

The recurring sphere and spline geometry failures remain isolated in per-file JSON. They require support/trim materialization work beyond this verified exporter and qualification change. Broad tolerance increases, schema/vendor exceptions, and renderer face repairs were rejected as routes to acceptance.

## AP242 normalization

Six mechanically qualified body/display candidates were attempted; five passed the bounded counts/bounds/edge comparison. This is body-level evidence, not normalization of an entire degraded multibody product.

| Source | Candidate bodies | Passed | Result |
|---|---:|---:|---|
| Pawl `mcm-13` | 1 | 1 | Whole specimen: preserved counts, bounds, and 54 sampled edges |
| Connector `mcm-17` | 1 | 0 | AP242 reimport rejects four face-local pcurves; explicit failed candidate |
| Threaded screw `mcm-21` | 1 | 1 | Whole specimen: 147 faces, 405 edges; bounds `4.00e-14 mm`, sampled edges `7.56e-11 mm` |
| Collar `mcm-10` | 2 | 2 | Qualified sub-bodies only; whole specimen remains Degraded |
| Mounted bearing `mcm-18` | 1 | 1 | Qualified sub-body only; whole specimen remains Degraded |

Comparison budgets are `1e-5 mm` for bounds/vertex endpoint matching and `1e-3 mm` for 33 native-domain samples per matched edge. Samples compare both directions and consume matches bijectively. No serialization-ID, absolute-parameter, or rounded-coordinate equality assumption is used. Surface interiors, independent external CAD opening, and mass/volume equivalence were not qualified. The connector failure therefore prevents treating all three machine-Qualified specimens as normalized, fidelity-accepted examples.

## Remaining failure classes

| Class | Evidence / next work |
|---|---|
| Missing face-local pcurves | Numerous analytic/exact-spline vendor bodies never enter the existing recovery-only population branch. They remain Inspectable rather than being silently Qualified. |
| Failed pcurve recovery / validation | Recovery diagnostics and measured lift/closure residuals remain Degraded. The base 0.001 mm pcurve and 0.1 mm geometry budgets are unchanged. |
| Spherical trims | Ball bearing: 166 bound faces, 138 displayed. Explicit failures include two-loop sphere trims and degenerate elevation spans. |
| Spline trim materialization | Gear, collar, U-joint, and mounted bearing contain empty patches; the detailed report retains loops, directed edges, vertices, and UV endpoints. |
| Spline interior materialization | The spring's existing pcurve recovery validates its trims, but the resulting display remains distorted. Recovery alone is not a fix. |
| Normalized source pcurve mismatch | The connector's AP242 reimport rejects four spline-support bindings with 0.007–0.009 mm lift error against a 0.001 mm allowance. This remains an explicit failed normalization witness. |

The corpus contains multibody products, including the U-joint and bearings, but no `NEXT_ASSEMBLY_USAGE_OCCURRENCE` hierarchy. It cannot qualify external AP203/AP214 assembly transforms or definition sharing. Existing authored assembly regression fixtures cover that separate path. The sole AP214 specimen remains Inspectable; no qualified AP214 normalization claim is made.

## Reproduction

Run from the repository root with the .NET 10 SDK:

```powershell
pwsh -File scripts/qualify-step-legacy.ps1 -CorpusDirectory '<local McMaster directory>' -OutputDirectory artifacts/local/step-legacy-x0/final
python scripts/render-step-legacy.py artifacts/local/step-legacy-x0/final
```

The runner checks manifest hashes, reports missing local specimens explicitly, contains each import/display/normalization in a separate process, and continues after a per-file timeout/failure. Normalization is attempted only for mechanically qualified body/display candidates. Its comparison matches edge endpoints and samples each curve in its own parameter domain; it does not assume serialized entity IDs or parameter origins survive. Surface interiors, independent external opening, and human visual review remain separate unqualified gates.

Run data lives under ignored `artifacts/local/step-legacy-x0/`: `baseline/`, `final/`, `before-after/summary.md` and `.json`, `probes/`, `normalization-probes/`, and `product-final/`. Baseline and final each contain 52 two-angle specimen PNGs plus a contact sheet. Final product evidence contains real upload responses, WebGPU screenshots, orbit comparisons, and selection hits for the pawl, connector, threaded screw, U-joint, and spring. All five upload/orbit/selection checks passed with zero browser page errors. Representative screenshots are `product-final/mcm-13-product.png`, `product-final/mcm-15-product.png`, and `product-final/mcm-24-product.png`. Topology overlay and human review are explicitly unqualified. Helios was not changed or qualified in this repository-scoped task.

To reproduce the product witness, build the frontend with `tspack run build --root .`, then build `Aetheris.Server -c Release -m:1` so its static frontend is current. Run its DLL in a foreground terminal with `--urls http://127.0.0.1:5087 --no-browser`, then invoke `node scripts/qualify-step-legacy-cadmata.mts <playwright-index.mjs> <local-corpus-directory> artifacts/local/step-legacy-x0/product`. The script uses local Edge and does not install/start a service.

## Validation

- Full Release solution build: passed, 0 errors (2 existing warnings). Final runner and product builds: passed, 0 warnings/errors.
- Fast Core lane baseline: 1,017 passed. Focused qualification/export/empty-patch tests: 7 passed.
- Required full serial solution lane: **4,347 passed, 0 failed, 0 skipped**, across 20 test assemblies, including NIST, orientation, assembly, and recovered McMaster bolt coverage. FrictionLab currently discovers no tests.
- Frontend typecheck/build: passed; frontend suite: **86 passed**.
- Repository content-location guard: passed. Missing-source runner check: all 26 explicitly reported `MissingLocalSpecimen`, without attempting import.
- Corpus import latency on this machine: `0.21–9.68 s`, mean `2.43 s`; all workers completed within the 180 s containment budget. This is measured timing, not a performance guarantee.

The executive answer is bounded: Aetheris now exposes degraded legacy imports and removes a demonstrated normalization exception, but the representative corpus still contains incomplete/distorted display geometry and a failed normalized pcurve family. Preview 4 legacy STEP acceptance remains blocked.
