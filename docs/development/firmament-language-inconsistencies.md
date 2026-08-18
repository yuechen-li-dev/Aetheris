# Firmament language reconciliation register

This A5c register classifies the language seams first exposed by A5b. The public dialect is defined by [`docs/public/firmament`](../public/firmament/overview.md), especially the [language-style contract](../public/firmament/language-style.md). Parser/lowering ownership is recorded in the [active grammar inventory](firmament-v2-grammar-inventory.md).

| Area | Final classification | Decision and evidence |
|---|---|---|
| Declaration and construct casing | ResolvedCanonical | Firmament-owned vocabulary is canonically PascalCase. Audited lowercase aliases remain only where unambiguous and select the same semantic route; no style warnings exist. |
| Legacy V1 document shape | LegacyQuarantined | `fixtures/LegacyV1/` remains untouched and is compatibility evidence, not V2 authoring authority. |
| Legacy primitive/Boolean vocabulary | LegacyQuarantined | V1 operations remain on their versioned compiler path. Public V2 uses named declarations and semantic features. |
| Direct versus `solid` primitive declarations | RetainedCompatibility | Direct `Box`/`Cylinder`/`Cone`/`Sphere`/`Torus` declarations are canonical. `solid name: Type` remains a compatibility adapter to the same primitive AST/lowering. |
| Through-hole and extent vocabulary | IntentionalDomainDifference | Model Hole `End`, Slot `Extent`, legacy Cut exit selectors, and recognition/speculative booleans have different owning types and were not merged. |
| Model versus Sheet Metal holes | IntentionalDomainDifference | Model uses `Hole<Variant>` and explicit termination. Sheet Metal uses planar-region `Hole Name` and rejects Model syntax. |
| Model targets versus Sheet Metal regions | IntentionalDomainDifference | Native axes/semantic selectors and named planar manufacturing regions remain separate target value types. |
| Native versus imported FEA faces | IntentionalDomainDifference | Native `Beam.face(-X)` and imported AP242 `body.face(#170)` retain distinct identity sources. |
| Assembly document families | RetainedCompatibility | V2 `Assembly` source is current. JSON-shaped `.firmasm` is explicitly deprecated compatibility; it is not merged into Model. |
| PMI declaration/value shapes | RetainedCompatibility | Canonical Model PMI is PascalCase. Bounded legacy spellings remain on the same semantic PMI/AP242 route; Sheet Metal manufacturing PMI stays domain-specific. |
| Edge finishing | ResolvedCanonical | Current V2 uses qualified `EdgeFinish`. V1 low-level operations are quarantined; future `.firmfixture` forms remain speculative. |
| Template / Record / `with` / Struct / Compose / Modify | ResolvedCanonical | Ownership is explicit in the grammar inventory: compile-time data/specialization, constructive body creation, and post-construction operations are distinct. |
| Boss / Pocket versus Add / Remove | RetainedCompatibility | Boss/Pocket preserve their first-class engineering contracts. Low-level Add/Remove remain bounded profile-composition compatibility, not coequal public feature design. |
| Units and literals | ResolvedCanonical | V2 uses `Units: mm` and unit-bearing engineering literals. Lowercase V2 input, external JSON, and unitless V1 arrays are separately classified compatibility forms. |
| Speculative keyword families | SpeculativeQuarantined | Future/not-implemented `.firmfixture` bodies remain untouched and do not define parser or documentation requirements. |
| External identifiers and snake_case | IntentionalDomainDifference | Standards, material designations, part numbers, namespaces, and imported identities preserve source spelling. No global identifier-style enforcement exists. |

No rows remain `UnknownNeedsAudit` or `PotentialParserInconsistency`. Future language design proposals must be added as `DeferredDesignQuestion` rather than inferred from historical or speculative corpus text.
