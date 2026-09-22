# 3DM-RECOVERY-X1 — rational geometry recovery toolkit

## Verdict: Meaningful progression

The local Product Design lamp now has deterministic, inspectable recovery evidence at body, face, and edge level. `recover-3dm` does not emit an Aetheris BRep, authoritative Firmament, or STEP from the lamp. The result is useful for a later human/LLM recovery plan, but trim/topology qualification and a real Firmament witness remain open.

The central result is that the 53 edges left unclassified by X0's native analytic probes are **not evidence that Aetheris should adopt rational NURBS as production geometry**. All 53 have nearly constant control-point weights (relative spread at most `1.21e-13`). A non-rational B-spline with the same controls, knots, and parameter values matches each source edge within the file's `0.001 mm` tolerance; the largest sampled deviation is `2.28e-13 mm`. Sixteen of these edges also fit circular support within source tolerance. The other 37 retain the non-rational candidate. These are support-geometry findings, not rebuilt edge uses or bodies.

## Reproduce and navigate

```powershell
dotnet run --project Aetheris.CLI -- recover-3dm testdata/3DM/cartesian-product-metres.3dm --json > artifacts/local/3dm-recovery.json
dotnet run --project Aetheris.CLI -- recover-3dm testdata/3DM/cartesian-product-metres.3dm --body 5 --edge 0 --json
dotnet run --project Aetheris.CLI -- recover-3dm testdata/3DM/cartesian-product-metres.3dm --body 5 --face 0 --json
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter FullyQualifiedName~ThreeDm
```

`--body`, `--edge`, and `--face` narrow the report to one source entity and include its source object UUID. The complete local JSON lists every BRep, face, and edge, with candidate parameters, RMS/p95/max positional residual, endpoint residual, tangent deviation for edges, normal deviation for analytic faces, qualification class, and topology status. It also groups repeated radius hypotheses for review. No source control net, raw geometry payload, or converted lamp asset is committed. The private file remains ignored at `/testdata/3DM/*.3dm`; if absent, local tests skip explicitly.

## Source authority and qualification

The file declares **metres**, so measurements are converted once to **mm**. Its absolute model tolerance is `1e-6 m = 0.001 mm`; sampled trimmed edges span roughly `570 × 155 × 560 mm`. Rhino's native BRep box includes untrimmed support extents and had overstated the object envelope. Edge-sampled bounds are navigation evidence, not certified face-interior extrema. Source tolerance is a geometric assertion from the file, not a manufacturing tolerance. No manufacturing tolerance was supplied. Numerical candidates are called `WithinSourceTolerance` only when bounded deterministic samples pass positional and source angle tolerances. This is numerical evidence, not a symbolic proof or a claim that all unsampled extrema pass. A candidate remains provisional until trims, edge uses, face support, orientation, and shell topology are qualified.

The edge study samples up to 257 parameter locations for each unclassified rational edge. Its line candidate uses the endpoint line; its circle candidate uses three source points and tests the entire edge. Its non-rational candidate keeps the same control net and knot parameterization while setting weights to one. OpenNURBS' two omitted end knots are restored for the Aetheris B-spline evaluator. The near-constant weights make this replacement exceptionally close for the Product edges, but X1 does not generalize that result to arbitrary rational curves. The bounded candidate chooser uses JudgmentEngine only after candidates pass residual and angular admissibility, then prefers the simpler representation with explicit residuals retained.

## Edge recovery summary

| Rational source edges | Native circular supports | Native circular arcs | Previously unclassified | Circular candidates from those 53 | Non-rational same-net candidates from those 53 | Unresolved support |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 196 | 87 | 56 | 53 | 16 | 37 | 0 |

All 143 native circular/arc supports were independently sampled and remained within source tolerance. Of the 53 studied edges, the 16 selected circle supports have sampled max deviations from `9.77e-5` to `9.30e-4 mm`. The 37 selected non-rational same-net curves have sampled max deviations from `7.42e-14` to `2.28e-13 mm`. Some rejected circle candidates fit visually but exceed the source tolerance; the report retains their residuals instead of silently choosing them. All 53 require edge/trim and face coherence checks before promotion.

### All 53 previously unclassified rational edges

