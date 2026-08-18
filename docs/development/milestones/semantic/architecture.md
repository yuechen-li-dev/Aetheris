# Compiler semantic-value architecture

`Aetheris.Semantics` owns the bounded compiler model shared by Firmament,
Recognize, Forge, Selection, and FEA. It depends on Kernel.Core for exact BRep
binding types; Kernel.Core does not depend on it. This keeps semantic policy out
of the geometry kernel and avoids a Firmament/Forge dependency cycle.

`SemanticValue` is one meaningful typed entity. It has a deterministic stable
identity, `SemanticType`, structural capability objects, exact bindings, exposed
`SemanticValue` members, authored/generated spans, and an ordered acyclic
provenance chain. `SemanticReference` is resolution context: the value plus
structured path segments and the consumer span. Transient CLR object identity is
never semantic identity.

Capabilities are consumer contracts, not origin tags. `ProfileCapability`,
`BoundaryRegionCapability`, `SelectableCapability`, `ComposeOperandCapability`,
`ModifyTargetCapability`, `BodyCapability`, `ExactGeometryCapability`, and the
analysis/material region contracts may coexist. The validator proves claims:

- Profile and Compose require a producer-owned `ExactProfileBinding` whose
  validator accepts the exact profile.
- Boundary regions require an exact BRep face/region or exact analytic analysis
  region binding.
- Body requires `ExactBrepBodyBinding`.
- Selectable, Modify, and exact-geometry claims require bounded exact evidence.
- Forge additionally verifies that bound bodies/faces belong to its validated
  `ExactBrep` output and that capability/version provenance exists.

Bindings are a closed compiler vocabulary, not a service locator:
`ResolvedProfile2D`, exact BRep body/face/region, source-grounded topology
selection, exact analysis region, construction identity, and imported STEP
entities. A binding can carry a kernel representation; CLI/API inspection emits
only `SemanticValueDescriptor`, not kernel internals. That descriptor is debug
and tooling output, explicitly not a stable interchange schema.

Concept Path, native Concept IR/Struct, template expansion, Recognize, and Forge
are producers. Profile, Compose, Selection, admitted Modify operations, and FEA
are consumers. Consumer admission calls a capability contract and then retrieves
its exact binding. Origin remains provenance and is not a lowering dispatch key.

No dynamic reflection, arbitrary object walking, string `Resolve("A.B")`, raw
BRep topology navigation, or mesh/lattice identifiers are introduced. Member
resolution accepts parsed `SemanticPathSegment` objects only. Raw topology is
reachable only when a producer deliberately exposes a semantic member and binds
it exactly.

AnalysisIR is downstream of this model. `AnalysisSemanticRegionNormalizer`
erases a boundary-capable value to compact `SemanticRegionBinding` evidence;
solver and mesh code never depend on `SemanticValue`.
