# Worst-case tolerance stackup

Existing Firmament `let` syntax supports typed scalar and one-level Record values, bilateral `tol 0.05mm`, asymmetric `tol +0.10mm -0.05mm`, `mm`/`deg` unit checks, exact-alias tolerance preservation, and explicit warnings when arithmetic drops tolerance. M0 uses the same nominal/lower/upper convention in `TolerancedDimensionBinding`; it does not perturb BRep geometry.

Assembly dimensional relations form a signed graph over instance-scoped semantic datums. Edges contain nominal, lower/upper tolerance, unit, sign/orientation, originating instance, source/Table/Template provenance, and optional Mate/Interface transition provenance. Only explicitly known engineering dimensions and zero-offset interface transitions enter the graph.

```firmament
Assert ToleranceStackup AxialReach {
    Between: [BearingModule.FixedSupport.Housing.Datum,
              BearingModule.Rotor.Shaft.Shoulder];
    Require: Clearance >= 44.90mm;
}
```

The compiler enumerates deterministic simple paths. Zero paths are `assembly-tolerance-path-missing`; more than one is `assembly-tolerance-path-ambiguous`. It never picks an arbitrary route, so JudgmentEngine is unnecessary for M0. For the unique signed chain, nominal values sum directly. A forward contributor adds `[lower, upper]`; a reversed contributor adds `[-upper, -lower]`. This is bounded worst-case interval propagation—no RSS, distributions, Monte Carlo, or Six Sigma.

The bearing-module proof automatically crosses Housing (30 -0.04/+0.05 mm), a Mate transition, Bearing (10 ±0.02 mm), another Mate transition, Spacer (5 -0.02/+0.03 mm), and the final seating transition. Result: nominal 45.00 mm, worst case 44.92–45.10 mm. The 44.90 mm assertion passes. The paired failing fixture requires 44.95 mm and reports a typed assertion failure while retaining all five contributors.

Template Assembly definitions keep their dimensional graph private. An
explicitly exposed `Relation` is lowered to one public summary edge containing
the unique signed internal chain as structured `ExpandedContributors`. A parent
assertion therefore depends only on `LeftModule.Mount -> LeftModule.Drive`,
while evidence can expand the edge back to Housing seat, bearing width, spacer
width, and their Template/Static Table/Record/`with` provenance. No other local
relation becomes reachable across the encapsulation boundary.