The source edge identifier is `(object index, Brep edge index)`. The table records the selected **support** candidate; every row remains topology pending. Full candidate sets, including rejected circles and line fits, are available in local JSON.

| Object | Edge | Preferred support | Sampled max deviation (mm) | Status |
| ---: | ---: | --- | ---: | --- |
| 5 | 0 | CircleSupport | 9.77144E-05 | Within source tolerance; topology pending |
| 5 | 5 | CircleSupport | 9.77144E-05 | Within source tolerance; topology pending |
| 5 | 6 | NonRationalSameNet | 1.5641E-13 | Within source tolerance; topology pending |
| 5 | 7 | NonRationalSameNet | 1.01796E-13 | Within source tolerance; topology pending |
| 5 | 8 | NonRationalSameNet | 8.8176E-14 | Within source tolerance; topology pending |
| 5 | 10 | NonRationalSameNet | 1.00061E-13 | Within source tolerance; topology pending |
| 5 | 11 | NonRationalSameNet | 1.07759E-13 | Within source tolerance; topology pending |
| 5 | 13 | NonRationalSameNet | 1.0563E-13 | Within source tolerance; topology pending |
| 5 | 14 | NonRationalSameNet | 9.95761E-14 | Within source tolerance; topology pending |
| 5 | 16 | NonRationalSameNet | 7.41882E-14 | Within source tolerance; topology pending |
| 5 | 20 | NonRationalSameNet | 1.28009E-13 | Within source tolerance; topology pending |
| 5 | 22 | CircleSupport | 0.000101631 | Within source tolerance; topology pending |
| 5 | 23 | NonRationalSameNet | 1.42244E-13 | Within source tolerance; topology pending |
| 5 | 47 | NonRationalSameNet | 9.23706E-14 | Within source tolerance; topology pending |
| 5 | 49 | NonRationalSameNet | 9.96186E-14 | Within source tolerance; topology pending |
| 5 | 50 | NonRationalSameNet | 1.17186E-13 | Within source tolerance; topology pending |
| 5 | 55 | NonRationalSameNet | 1.36028E-13 | Within source tolerance; topology pending |
| 5 | 57 | NonRationalSameNet | 1.1375E-13 | Within source tolerance; topology pending |
| 5 | 59 | NonRationalSameNet | 7.70415E-14 | Within source tolerance; topology pending |
| 5 | 60 | NonRationalSameNet | 1.16429E-13 | Within source tolerance; topology pending |
| 5 | 61 | NonRationalSameNet | 1.2958E-13 | Within source tolerance; topology pending |
| 5 | 63 | CircleSupport | 0.000101631 | Within source tolerance; topology pending |
| 5 | 64 | CircleSupport | 0.000268969 | Within source tolerance; topology pending |
| 5 | 65 | CircleSupport | 0.000268969 | Within source tolerance; topology pending |
| 5 | 66 | NonRationalSameNet | 9.23706E-14 | Within source tolerance; topology pending |
| 5 | 67 | CircleSupport | 0.000930239 | Within source tolerance; topology pending |
| 5 | 68 | NonRationalSameNet | 9.11498E-14 | Within source tolerance; topology pending |
| 5 | 70 | NonRationalSameNet | 9.10112E-14 | Within source tolerance; topology pending |
| 5 | 71 | NonRationalSameNet | 1.16022E-13 | Within source tolerance; topology pending |
| 5 | 72 | CircleSupport | 9.77144E-05 | Within source tolerance; topology pending |
| 5 | 74 | CircleSupport | 9.77144E-05 | Within source tolerance; topology pending |
| 5 | 75 | NonRationalSameNet | 1.39961E-13 | Within source tolerance; topology pending |
| 5 | 76 | NonRationalSameNet | 9.8718E-14 | Within source tolerance; topology pending |
| 5 | 78 | NonRationalSameNet | 8.78893E-14 | Within source tolerance; topology pending |
| 5 | 79 | CircleSupport | 0.000930239 | Within source tolerance; topology pending |
| 5 | 80 | NonRationalSameNet | 8.53392E-14 | Within source tolerance; topology pending |
| 5 | 83 | CircleSupport | 0.000101631 | Within source tolerance; topology pending |
| 5 | 85 | NonRationalSameNet | 1.14106E-13 | Within source tolerance; topology pending |
| 5 | 110 | CircleSupport | 0.00026858 | Within source tolerance; topology pending |
| 5 | 111 | NonRationalSameNet | 8.53391E-14 | Within source tolerance; topology pending |
| 5 | 112 | CircleSupport | 0.000928503 | Within source tolerance; topology pending |
| 5 | 113 | NonRationalSameNet | 8.98773E-14 | Within source tolerance; topology pending |
| 5 | 115 | NonRationalSameNet | 1.14572E-13 | Within source tolerance; topology pending |
| 5 | 122 | CircleSupport | 0.00026858 | Within source tolerance; topology pending |
| 5 | 123 | NonRationalSameNet | 8.55607E-14 | Within source tolerance; topology pending |
| 5 | 124 | CircleSupport | 0.000928503 | Within source tolerance; topology pending |
| 5 | 125 | NonRationalSameNet | 8.6751E-14 | Within source tolerance; topology pending |
| 5 | 127 | NonRationalSameNet | 1.0569E-13 | Within source tolerance; topology pending |
| 5 | 128 | NonRationalSameNet | 1.14915E-13 | Within source tolerance; topology pending |
| 5 | 129 | CircleSupport | 0.000101631 | Within source tolerance; topology pending |
| 14 | 0 | NonRationalSameNet | 8.52663E-14 | Within source tolerance; topology pending |
| 14 | 3 | NonRationalSameNet | 1.42818E-13 | Within source tolerance; topology pending |
| 14 | 5 | NonRationalSameNet | 2.27486E-13 | Within source tolerance; topology pending |

