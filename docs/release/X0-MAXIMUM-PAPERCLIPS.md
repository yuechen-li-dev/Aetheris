# X0 — MAXIMUM PAPERCLIPS

## Executive verdict

Success. Aetheris can author a recognizable, parametric, manufacturable paperclip from typed Firmament source, lower it through semantic Sweep AIR into an analytic enclosed BRep, round-trip it through STEP AP242, invoke the same Template through Forge Host Protocol v1, and regenerate it interactively in Cadmata. The default build creates exactly one paperclip. Planetary resources remain unmodified.

## Sweep architecture

X0 reuses the existing domain-neutral `Concept Path` representation introduced for profile programming. It does not create a parallel `SweepPath` language. Template and `Static` expansion happen first; the path resolver then retains ordered named line and circular-arc segments, endpoints, stable identities, and provenance in `ResolvedConceptPath2D`.

`CircularSweepFeatureAir` is the explicit semantic boundary before topology. It contains the resolved path, constant diameter, resolved catalog material, clearance policy, and provenance. BRep construction consumes this AIR rather than parser syntax or a sampled point cloud.

The bounded frame is the XY path plane normal crossed with the local tangent. Circular symmetry makes rotation about the tangent irrelevant. X0 admits continuous, tangent, open XY paths only. Lines produce analytic cylindrical faces, arcs produce analytic toroidal faces, joins share circular ring edges, and the two physical wire ends receive planar caps. Closed paths are deliberately deferred.

Geometric validity and manufacturing policy are separate:

- kernel validity requires finite positive diameter, nondegenerate continuous/tangent segments, arc centerline radius greater than section radius, and no detected overlap;
- the Paperclip Template's inspectable `Require` clauses impose the stricter wire-forming policy (`BendRadius > WireDiameter`, loop clearance, and compatible overall dimensions).

Nonadjacent line/line clearance is exact. Checks involving arcs use a deterministic conservative 96-chord witness. This detects obvious overlap and may conservatively reject a near-limit design; it is explicitly not presented as a complete general curve-distance proof.

## Paperclip Template

The first flagship product is `Standard.Products.Office.Paperclip`. Its public source follows `Record → Static defaults → with overrides → Template<Policy> → Struct` and uses the real material identity `Standard.Materials.StainlessSteel.304_Annealed`.

Default metric policy:

| Parameter | Default |
|---|---:|
| WireDiameter | 0.8 mm |
| OverallLength | 33 mm |
| OuterWidth | 9 mm |
| InnerWidth | 5 mm |
| BendRadius | 1 mm |
| LoopGap | 1 mm |
| Material | 304 annealed stainless steel |

The readable centerline has seven semantic segments: four lines and three tangent arcs. Derived values expose wire length, volume, catalog-density mass, envelope, paperclips per metre of wire, and STEP identity. The dimensions are plausible demonstration defaults and make no commercial or standards-compliance claim.

## Geometry evidence

The canonical default build reported:

| Check | Result |
|---|---|
| Segment count | 7 |
| Wire diameter | 0.8 mm |
| Centerline length | 131.132741 mm |
| Analytic volume | 65.914505 mm³ |
| Material mass | 0.520725 g |
| Authored solid envelope | `[-2.4,-0.4,-0.4] → [7.4,33.4,0.4]` mm (9.8 × 33.8 × 0.8 mm) |
| Surface families | 4 cylinders, 3 tori, 2 planes |
| Topology | enclosed manifold |
| STEP reimport | success; enclosed manifold |
| STEP SHA-256, repeated builds | `6cdcbfbb407cbcf86d26f0e59482e776e8fd6fa02158df67c800fc4afc32c305` both times |

The general STEP analyzer's vertex/trim-derived box reports only 29.5 mm in Y for this toroidal model because it does not include interior extrema of trimmed analytic faces. Sweep qualification therefore uses the analytic centerline-plus-section envelope above; this analyzer limitation is recorded rather than concealed.

Representative warm-process phase timings from the direct canonical build were 42.7 ms Template parse/bind, 14.1 ms canonical parse, 243.3 ms AIR/path lowering and material resolution, 52.8 ms BRep construction, 18.1 ms STEP export, and 32.9 ms STEP reimport. A Protocol v1 process invocation reported 741.9 ms execution (1,479 ms wall time including `dotnet run` process startup). These are qualification observations, not benchmarks.

## Forge and demo

Forge Host Protocol v1 needs no product-specific RPC:

