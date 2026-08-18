# M5 contact and tangency audit

Audit date: 2026-08-13

## Reusable domain-neutral substrate

| Area | Existing behavior | Classification | M5 use |
|---|---|---|---|
| `SignedSideQuery` | Bounded patch/plane strict side, crossing, candidate zeros, interval budgets | generic; whole-domain; certified or sampled | Preserved as the authoritative sidedness sub-result and saddle guard. |
| `ClosestPointQuery` | Bounded point/curve/patch distance and parameter candidates | generic; whole-domain optimization; tolerance-bounded/structural | Reused indirectly through M4; no second locator was introduced. |
| `IntersectionQuery` | Curve/plane, patch/plane, curve/patch, patch/patch relation and non-authoritative witnesses | generic; bounded; mixed certified/tolerance evidence | Direct candidate and relation source for every M5 family. |
| first/second jets | Regularity, tangent planes, authored derivatives | generic; local differential; tolerance interpreted | Used only after an M4 witness is established. |
| `CurvatureQuery` | invariant curve curvature, fundamental forms, principal and arbitrary normal curvature | generic; local differential; tolerance-bounded | Reused for curve/patch and directional surface second-order comparison. |
| Panel G0/G1/G2 | sampled seam position, tangent-plane angle, transverse normal-curvature residual | CAD-specific engineering semantics; sampled | Dogfood mapping only; Panel API remains unchanged. |

## CAD/topology-specific logic left in place

- Kernel BRep Boolean recognizers contain many analytic-family `TangentContact` rejections. They protect supported constructive families and zero-thickness topology; they are not generic differential contact classifiers.
- BRep spatial ray queries mark near-tangent or duplicate/coincident hits for point-in-body voting. This is whole-body/topological classification, not reusable contact order.
- Tessellation tangent deviations and coincident-source checks are approximation/mesh controls.
- Surfacing materializers and fillet/blend routes own construction intent and remain separate.
- STEP recovery normal/tangent checks are imported-geometry reconstruction evidence, not generic proof.

## Continuum and FEA seams

Continuum currently owns region/CutCell classification rather than authored-geometry contact. FEA boundary enforcement owns quadrature, support, and solver policy; it does not implement nonlinear mechanical contact. M5 can later supply candidate classification or interference evidence to these systems, but no solver/contact response was added.

## Dangerous conflations found and prevented

1. M4 `ContactObservation` used the word contact for a local differential observation. M5 leaves it in place for compatibility but gives the public claim separate `ContactClassification` and `ContactOrderEvidence` types.
2. A near-zero closest distance was already correctly prevented from becoming touching/coincidence. M5 preserves this gate.
3. A zero gradient can be mistaken for tangent-only contact. The mandatory `z=u²-v²` fixture makes whole-domain crossing override the stationary origin jet.
4. Equal principal-curvature values alone can hide direction/mixed-term differences. M5 compares normal curvature in three shared physical directions.
5. Panel G2 compatibility can be mistaken for coincidence. M5 reports `SecondOrderCompatible`, never structural coincidence, for distinct identities.
6. Finite floating-point jet agreement can be mistaken for exact higher multiplicity. M5's quartic fixture stops at `AtLeast 2` / `HigherOrderCandidate`.

No broad refactor was required. Generic M5 code is owned by `Aetheris.Geometry`; CAD, BRep, Continuum, and FEA authority boundaries are unchanged.
