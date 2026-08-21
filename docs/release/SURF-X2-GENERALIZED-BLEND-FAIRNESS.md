# SURF-X2 generalized blend and fairness judgment

## Executive verdict

**Meaningful progression.** Aetheris can generate and deterministically choose among high-quality G1/G2 transition surfaces for the qualified two-support housing-crown/planar-shoulder lane, and the selected patch flows through the real SURF-X1a trim, pcurve, `BodyState`, preservation, and rational-free STEP path. The broader SURF-X2 surface-pair matrix and variable-law scope are not complete, so this report does not claim general production blend support.

## Blend mathematics

Candidate `m` is the exact tensor-product graph

```text
x = -width/2 + width*u
y = -depth/2 + depth*v
z = baseZ + height * g_m(u) * g_m(v)
g_m(t) = 4^m [t(1-t)]^m
```

for `m=2..5`. It is emitted exactly as a clamped, single-span, non-rational polynomial B-spline of degree `2m`. `m=2` has zero first but nonzero second transverse derivative at a shoulder and is G1-only. `m>=3` has zero first and second transverse derivatives.

The bounded G2 contract is position equality, tangent-plane equality, and transverse normal-curvature equality to the planar shoulder at 33 fixed parameters per side. It is a normal-curvature formulation; it does not claim global principal-direction compatibility. Candidate evidence uses analytic polynomial jets. The realized selected B-spline is independently checked from its clamped boundary control-net second differences.

Fairness is a fixed 25×25 quadrature of `integral(k1^2+k2^2)dA`. Curvature variation is the normalized sum of adjacent mean-curvature differences. Compactness is the sampled fraction above one percent of requested height. Complexity is the actual post-normalization control-point count. No numerical optimizer is used, so convergence and stochastic-seed concerns do not apply.

## Judgment policy

Every candidate is normalized to a non-rational B-spline, materialized through the housing BRep builder, and gated for requested continuity, degree limit, trim contract, pcurves, manifold validity, self-intersection evidence, locality, and preserved semantics before scoring. `StandardBlendJudgment/v1` weights fairness `0.40`, curvature variation `0.30`, compactness `0.20`, and complexity `0.10`. Considerations use deterministic candidate-set min/max normalization and `[0,1]` clamping. Highest utility wins; equal utility uses lower materialized control-point count and then ordinal `CandidateId`. Source/generation order is not a tie-break.

An explicit `Preferred: G2, Minimum: G1` permits fallback. Exact `Minimum: G2` fails if no G2 candidate survives. `UseCandidate` can select a different eligible candidate and is recorded as a manual override.

## Flagship candidate evidence

Input: `100 × 80 × 20 mm` housing, `80 × 50 mm` authorized transition, `8 mm` crown height, four protected mounting holes. Values below are from the canonical CLI build.

| Candidate | Eligible for G2 | G1 error (deg) | G2 error (1/mm) | Bending energy | Curvature variation | Footprint | Controls | Utility |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `PowerM2Degree4` | no | 0 | 0.1024 | — | — | — | 25 | — |
| `PowerM3Degree6` | yes, selected | 0 | 0 | 4.1367937876 | 0.3846414874 | 0.6544 | 49 | 0.800000 |
| `PowerM4Degree8` | yes | 0 | 0 | 4.7605539756 | 0.3946031772 | 0.5392 | 81 | 0.604060 |
| `PowerM5Degree10` | yes | 0 | 0 | 5.4642390046 | 0.4139407492 | 0.4880 | 121 | 0.200000 |

`PowerM3Degree6` won because it has the lowest bending energy and curvature variation and the lowest G2-eligible representation complexity. The more compact higher-order candidates do not recover enough compactness utility to offset their curvature and complexity costs. The G1 candidate is rejected, not assigned a low score.

## Realized geometry and preservation

