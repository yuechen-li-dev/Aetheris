# Evidence-aware contact reasoning

Status: AETHERIS-GEOMETRY-REASONING-M5

## What contact adds

`IntersectionQuery` answers whether bounded objects are disjoint, cross, touch, coincide, overlap, or remain unresolved. `ContactQuery` consumes that M4 result and asks a different, local differential question: at an established witness, are the objects transverse, first-order compatible, or second-order compatible?

The two claims remain separate. `Tangent` is local. `Touching` is an M4 whole-domain relation requiring no-crossing evidence. A locally tangent curve/patch result can therefore carry `IntersectionRelation.Unknown`; this is intentional, not contradictory.

The public overloads are:

```csharp
ContactQuery.Between(curve, plane);
ContactQuery.Between(patch, plane, policy);
ContactQuery.Between(curve, patch, policy);
ContactQuery.Between(patchA, patchB, policy);
```

Both operand orders are supported. `ContactPolicy` records linear, angular, curvature, and parameter tolerances; iteration/subdivision budgets; and the maximum derivative order observed (one or two in M5). Every result records the policy actually used.

## Result semantics

`ContactQueryResult` reports nullable `ContactExists` (`true`, `false`, or unresolved), classification and evidence, local/whole-domain/structural scope, witnesses and authored parameters, the composed distance/intersection/side relations, tangent and normal relations, curvature observations, separate `ContactOrderEvidence`, statistics, typed diagnostics, identities, and provenance.

- `Disjoint` is a whole-domain conclusion inherited from M4.
- `Crossing` preserves a whole-domain side change that need not be transverse at every zero. The saddle `z=u²-v²` crosses its plane although its gradient vanishes at the origin.
- `Transverse` is a local regular contact whose first-order tangent spaces are not compatible.
- `Tangent` is local first-order compatibility. It says nothing by itself about global touching.
- `SecondOrderCompatible` means geometric normal-curvature forms agree, within policy, in the compared directions. It is not coincidence.
- `HigherOrderCandidate` means every derivative M5 could observe in an admitted scalar reduction vanished within tolerance. It is not an exact higher order.
- `Coincident` requires structural evidence and has no finite contact order.
- `Unknown` is a normal result for missing witnesses, singular parameterizations, unavailable jets, exhausted evidence, or unsupported conclusions.

`ContactEvidenceScope` makes locality explicit. `HasTopologyAuthority` is permanently false.

## Contact order

Classification and order are separate results. `ContactOrderEvidence` has `Exact`, `AtLeast`, `Candidate`, and `Unknown` states, plus an optional order, optional proven lower bound, maximum derivative order checked, evidence, and diagnostic.

M5 admits an exact integer definition only for regular curve/plane scalar reduction:

```text
g(t) = dot(n, C(t)-p0)

order k at t*:
g(t*) = ... = g^(k-1)(t*) = 0
g^k(t*) != 0
```

All zero/nonzero decisions are tolerance-qualified. Regular `g=0`, nonzero `g'` supports local exact order 1. Regular `g=0`, zero-within-tolerance `g'`, and nonzero invariant `g''/|C'|²` supports local exact order 2. If `g`, `g'`, and `g''` all vanish within tolerance, M5 reports at least order 2 / `HigherOrderCandidate`; it does not report order 3 or 4.

Patch/plane, curve/patch, and patch/patch report directional first/second-order geometry without a generic integer multiplicity. General surface/surface contact is not forced into a scalar root model.

> Agreement or vanishing of all derivatives currently available to Aetheris does not imply infinite-order coincidence or establish a higher exact contact order without additional evidence.

## Directional surface evidence

Second-order patch comparisons use `CurvatureQuery.NormalCurvature`, not raw `Duu/Duv/Dvv` across unrelated parameterizations. M5 constructs two orthogonal physical tangent directions and their diagonal. Agreement in those three directions determines the symmetric normal-curvature quadratic form, including the mixed term. Opposite authored normal orientation is aligned before comparison.

Each `ContactDirectionalObservation` records the physical tangent direction, both oriented normal curvatures, their difference, tolerance-qualified relation, evidence, and diagnostic. Partial agreement becomes `DirectionDependent`. Principal directions are not required, so umbilics remain supported.

Curve/patch compares curve normal acceleration with patch normal curvature in the curve tangent direction. Patch/plane compares patch normal curvature with the plane's zero normal curvature.

## Evidence and tolerances

`Structural`, `Certified`, `ToleranceBounded`, `Sampled`, `Heuristic`, and `Unknown` retain their existing meanings. Representation kind is not evidence. Floating-point AD evaluation is tolerance-bounded, not algebraically exact. A derivative below tolerance is explicitly `ZeroWithinTolerance`; its raw value is retained.

The composed result preserves weaker sub-results. For example, certified `SignedSideQuery` intentionally cannot certify touching and may return `Unknown`, while M4's additional interval and local evidence establishes `IntersectionRelation.Touching`. M5 records both instead of silently upgrading SignedSide.

## Authority and consumers

Contact is observational. It cannot split topology, create trims, modify a BRep, reposition geometry, generate fillets, or produce collision response. Panel retains G0/G1/G2 engineering semantics: G1 maps to local first-order compatibility and G2 maps to second-order compatibility, but `ContactQuery` does not replace `PanelNetworkValidator`.

Future Continuum CutCell, FEA mechanical-contact candidate, and interference consumers can use classification as evidence input. M5 does not implement nonlinear contact mechanics or boundary authoring.

## Future work

Possible later work includes higher-order curve jets, certified higher contact order for admitted scalar/algebraic families, specialized algebraic contact, and certified overlap loci. M5 intentionally does not add arbitrary-order AD or patch third jets.