## Surface recovery and component evidence

Of 185 source NURBS supports, native analytic probes plus independent 17×17 support-domain checks identify 74 planes, 20 cylinders, and one torus within source tolerance. Among the 104 rational surfaces, 20 cylinders and one torus have analytic support candidates; **83 rational surfaces remain unresolved**. The support-domain grid is not trim-aware and cannot establish valid faces. The local report gives each face's degree, control net size, knots, loop/trim counts, and candidate residuals. No sampled refitting of surfaces was performed.

Each source BRep is a neutral component group; the tool does not label base, stem, shade, or cable from geometry alone. It records source solid/manifold flags, bounds, adjacency counts, and provisional recoveries per body. Repeated radius hypotheses include 2.1, 3.5, 4, 4.3, 5.5, 7, 10, 41.45, and 42.02 mm groups. Those are measurements, not authored dimensions or constraints. The three remaining non-unit-weight edges in the 53-edge study occur in object 14 and have nearly constant weights around 1.669; they pass the same-net check numerically, without being called structurally exact.

## Production STEP boundary

`Step242Exporter` now rejects rational B-spline support surfaces and rational pcurves on `TrustedProductionRoute` with `RationalGeometryNotCanonical`. Tests prove rejection and prove an ordinary box still exports without rational entities. The legacy interchange route retains rational serialization for existing compatibility/debug workflows; it is not a certificate of recovered production geometry. The lamp has no production STEP yet, so there are no lamp STEP entity counts or round-trip metrics to report. No raw rational debug STEP was created.

## What remains unresolved

The 742 source trims, including 114 seams and 50 singular trims, have not been mapped to Aetheris pcurves/vertex loops. The 83 rational surfaces without analytic recognition need a bounded non-rational recovery study or explicit unresolved designation at body reconstruction time. No source subset has yet been rebuilt as a canonical Aetheris BRep, compiled through provisional Firmament, or compared after STEP reimport. Geometry alone cannot establish whether a body was originally authored with Revolve, Boss, or another feature. The report therefore contains candidate geometry and repeated dimensions, with no authoritative feature history.

## Validation

`dotnet build Aetheris.slnx --verbosity quiet` completed with zero errors and two existing Web.Runtime WASM warnings. The focused 3DM tests passed (2/2). The production rational guard tests passed (2/2), and a broader STEP regression filter covering exporter, pcurve/vertex-loop, rational surface reduction, and the guard passed (50/50). The X0 full-suite run had 14 failures in existing Core, Modules, Firmament, Server, and EditableV8 tests; the representative Modules failure reproduced alone in unchanged code, as recorded in `3DM-IMPORT-X0.md`. X1 did not rerun the full suite.

