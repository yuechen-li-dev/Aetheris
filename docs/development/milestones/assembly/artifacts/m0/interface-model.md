# Interface model proof

- `InterfaceDefinition` is relational and contains 1..N `InterfaceRoleDefinition` records.
- Roles carry exact structural capability names and cardinality-ready minimum/maximum.
- `MateIr` contains instance-scoped participant paths and `SemanticValue` IDs.
- `ShaftBore` lowers engineering intent to one `AxisCoincident` placement consequence and a symbolic diameter fit.
- `ShoulderBearingSeat` lowers to `AxisCoincident` plus `PlaneCoincident`.
- Source tree and Mate graph are separate syntax and separate IR collections.
- Diagnostics cover missing/duplicate Roles, scope, capability mismatch, fit incompatibility, under/overconstraint, missing/ambiguous paths, unit mismatch, and assertion failure.
