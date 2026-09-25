# STEP recovered pcurve X1

**Verdict: Meaningful progression, with the mandatory McMaster witness qualified.** The normal exact-BRep import path now constructs and qualifies a face-local pcurve for every one of its 810 coedge uses. The source supplies none. The body remains one enclosed manifold with 147 faces, 405 edges, and 268 vertices; its X0 geometry recovery remains 160 curves, 3 surfaces, and 0.023580323 mm worst sampled geometry deviation. A separate NIST part still has a partial recovered trim set, so the broader corpus qualification target remains open.

## Binding and tolerance

The importer retains and validates source pcurves first. When geometry recovery was needed, it reconstructs missing coedge pcurves from each edge curve and supporting face. Planes use affine projection, cylinders and cones use analytic inverse frames, and sphere and torus inverses use their existing analytic parameterizations. Periodic angles are unwrapped along each trim. Cone trims that were previously excluded now use a bounded cubic UV curve; each candidate is accepted only after its lift back to the support is sampled against the unchanged 3D edge. Spline supports use a bounded grid and dense domain-boundary initialization at the first point, then neighboring UV seeds with damped Gauss–Newton and bounded pattern refinement. Polyline sampling doubles from 129 up to 4097 only as required by measured 3D lift error.

`ImportPolicy.PcurveQualificationToleranceMillimetres` defaults to **0.001 mm**, distinct from kernel topology tolerances and the **0.1 mm** default geometry recovery budget. For a recovered edge or support, the local allowance includes 1.25 times the sum of their measured X0 deviations and never exceeds the geometry budget. Each recovered binding records origin, method, surface family, local tolerance, lift sample count, and maximum sampled edge-to-lift deviation; source STEP identity remains on imported bindings. The importer also runs the independent pcurve validator over every coedge, including endpoints, sense, periodic UV loop closure, and surface domains. A body with incomplete bindings remains inspectable but reports `inspectable-unqualified`; export suppresses its partial recovered pcurve set and retains valid source pcurves.

## McMaster evidence

The local witness is `91280A546_Medium-Strength Class 8.8 Steel Hex Head Screw.STEP`, supplied outside the repository. Before X1, diagnostic reconstruction produced 579 bindings and left 54 cone and 177 spline inverse failures. The completed import produces **810/810 bindings, zero failed bindings**, including **54 cone**, **317 cylinder**, **134 plane**, and **305 spline-support** uses. The worst sampled pcurve lift deviation is **0.031310873 mm** under its local qualification allowance; this is separate from the **0.023580323 mm** geometry recovery deviation. A coarse first-sample spline grid missed a narrow boundary solution on face 143 / edge 255; bounded boundary search resolves that initialization failure. The witness remains tessellatable and exports to STEP with pcurve associations. Reimport validates all 810 source pcurves, preserves face and edge counts, and passes the existing sampled corresponding-spline-edge drift bound of 0.01 mm.

The `analyze --json` `pcurveQualification` object reports `qualified`, total/source/recovered/failed binding counts, worst sampled lift deviation, base tolerance, and surface-family counts. A measured CLI wireframe run took **4.26 seconds** on this machine, including import, pcurve qualification, and SVG output; the X0 analyze path had taken about two seconds, but these are different commands, not a controlled before/after timing comparison. The local wireframe output and raw JSON live under ignored `artifacts/local/step-recovered-pcurve-x1/`; they are diagnostic artifacts, not committed fixtures.

## Export parameterization

STEP export rewrites 3D lines from their start vertices, so recovered UV pcurves on those lines must receive the same affine parameter change. The exporter maps their spline knots or line/polyline domains before writing `PCURVE`. It also names recovered pcurve representations with their bounded local qualification tolerance. Reimport treats that name as a tolerance claim and independently checks the lifted curve and loop; ordinary source pcurves continue to use source accuracy. A source pcurve with a disjoint spline domain receives a specific diagnostic instead of an interval-constructor exception.

## Scope and limits

Measured deviations are sampled bounds, not mathematical suprema. Sphere and torus analytic inverse paths exist, but the supplied bolt exercises only plane, cylinder, cone, and spline supports. A NIST CTC part with four recovered surfaces currently reports a partial trim set (`inspectable-unqualified`); its STEP export intentionally omits those partial recovered pcurves. This does not change that part's pre-X1 interchange result. The source McMaster STEP file remains uncommitted.

## Validation

The local McMaster witness test passes import, all-binding qualification, independent pcurve validation, tessellation, bound STEP export/reimport, and sampled spline drift. The fast kernel lane passes **1,002/1,002**; the serial full kernel corpus passes **1,135/1,135**. The full serial solution gate has one pre-existing unrelated failure: `MeshObj_HexBolt_ExportsStructuredPolygonsDirectlyFromSurfaceMeshIr` expects 905 OBJ polygons and sees 908. The other test projects, including Server, pass. The full solution builds with zero errors and seven Web.Runtime warnings. `git diff --check` passes.

## Reproduction

```powershell
$env:AETHERIS_STEP_NURBS_WITNESS = '<local McMaster STEP path>'
dotnet test Aetheris.Kernel.Core.Tests -c Release --filter FullyQualifiedName~Step242LocalRationalWitnessTests
dotnet run --project Aetheris.CLI -c Release -- analyze $env:AETHERIS_STEP_NURBS_WITNESS --json
dotnet run --project Aetheris.CLI -c Release -- wireframe $env:AETHERIS_STEP_NURBS_WITNESS --out artifacts/local/step-recovered-pcurve-x1/mcmaster-bolt.svg --view iso --density 8 --json
```