The selected candidate is committed through `ReplaceRegionSculptor`. The realized BRep has shared trim edges, two pcurves per edge, valid orientation, edge incidence two, and no preflight errors. Independent locality comparison below `z=20 mm` reports zero deviation and identical lower planes/cylinders/circular trims. `BottomMountingInterface`, `MountingHolePattern`, `OuterFootprintBoundary`, and `SideWallsLower` retain exact semantic fingerprints; AP242 association remapping uses explicit correspondence.

Representation inventory:

```text
Planes: 6
Cylinders: 4
Cones: 0
Spheres: 0
Tori: 0
NonRationalBSplines: 1
RationalProductSurfaces: 0
```

The generated manual artifact is `artifacts/local/surf-x2/surf-x2-judged-blend-housing.step` (SHA-256 `4b5f4a0d36fef35af5f31c889496321d97cfd96a4276e6af56065c6401dd85c8`), with its sibling decision/provenance JSON. `candidate-a-g1.step` and `candidate-c-power-m4.step` provide additional local comparisons. Generated output remains ignored by policy.

## Human inspection

Automated utility is mathematical engineering evidence, not aesthetic approval. A human result has not yet been recorded. Inspect highlight flow, flat spots, curvature bumps, boundary creases, intended symmetry, transition locality, and mounting/interface preservation in a capable STEP viewer.

## Relationship to Fillet and Chamfer

Existing bounded Fillet and Chamfer implementations already use `JudgmentEngine` for their own admissible strategy choices. X2 does not gratuitously reroute them. A constant-radius Fillet is conceptually a more constrained generalized blend; a planar Chamfer is an exact planar transition and should not be lowered through a freeform B-spline when the analytic representation is better.

## Fresh-agent requests

| Request | Result in qualified lane |
|---|---|
| A — required G2 crown-to-side-support transition | `BlendBoundary`, hard G2 gate, candidate generation, judgment |
| B — prefer smoother with slightly larger patch | express with larger bounded `RegionSize`; standard fairness remains explicit; custom weight authoring is deferred |
| C — more compact without curvature spikes | higher-order eligible candidates expose compactness and curvature separately; custom weights are deferred |
| D — G2 mandatory or fail | `Minimum: G2`; regression proves no G1 fallback |
| E — explain selection | CLI JSON/text and `.delta.json` expose rejection, metrics, score, tie-break, and winner |
| F — export selected surface | STEP contains analytic supports plus one non-rational B-spline and zero rational product surfaces |

## Validation recorded during implementation

- Release build of the CLI dependency graph: passed with zero warnings/errors.
- Full serial .NET suite: 3,140 passed, zero failed. The legacy-gated FrictionLab assembly reports no discoverable tests under the ordinary solution invocation, consistent with its gate.
- Focused Firmament/SURF-X2 tests: 4 passed; focused CLI judgment-report test: 1 passed.
- Canonical qualification: 97 fixtures passed, including three X2 valid witnesses.
- Canonical real CLI build and STEP reimport: one enclosed body, 11 faces, 28 edges, 20 vertices, bounds `[-50,-40,0]` to `[50,40,28] mm`.
- Canonical surface inventory: 6 planes, 4 cylinders, 1 non-rational B-spline, 0 rational surfaces.
- Client: 82 tests, TypeScript/production build, and lint passed.
- VS Code extension: 13 tests, typecheck, build, and VSIX package passed.
- Fresh self-contained win-x64 CLI publish and canonical X2 validation passed.
- Repository layout guard (3,726 tracked files) and `git diff --check` passed.
- NativeAOT Forge validation was not rerun because Forge and its host/interop boundary were not changed.

Manual visual approval remains outstanding and is deliberately separate from mathematical utility evidence.

## Deferred scope

Plane/Cylinder and Cylinder/Cylinder generalized blends, general freeform-to-freeform boundaries, variable blend laws, bounded control-point optimization, candidate STEP fan-out, arbitrary imported-face blend authoring, and N-way junctions are not implemented. Fillet/Chamfer public semantics are unchanged.
