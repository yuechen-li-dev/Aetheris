# Firmament V2 language audit — Assembly M1

## Method

Evidence priority was parser/regex productions, binder/Concept IR/static expansion, regression tests, canonical fixtures, production consumers, then old docs. `Aetheris.Kernel.Firmament/FirmamentV2/FirmamentV2Parser.cs` is a hand-written bounded parser with cooperating parsers (`ConceptIr`, `CanonicalStaticAuthoring`, `FirmamentV2TemplateExpansion`, `ProfileAuthoringParser`) rather than a generated token stream. Therefore the token inventory records source spellings and parser consumers, not a nonexistent token enum.

Assembly relational syntax is currently owned by `AssemblyM0Parser`. This is now documented explicitly; pretending `FirmamentV2Parser` alone defined every supported construct was the largest documentation contradiction.

## Findings

- `satisfies` and `requires` are not synonyms. `satisfies` is Template type-parameter structural conformance; lowercase `requires` is an Assembly Role capability list; `Require` is a compile-time predicate/constraint.
- `Semantic` is an Assembly semantic member group normalized to SemanticValue, not a general synonym for Concept/Struct.
- Point/Axis/Plane/Dimension assembly syntax creates exact typed bindings in definition-local millimetres. M1 establishes the world-binding seam without cloning definitions.
- `Lower` is a finite Interface-to-placement lowering declaration. `Fit` is interval clearance plus a typed dimensional transition. `Allow` admits named residual rigid freedoms.
- `Relation` is a bounded dimensional graph edge, not general symbolic algebra.
- Parser breadth is substantially larger than uniformly productionized lowering. Route-specific Hole/Slot/EdgeFinish/PMI/lattice forms are Experimental unless a bounded supported route is identified.
- Old `.firmasm` and V1 fixture/expect syntax are Legacy/Deprecated and do not define V2.

No parser branches were deleted in this pass: several apparently historical representations share regex paths with active compatibility fixtures, and removal without a generated grammar/token test would be risky. They are classified rather than blessed.

## Deliverables

- Canonical reference: `docs/firmament-v2/language-reference.md`
- Beginner path: `docs/firmament-v2/quickstart.md`
- Machine-readable status: `docs/firmament-v2/language-features.json`
- Evidence matrices: `docs/firmament-v2/artifacts/language-audit-m1/`

Public website source is not contained in the Aetheris repository. The repository documentation is reconciled here; changing the separate website repository would violate this task's repository-locality instruction. That external publication remains a concrete integration task, not an undocumented omission.

