# 3DM-IMPORT-X0 — Cartesian Product geometry bridge

## Verdict: Meaningful progression

The local Product Design file parses through McNeel OpenNURBS and is now inspectable with `aetheris inspect-3dm`. It is **not** imported into Aetheris BRep, exported as STEP, or round-trip qualified. No exactness or fidelity claim is made. The inspection path removes uncertainty about the source and identifies a concrete blocker: 53 of its 196 rational 3D edges do not classify as circles, arcs, or ellipses, while Aetheris currently has no generic rational 3D curve carrier. Another 56 classify as circular arcs when `TryGetArc` is tested before `TryGetEllipse`; circles/arcs can also satisfy Rhino's ellipse probe. The STEP importer explicitly rejects rational B-spline curves that cannot recover as analytic circles (`Step242Importer.cs`, rational curve branch). Conversion must also map 742 trims, including 114 seams and 50 singular trims, through the established pcurve and vertex-loop architecture before export can be claimed. X1 recovery evidence is reported separately in `3DM-RECOVERY-X1.md`.

## Reproduce the local inventory

```powershell
dotnet run --project Aetheris.CLI -- inspect-3dm testdata/3DM/cartesian-product-metres.3dm --json
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter FullyQualifiedName~ThreeDmLocalQualificationTests
```

The second command skips explicitly if the private fixture is absent. The JSON inventory is local output; if saved, it belongs under `artifacts/local/`. This inspector reads BRep data directly and does not use meshes. It classifies each Product object as `exact-but-not-yet-mapped`; classification is not a conversion result.

## Source inventory (local run, 2026-09-22)

| Measure | Result |
| --- | ---: |
| File size | 736,583 bytes |
| 3DM archive version | 70 |
| Model units | metres |
| Conversion to Aetheris units | 1,000 mm/metre |
| Model absolute tolerance | 0.000001 m = 0.001 mm |
| Model angle tolerance | 1 degree |
| Layers / instance definitions | 3 / 0 |
| Objects | 27 BReps; 0 extrusions, meshes, SubD, standalone curves or surfaces |
| Classification | 0 exact-supported; 27 exact-but-not-yet-mapped; 0 non-BRep/unsupported/irrelevant |
| Source BRep faces / loops / trims / edges / vertices | 185 / 232 / 742 / 346 / 219 |
| Seam / singular trims | 114 / 50 |
| Rational edge / surface / trim NURBS | 196 / 104 / 142 |
| Rational edges not recognized as circles | 109 |
| Of these, recognized as circular arcs / unresolved by native analytic probes | 56 / 53 |
| Sampled trimmed-edge bounds in mm | min (-430.583432381, -77.334934891, 0); max (139.199739336, 77.328068101, 559.759999981) |
| Parse / inventory wall time | about 572 / 19 ms on this local run |

All 27 BReps report `IsSolid=true`. The inventory is source metadata, not an Aetheris enclosure, manifold, orientation, mass-property, or STEP qualification. The source stores every support surface as `NurbsSurface`, every edge as `NurbsCurve`, and every trim as `NurbsCurve`; no analytic surface carrier is yet assigned. Source UUID, layer index, per-object topology counts, source-space extents converted into mm, and curve blockers are in CLI JSON output.

## Dependency and deployment audit

`Aetheris.ThreeDm` isolates [McNeel Rhino3dm 8.32.0](https://www.nuget.org/packages/Rhino3dm/8.32.0) from kernel geometry types. McNeel publishes it under MIT and describes it as a .NET OpenNURBS reader/writer independent of Rhino ([source repository](https://github.com/mcneel/rhino3dm)). The NuGet package includes managed .NET assemblies and native runtimes for Windows x86/x64/arm64, Linux x64/arm64, and macOS x64/arm64. Redistribution requires retaining its MIT notice; no Cartesian source asset is redistributed. The native library and P/Invoke make NativeAOT compatibility unqualified here. Browser WASM support is not part of X0. This is a desktop/server inspection adapter, with no Rhino type in its public records.

## Legal and repository hygiene

`/testdata/3DM/*.3dm` is ignored by Git. `git ls-files 'testdata/3DM/*.3dm'` returns no tracked files. No Cartesian source, extracted payload, converted copy, image, or screenshot was added. The local fixture is qualification input only. Later local corpus candidates are Furniture, Interior, Structure, and Manufacturing; none was opened for this milestone.

## Remaining qualification boundary

There is no 3DM importer or `convert-3dm` command yet. The X1 recovery toolkit now qualifies non-rational support candidates for the 53 unclassified rational edges; its evidence is in `3DM-RECOVERY-X1.md`. A canonical conversion still needs edge-use/trim and singular-boundary mapping, face/edge coherence, orientation and shell validation, then source-to-imported and STEP-reimport quantitative comparisons. Rational curves remain source evidence, not a required production representation. Accordingly, imported bbox, vertex/edge/surface/trim deviations, topology deltas, STEP entity counts, area, volume, centroid, allocation, and round-trip timings are **not available**; reporting zero for them would be false.

## Validation

The full solution build completed with zero errors (seven existing Web.Runtime analyzer/WASM warnings). The local 3DM qualification test passed. The full solution test run did not pass: 14 failures appeared in existing Core, Modules, Firmament, Server, and EditableV8 tests, while all 446 CLI tests passed. A representative unrelated failure, `SurfacingM1Tests.NonRationalBSplineContractRejectsInvalidDataAndHasNoWeights`, reproduced alone: it asserts that `BSplineSurfaceWithKnots` has no `Weights` property, though that property already exists in unchanged kernel code. The complete test output is local at `artifacts/local/3dm-full-test.log`.
