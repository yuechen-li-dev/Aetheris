# PREVIEW3-HARDEN-A5c — Firmament V2 language reconciliation

## Executive verdict

Yes. Firmament V2 now has one documented canonical visual dialect: Firmament-owned declarations, features, fields, and owned values use PascalCase; fields use `Name: Value`; ordinary primitives use direct named declarations; and dimensions carry explicit units. The parser remains deliberately more permissive than the public style. Bounded historical lowercase and primitive aliases continue to select the same semantics without style warnings.

This was a reconciliation pass, not Firmament V3. No CAD, Sheet Metal, FEA, PMI, Assembly, or Forge engineering capability was added.

## Grammar inventory

The active parser, AST, and lowering ownership audit is recorded in [the Firmament V2 grammar inventory](../development/firmament-v2-grammar-inventory.md). It covers Model and Units; native and analytic primitives; profiles and concepts; static data, Records, Templates, derivation, and Pattern; Struct/Compose construction; Modify and semantic features; PMI; Sheet Metal; Analysis; inline STEP; Assembly; and Drawing.

The [language inconsistency register](../development/firmament-language-inconsistencies.md) is fully reclassified. No `UnknownNeedsAudit` or `PotentialParserInconsistency` row remains.

## Canonical casing

- Firmament-owned vocabulary is canonically PascalCase.
- User-defined identifiers are case-preserving and stylistically unrestricted.
- External standards, material designations, STEP identities, part numbers, and namespaces preserve source spelling.
- `Units: mm`, `Field: Value`, braces for declarations, and brackets for lists are the canonical presentation. Semicolons remain optional where the owning grammar is unambiguous.
- Casing does not carry semantic meaning unless a domain explicitly documents otherwise.
- Compatibility aliases produce no style diagnostics.

The concise public contract is [Firmament V2 language style](../public/firmament/language-style.md).

## Primitive declaration result

`Sphere Body`, `Cone Body`, and `Torus Body` now participate in the same canonical direct named primitive family as `Box Body` and `Cylinder Body`. The canonical parser creates the existing primitive records and uses the existing lowering/runtime routes; pointed Cone remains the same Cone semantics with `TopRadius: 0mm`.

The older form `solid body: Sphere` remains a bounded compatibility adapter. Equivalence tests compare parsed primitive values and lowering results, not merely parse success, for Sphere, Cone, and Torus. The two spellings are not coequal documentation dialects: public source and current fixtures use direct declarations.

## Compose / Modify / Template semantic matrix

| Family | Verified role | Geometry effect |
| --- | --- | --- |
| `Record`, `Static`, `Table`, `with` | immutable finite engineering data and derivation | none; erased after binding |
| `Template` | typed compile-time specialization | none directly; emits admitted ordinary source |
| `Pattern` / `Over` | finite semantic repetition over static data | repeats an admitted feature |
| `Struct` | named construction intent or concept materialization | contains the selected construction route |
| `Compose` / `Base` | constructive creation and initial material interval | creates the prismatic host |
| `Boss` / `Pocket` | first-class connected addition and finite non-through removal | preserves the A3b engineering contracts |
| `Add` / `Remove` | bounded lower-level section-stack compatibility | adds or removes finite profile intervals |
| `Modify` | post-construction semantic operations on an admitted body | applies qualified holes, finishes, or other supported mutations |
| `EdgeFinish` | current qualified semantic finishing | admitted chamfer/fillet lowering only |
| `Pmi` | engineering requirements over the semantic model | no geometry; emits semantic AP242 |

Direct analytic primitive declarations are not profile-based Compose bodies and cannot be reopened as though they were one. The geometry guide now makes this placement boundary explicit instead of inviting a guess between equivalent-looking spellings.

## Through/extent audit

No unlike semantic types were merged. Native Model Hole termination is canonically `End: ThroughAll`. Slot `Extent: ThroughAll` is a slot interval. The old `through: face(-X)` form is an exit-face selector in its legacy Region/Cut owner. Boolean `through` occurrences belong to recognition evidence or legacy/speculative inputs according to their owner. Sheet Metal openings pass through their planar sheet region by Sheet Metal semantics.

These distinctions are now classified and documented rather than normalized by token resemblance.

## Intentional domain distinctions

- Model uses typed `Hole<Variant>` declarations and Model semantic targets; Sheet Metal uses specialized planar-region `Hole Name` declarations.
- Native Analysis targets such as `Beam.face(-X)` and imported AP242 identities such as `Body.face(#170)` retain different identity sources.
- Assembly uses typed roles, ports, interfaces, and DatumFrame references. V2 `Assembly` is current; JSON-shaped `.firmasm` remains deprecated compatibility and was not folded into Model.
- Canonical Model PMI remains distinct from Sheet Metal manufacturing semantics while both preserve their qualified runtime routes.
- `InlineStep` declarations and the bounded FEA `inlineSTEP(...)` expression remain domain-specific forms.
- V1 fixtures and future/not-implemented `.firmfixture` bodies remain quarantined evidence, not public V2 syntax.

## Compatibility policy

The parser retains harmless, already-supported lowercase spellings for audited Units, primitive fields, and FEA constructs/fields. Mixed input such as `Model` plus `units` and `Box` plus `size` parses through the canonical document route with zero warnings. Legacy `solid name: Primitive`, V1 input, JSON `.firmasm`, and the bounded inline-STEP expression remain explicit compatibility surfaces.

