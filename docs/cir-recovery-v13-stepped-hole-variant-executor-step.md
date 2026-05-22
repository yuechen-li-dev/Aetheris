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

## V13.1 blocker characterization
Observed production failure: repeated overlapping coaxial subtracts on the canonical stepped stack produce `BooleanFailed` in current exact Boolean path.

Characterization summary:
- baseline order (`small -> medium -> large`) fails,
- failure aligns with overlapping/coincident coaxial tool topology handling,
- no bounded deterministic safe alternative route was promoted in this milestone,
- unioned-tool route is explicitly deferred (not enabled in production without dedicated stability coverage).

## A2 execution policy update
After A1/A1.1 validator admission fix, stepped execution is now probed in `HoleRecoveryExecutor`, but production remains deferred due to downstream subtract-stage blocker diagnostics.

Execution route:
- create host box,
- subtract small through cylinder,
- subtract medium depth cylinder,
- subtract large shallow cylinder,
- if any subtract stage fails (or probe is not yet production-hardened), return deferred unsupported status with explicit blocker diagnostics.

Diagnostics now record stepped plan validation and each subtract invocation/success.

STEP smoke coverage confirms exporter compatibility using existing `Step242Exporter.ExportBody(...)` behavior only.

## Non-goals
No generic N-level stack executor, no threaded-hole support, no STEP exporter changes, no public API changes.

## Next architecture milestone
Introduce a dedicated stepped-tool execution architecture before enabling stepped exact execution, e.g. one of:
1. bounded stepped tool builder + validated single subtract,
2. unioned coaxial tool composition with dedicated Boolean stability contracts,
3. dedicated profile-stack executor/topology builder for stepped coaxial reliefs.
