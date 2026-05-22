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

## A4 productionization update
Stepped execution is now production-enabled for the bounded canonical route selected by A3.1:

- deterministic order: `small -> medium -> large`,
- bounded pre-boolean shape validation,
- per-stage subtract diagnostics,
- successful completion now returns `Succeeded` + produced `BrepBody`.

If a subtract stage fails, executor now returns `BooleanFailed` for that stage with explicit stage diagnostics.

STEP smoke coverage now executes for stepped-hole via the recovered body and confirms manifold/non-void export markers using unchanged `Step242Exporter.ExportBody(...)` behavior.

## Non-goals
No generic N-level stack executor, no threaded-hole support, no STEP exporter changes, no public API changes.

## Next architecture milestone
Introduce a dedicated stepped-tool execution architecture before enabling stepped exact execution, e.g. one of:
1. bounded stepped tool builder + validated single subtract,
2. unioned coaxial tool composition with dedicated Boolean stability contracts,
3. dedicated profile-stack executor/topology builder for stepped coaxial reliefs.