```text
list
describe Standard.Products.Office.Paperclip
invoke Standard.Products.Office.Paperclip --request ... --out ...
```

`describe` exposes the typed `PaperclipPolicy` record, units, constraints, and STEP artifact. `invoke` specializes through the normal Firmament binder and produces `paperclip.step`. Repeated identical invocations have identical specialization and artifact hashes.

The Cadmata `MAXIMUM PAPERCLIPS` tab sends bounded metric policy values to `/api/v1/demos/maximum-paperclips`. The server specializes the same Template and returns its normal AP242 artifact. The client imports that STEP through the existing viewer path; it does not draw a handcrafted paperclip mesh. The generated STEP is directly downloadable. Status reports parametric, manufacturable, AP242, deterministic, and—correctly—planetary resources unmodified.

## Fresh-agent results

Two clean-room agents were restricted to `docs/public/` and `fixtures/Canonical/`:

1. The Paperclip task found `Templates/paperclip.firmament` on its first attempt, proposed `StandardPaperclip with { WireDiameter: 1mm; OuterWidth: 11mm; InnerWidth: 6mm; BendRadius: 1.2mm; LoopGap: 1.2mm }`, retained inherited stainless material, and changed only the Template specialization. It did not rewrite Sweep geometry.
2. The bent-wire task found `Features/Sweep/circular-planar-path-sweep.firmament` on its first attempt and produced a valid U path using `Line → Arc(-180deg) → Line`, then a 1.2 mm circular Sweep with the catalog stainless material.

## Limitations and follow-up

X0 intentionally supports constant circular sections on open planar XY Concept Paths made from tangent line/arc segments. It does not support arbitrary 3D guides, variable sections, rails, twist laws, lofting, surface-only output, general piping, or closed rings. Generic planar-plane detection, exact arc/arc minimum-distance qualification, closed-loop topology, and a face-extrema-aware STEP analyzer box are appropriate follow-ups—not hidden X0 claims.

## Bugs found and disposition

- Template-backed CLI `validate` and `inspect` initially selected Sweep before Template/Static expansion. Both now use the same expansion route as `build`, with CLI regression coverage.
- Widening path measures for policy arithmetic initially consumed the next compact same-line property. Property termination now recognizes both expression whitespace and a following `Name:` token; the existing semantic path regression test and Sweep tests pass.
- The initial invalid Paperclip fixture put declarations on one line and exercised parser formatting instead of policy. It now isolates the intended `OuterWidthExceedsInnerWidth` `Require` diagnostic.
- STEP analyzer bounds omit trimmed torus interior extrema. No brittle analyzer patch was added during X0; analytic Sweep bounds are the qualification authority and the limitation is documented above.
- Solution-wide concurrent test execution exposed pre-existing wall-clock ratio tests in `RecognizedConstructionRecipeTests` as load-sensitive (different timing assertions failed on separate runs). The unchanged Kernel Core suite passes 961/961 in isolation; final full qualification ran test projects sequentially to avoid cross-assembly benchmark interference.

## Validation

- Release solution build: passed, 0 warnings / 0 errors.
- Full .NET solution suite: passed after the compact-property regression fix; 3,044 tests across test-bearing projects, 0 failures when projects run sequentially (the empty FrictionLab test assembly reports no discoverable tests).
- Focused Sweep/AIR/BRep/STEP tests: 5 passed, including analytic surfaces, manifold topology, bounds, material-derived mass, invalid bend, and disconnected AIR.
- CLI semantic route: 1 passed; canonical `validate`, `inspect`, and `build` also succeeded directly.
- Forge Host: 25 passed in the full suite; focused Protocol v1 run passed all 15 selected tests.
- Server demo integration: passed through real Template specialization and STEP round-trip.
- Canonical qualification: 69/69 fixtures passed, including all three neighboring Sweep shapes and both Paperclip fixtures.
- Invalid qualification: zero diameter, disconnected path, non-XY path, insufficient bend radius, self-intersection, and invalid policy each produced its single intended engineering diagnostic.
- Client: 16 files / 82 tests passed; production build and lint passed.
- VS Code extension: 13 tests passed; build and typecheck passed. No `lint` target is defined.
- Public documentation/cookbook and canonical coverage manifests: qualified by the canonical script and both clean-room tasks.
- Determinism: two direct default builds were byte-identical at the SHA-256 above; Forge repeated-invocation coverage also passed.
- `git diff --check`: passed.

The X0 convergence state is **Success**.
