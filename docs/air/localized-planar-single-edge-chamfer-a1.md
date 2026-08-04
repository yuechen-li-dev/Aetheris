# AIR-CHAMFER-LOCALIZED-PLAN-A1

`AirLocalizedPlanarReplacementChamferCompiler` is the first production AIR route for a bounded
edge replacement. It admits only a history-known axis-aligned Box and the semantic selector
`Face: +X, Target: SharedEdgePlusZ` (the shared finite +X/+Z edge), with a positive equal distance
smaller than the Box width and height.

The Feature AIR selection has no emitted B-rep identifiers. Its Construction AIR witness owns the
two planar support faces, the original finite edge, both trim lines, the chamfer quad, retained
polygons, material side, and `ExplicitOwnedEndpoints`. The authoritative plan then owns all ten
vertices, fifteen edges, seven ordered loops/faces, retained-face roles, the new chamfer face, and
a deterministic signature. `AirLocalizedPlanarReplacementEmitter` consumes that plan directly;
it does not call `BrepBoundedChamfer`, topology grafting, or a Boolean.

Canonical `10 x 8 x 6, d=1` and variations `d=2` and `12 x 5 x 7, d=1` export with Enforce
preflight and STEP reimport as 10 vertices, 15 edges, and 7 planar faces. The report records
`LocalizedPlanarReplacement`, `Direct`, explicit endpoint ownership, an authoritative plan, and
`legacyFallback: false`.

Two simultaneous EdgeFinish declarations are intentionally parsed only far enough to return
`localized-chamfer-construction-witness-required:two-edge-junction`. A deterministic junction
patch and its retained/replacement ownership have not been proved, so no body or STEP is emitted.
Utility scoring remains unused: the admitted single-edge context has exactly one candidate.

The reusable fillet infrastructure is semantic selection, retained/replacement ownership,
endpoint policy, authoritative plan emission, admission, preflight, and STEP proof. Fillets still
need tangent arc/blend geometry, cylindrical/toroidal surfaces, and continuity/radius admission.