There is no global case-insensitive lexer or identifier rewrite. Aliases are local to their owning binders, so user and external identifiers keep their spelling and casing. No casing-only warning was introduced.

## Canonical corpus migration

The consolidated `fixtures/Canonical/` corpus was audited and required no dialect repair. Active analytic Sphere, pointed Cone, and Torus fixtures were migrated from the lowercase `solid` adapter to direct PascalCase declarations. The canonical FEA cantilever, inline-step analysis, and public A36 dogfood source now use ordinary Model geometry and PascalCase analysis vocabulary.

Public geometry, FEA, Sheet Metal, materials, syntax, feature-reference, and index documentation now use and explain the same dialect. The FEA and Sheet Metal guides contain complete authorable examples. VS Code grammar coverage recognizes the reconciled constructs, and snippets emit canonical Sphere, Cone, Torus, and Linear Analysis forms only. LegacyV1 and speculative `.firmfixture` inputs were not rewritten.

An executable qualification test scans current Firmament fixtures, public `firmament` code fences, and VS Code snippets for known noncanonical owned vocabulary while allowing external identifiers and user-selected names.

## Parser equivalence evidence

- Direct and legacy Sphere, Cone, and Torus sources produce equal primitive records and lowering results.
- Canonical and lowercase FEA keyword/field forms produce equivalent compiled analysis contracts.
- Canonical ordinary `Box Beam` geometry reaches the native FEA region producer; the former FEA-only geometry-regex seam is removed while its old input remains a fallback.
- Mixed supported casing selects the canonical parser route without warnings.
- The real CLI validates the canonical analytic fixtures and solves the canonical cantilever as `FirmamentNative`; the deterministic FEA evidence hash is `54A2A9E1952D3F47ECE5B371D8A9AFA62F9FBEB675BC6798F3BCA8E76A7F3D27`.

## Fresh-agent results

A fresh agent was restricted to `docs/public/` and `fixtures/Canonical/`, with no historical fixtures. Its first pass found two genuine authoring ambiguities: the public FEA and Sheet Metal pages lacked complete copyable programs, and the relationship between direct Box geometry and profile-based Compose placement for Boss/Pocket was underexplained.

After those documentation repairs, the agent independently authored all three requested cases in one visual dialect:

- an ordinary machined part using a profile Base/Compose for Boss and Pocket, followed by Hole and EdgeFinish in the admitted semantic location;
- a canonical ordinary `Box` plus `Analysis`, `Fixed`, and `Force` cantilever;
- a specialized PascalCase `SheetMetal` bracket with `Flange` and Sheet Metal `Hole`.

The second pass reported no remaining spelling or field-placement guess. The test also confirmed that intentional Model/Compose and Sheet Metal distinctions are teachable without exposing historical dialects.

## Validation

- Release solution build: pass, 0 warnings, 0 errors.
- Full active serial .NET suite (`Category!=SlowCorpus`): 3,006 passed across the test projects, 0 failed. The FrictionLab assembly had no tests matching the active filter.
- Focused reconciliation suites: canonical grammar 11/11, production FEA 8/8, CLI dialect/public qualification 15/15.
- Repository layout guard: pass, 3,579 tracked files inspected.
- VS Code extension: TSPack sync/check, typecheck, 13 tests, build, and VSIX packaging passed.
- Cadmata release gate: TSPack sync/check, typecheck, 81 tests, build, and lint passed. Existing dependency-version and blocked lifecycle-script policy notices remain acknowledged.
- Release ZIP smoke: extracted cleanly; packaged CLI help and canonical validation passed; packaged Sphere and pointed Cone produced exact STEP; packaged canonical cantilever FEA converged with equilibrium evidence; packaged NativeAOT Forge Host reported Protocol v1 and five templates.
- `git diff --check` and public-document qualification: pass.
- SHA-256 manifest: 19/19 staged entries independently recomputed and matched.

## Release artifact impact

All Preview 3 candidate artifacts were regenerated from the A5c tree. The publication directory contains the Windows x64 bundle, Firmament VSIX, CLI package, and all 16 public library packages. The complete byte counts and hashes are in the generated `artifacts/local/a5c/release/RELEASE-INVENTORY.md` and `SHA256SUMS.txt`.

| Principal artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| Windows x64 ZIP | 107,782,724 | `939fe78efef094cb699e041474550911ddfabb463760b431a6a1fba9dd4e1a63` |
| Firmament VSIX | 12,327 | `830cebe18f7dfe97da8da88da862de8f85cbf5085791ed880edc685c5c47d38e` |
| CLI NuGet | 33,412,229 | `dedb7aded39914c3d624f7300c91a3baa88e5023b87d4d630dedc576451b6769` |

As with every uncommitted release-candidate assembly, publication automation must regenerate artifacts after the final commit/tag so embedded revision metadata and hashes correspond to that immutable revision.

## Feature freeze

Feature freeze remains intact. A5c adds only direct parser access to already-existing analytic primitive semantics, routes canonical ordinary geometry into the existing FEA compiler, bounds compatibility aliases, migrates current source/examples, and documents verified ownership. It does not add geometry, analyses, PMI, Sheet Metal operations, Assembly behavior, Forge templates, arbitrary Booleans, or a formatter.

The resulting policy is: one canonical dialect, permissive compatibility, intentional domain differences, and no syntax police.
