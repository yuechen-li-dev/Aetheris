# CIR-RECOVERY-V13 / V13.1: bounded stepped-hole variant + executor characterization + gate restoration

## Supported bounded CIR shape
`Subtract(Subtract(Subtract(Box, SmallThroughCylinder), MediumDepthCylinder), LargeShallowCylinder)`.

Constraints:
- exactly three cylindrical levels,
- all translation-only wrappers,
- Z-axis cylinders,
- coaxial in XY,
- radius ordering: `large > medium > small`,
- depth ordering: `large < medium < through`,
- medium/large must touch an entry face and must not be through,
- largest radius must pass strict host XY clearance.

## Relation to counterbore
Counterbore remains the 2-level shape. Stepped-hole admission requires 3 levels, so it does not steal counterbore/through/blind/countersink cases.

## Plan shape
- `HoleKind.Stepped`
- `HoleDepthKind.ThroughWithEntryRelief`
- `HoleEntryFeatureKind.Stepped`
- profile stack: large shallow cylinder, medium depth cylinder, small through cylinder
- expected annular transition floors and circular transition trims.

## A4.1 reconciliation update
Stepped execution is reverted to deferred in production pending plan/executor contract reconciliation:

- FrictionLab repeated-subtract success remains valid for lab-built route/tool placement.
- Production `HoleRecoveryPlan` lacks per-tier entry-side polarity for medium/large stepped tiers.
- Executor now returns `UnsupportedPlan` with `missing-stepped-entry-side-polarity` instead of attempting non-equivalent stepped Boolean execution.
- STEP smoke for stepped is therefore intentionally deferred in production until that contract gap is closed.

## Non-goals
No generic N-level stack executor, no threaded-hole support, no STEP exporter changes, no public API changes.

## Next architecture milestone
Introduce a dedicated stepped-tool execution architecture before enabling stepped exact execution, e.g. one of:
1. bounded stepped tool builder + validated single subtract,
2. unioned coaxial tool composition with dedicated Boolean stability contracts,
3. dedicated profile-stack executor/topology builder for stepped coaxial reliefs.
