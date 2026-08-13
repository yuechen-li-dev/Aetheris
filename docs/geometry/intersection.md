# Bounded intersection predicates

Status: AETHERIS-GEOMETRY-REASONING-M4

## Kernel law

> Generic numerical intersection may establish geometric evidence, but it does not author semantic topology.

`IntersectionQuery` observes bounded authored geometry. It does not create trim curves, split faces, change a BRep, run a Boolean, or confer export authority on a witness. Construction-specific algorithms remain separate when source intent already fixes the analytic family and expected topology; `TransverseConePlaneIntersection`, for example, remains the admitted cone/world-Z hyperbola construction.

## Public matrix

`IntersectionQuery.Between(a, b)` and `Between(a, b, policy)` support both operand orders for:

- bounded curve / plane;
- bounded patch / plane;
- bounded curve / bounded patch;
- bounded patch / bounded patch.

Curve/curve is intentionally deferred: it is optional in M4, and adding another shallow sampler would not improve the patch-focused evidence model.

`IntersectionPolicy` records linear, angular, and parameter tolerances; iteration and subdivision budgets; and the evidence preference. Every result returns the policy actually used, operand identities/provenance, bounded domains, ordered point witnesses and parameters, geometric residuals, statistics, typed diagnostics, and a plain-language provenance statement.

## Relation semantics

- `Disjoint`: the complete bounded domains are separated by the reported evidence. Certified signed-side or analytic scalar evidence is preferred; M3 distance convergence can provide tolerance-bounded separation.
- `Crossing`: strict opposite-side continuity evidence, or an interior near-zero candidate with transverse local differential evidence, has been established. A sampled policy never promotes its conclusion to `Certified`.
- `Touching`: a whole-domain one-sided argument and a compatible local tangent/contact observation are both available. A near-zero residual alone is insufficient.
- `Coincident`: structural identity or a bounded analytic structural-zero case is available. Numerical near-zero never earns this state.
- `Overlapping`: reserved for future certified partial-locus overlap. M4 does not infer it from dense sampling.
- `Unknown`: geometry is valid, but budget, unsupported interval operations, singular jets, ambiguous overlap, conflicting local evidence, or insufficient global evidence prevents an honest stronger conclusion.

`IsDefinitelyDisjoint` is a convenience projection over the richer result; it is not the primary API.

## Algorithms and evidence

Curve/plane reduces to `g(t) = dot(n, C(t) - p0)`. Analytic line endpoints give exact affine cases. Expression curves use the existing outward-rounded interval evaluator for strict separation and global one-sided evidence. Opposite strict signs plus continuity establish crossing; deterministic bisection produces a witness. A stationary near-zero candidate becomes `Touching` only with second-jet evidence and a whole-domain one-sided interval.

Patch/plane always invokes `SignedSideQuery`. Positive or negative maps to `Disjoint`; crossing maps to `Crossing`. When SignedSide remains inconclusive, its expression interval plus a tangent-plane contact candidate may establish tolerance-bounded touching. Otherwise the result remains `Unknown`.

Curve/patch and patch/patch first call M3 `ClosestPointQuery`. A stabilized distance above tolerance gates `Disjoint`. A near-zero candidate is then inspected with first jets. Curve tangent versus patch normal, or the two patch normals, supplies local transversality. A small deterministic interior candidate lattice prevents an arbitrary boundary minimum on a zero-distance locus from hiding an interior crossing. Compatible tangent planes remain a contact candidate and normally return `Unknown`; M4 does not trace a surface/surface intersection curve.

## Witness authority and future contact seam

`IntersectionWitness` is evidence only. `WitnessesAreAuthoritativeTrims` is permanently false, and the public result contains no BRep, trim, Boolean, face, edge, or mutation operation. No witness curves are emitted in M4, avoiding accidental trim reuse.

`ContactObservation` records tangent/normal and available second-jet observations. It deliberately does not expose multiplicity or contact order. A future `ContactClassification` / `ContactOrder` query can consume these observations without changing intersection authority.

## Tolerance and determinism

Tolerance is interpretation, not coordinate rounding. Residuals remain raw. Stable traversal orders are used for samples, subdivisions, bisection, candidate lattices, witnesses, and diagnostics. Budget exhaustion returns `Unknown` rather than a local answer that ignores unresolved regions.

