# FORGE-A0 — Aetheris.Forge and Standard Library semantic-extension architecture audit

Date: 2026-06-21
Status: **Outcome A — semantic-extension architecture recommendation**
Scope: audit/discovery only; no Forge implementation, parser/lowering/kernel behavior, Standard Library code, dynamic NuGet loading, or product behavior changes.

## 1. Purpose

This milestone audits the current `Aetheris.Forge` and Standard Library remnants/designs and recommends how Forge should be redesigned around Firmament-level semantic CAD extension packages rather than raw geometry plug-ins.

The motivating architecture boundary is:

```text
Firmament source:
  semantic authoring surface

AIR:
  normalized semantic/topology-generating IR

BRep/materializer:
  explicit executable geometry/topology

STEP:
  interchange/export artifact

DisplayIR:
  view/materialization artifact
```

The key doctrine is that authoring should preserve CAD intent as long as possible. Profile stacks, BRep primitives, safe booleans, STEP entities, and display meshes are downstream implementation details. Forge should therefore become a NuGet-distributed **semantic CAD extension SDK** for concepts, templates, validators, feature schemas, lowering contracts, examples, fixtures, and LLM-facing guidance.

## 2. Search summary

Commands run:

```bash
find . -maxdepth 3 -type d | sort | rg -i "forge|standard|stdlib|library|template|concept"
find . -maxdepth 4 -type f | sort | rg -i "forge|standard|stdlib|library|template|concept"
git grep -n -i "Aetheris.Forge\|Forge\|StandardLibrary\|Standard Library\|StdLib\|template<\|concept " -- .
git grep -n -i "IFeatureProvider\|FeatureProvider\|TemplateProvider\|ConceptProvider\|ExtensionProvider\|Plugin\|NuGet\|Package" -- .
git grep -n -i "Fastener\|ISO\|ASME\|Clearance\|Counterbore\|Countersink\|Tapped\|Thread" -- .
sed -n '1,220p' Aetheris.Forge/ForgeAtomics.cs
sed -n '1,220p' Aetheris.Forge/ForgeRoundedRectangleProfile.cs
sed -n '1,220p' Aetheris.Kernel.StandardLibrary/StandardLibraryPrimitives.cs
sed -n '1,220p' Aetheris.Kernel.StandardLibrary/StandardLibraryReusableParts.cs
sed -n '1,170p' docs/development/milestones/general/surface-feature-a0-architecture-audit.md
sed -n '1,140p' docs/development/audits/hole-a0-stacked-hole-air-recovery-audit.md
sed -n '1,120p' Aetheris.Kernel.Firmament/Execution/SurfaceFeatureDescriptors.cs
sed -n '1,80p' Aetheris.Kernel.Firmament/README.md
```

Key findings:

- Project directories exist for `Aetheris.Forge` and `Aetheris.Kernel.StandardLibrary`.
- `Aetheris.Forge` currently contains bounded rounded-rectangle profile atomics and centered extrusion helpers.
- `Aetheris.Kernel.StandardLibrary` currently wraps Forge atomics into BRep primitives and provides a reusable cube-with-cylindrical-hole part.
- Firmament has a connector for `standard_library/*` reusable part references.
- Existing docs already refer to Forge/deferred routes for threads/helical features and surface-feature families.
- Firmament V2 metadata-only `template<Process>` / `concept` docs and fixtures exist, but they are not parser-backed product behavior.
- No `IFeatureProvider`, `TemplateProvider`, `ConceptProvider`, `ExtensionProvider`, dynamic NuGet discovery, or general plug-in API was found.

Build/test constraint: this audit intentionally changed documentation only. Per milestone validation, `git diff --check` and `git status --short` were run. The optional .NET build was not required because no code changed.

## 3. Found artifacts

| Artifact | Classification | Notes |
| --- | --- | --- |
| `Aetheris.Forge/Aetheris.Forge.csproj` | refactor candidate / geometry-plugin assumption | Project exists in the solution, but current contents are raw geometry atomics rather than semantic extension descriptors. |
| `Aetheris.Forge/ForgeAtomics.cs` | refactor candidate / geometry-plugin assumption | Exposes `RoundedRectangle(...)` and `ExtrudeCentered(...)` helpers that return profile/BRep results. This is useful bounded geometry utility code, but it is not a semantic Forge SDK. |
| `Aetheris.Forge/ForgeRoundedRectangleProfile.cs` | refactor candidate | Encapsulates rounded-rectangle validation and profile tessellation. It has deterministic diagnostics and could remain as an internal geometry helper, but not as the public shape of Forge. |
| `Aetheris.Kernel.StandardLibrary/Aetheris.Kernel.StandardLibrary.csproj` | refactor candidate | Current Standard Library depends on `Aetheris.Forge`, which reverses the desired future relationship if Standard Library is to become the blessed semantic base pack. |
| `Aetheris.Kernel.StandardLibrary/StandardLibraryPrimitives.cs` | refactor candidate / geometry-plugin assumption | Provides `CreateRoundedCornerBox` and `CreateSlotCut` by calling Forge rounded-rectangle atomics and extruding directly to BRep. This is BRep helper library behavior, not Firmament semantic concept/template behavior. |
| `Aetheris.Kernel.StandardLibrary/StandardLibraryReusableParts.cs` | refactor candidate / geometry-plugin assumption | Provides `cube_with_cylindrical_hole` by direct box/cylinder safe subtract. Useful as fixture/demo evidence, but it is a pre-semantic reusable geometry part path. |
| `Aetheris.Kernel.Firmament/Connectors/FirmamentPartLibraryConnector.cs` | production-ready candidate for narrow connector seam / refactor candidate for future package resolution | Resolves `standard_library/*` references to current reusable parts. It shows an integration seam, but not NuGet package discovery or semantic concept lookup. |
| `Aetheris.Kernel.Firmament/README.md` | documentation-only | Notes the `standard_library/*` connector and states parsing/validation/lowering remains lane-owned. Useful boundary statement. |
| `Aetheris.Kernel.Firmament/Execution/FirmamentPrimitiveExecutor.cs` and `FirmamentPrismFamilyTools.cs` Forge/StandardLibrary references | refactor candidate | Production execution uses Standard Library / Forge geometry utilities for rounded-corner boxes and slot cuts. These should not be expanded into a general plug-in execution model. |
| `Aetheris.Kernel.Core.Tests/Brep/Features/ForgeRoundedRectangleTests.cs` | test-only artifact | Tests the current bounded rounded-rectangle atomics and Standard Library primitive helpers. Keep as regression coverage for existing behavior, not as Forge architecture tests. |
| `Aetheris.Kernel.Firmament.Tests/FirmamentConnectorLibraryPartTests.cs` | test-only artifact | Tests `standard_library/cube_with_cylindrical_hole` connector resolution. Useful for current connector behavior; future package tests should be descriptor-based. |
| `docs/development/milestones/general/surface-feature-a0-architecture-audit.md` | documentation-only / semantic-extension candidate | Already routes thread/helical, knurl, emboss/deboss, and broad surface features toward Forge/deferred rather than first-wave Core. Strong input for capability tiers. |
| `docs/development/audits/hole-a0-stacked-hole-air-recovery-audit.md` | documentation-only / semantic-extension candidate | Establishes semantic hole doctrine and warns against exposing profile-stack/BRep lanes as source truth. Strong input for Forge Fasteners examples. |
| `docs/development/milestones/general/air-firmament-a2-3-dfm-templates-concepts-pmi.md` | documentation-only / semantic-extension candidate | Defines metadata-only `template<Process>` and `concept` doctrine for DFM constraints. This is closer to the desired semantic extension model than current Forge code. |
| `fixtures/Templates/*` | test-only / documentation fixture | Metadata-only fixtures for process templates and concepts. They are useful future descriptor examples, not current parser-backed behavior. |
| Surface-feature descriptor/planning code and tests (`SurfaceFeatureDescriptors`, `SurfaceFeaturePlanningBridge`, dry-run/evidence tests) | refactor candidate / semantic-extension candidate | Uses descriptors, validation, diagnostics, and Forge/deferred statuses for future surface features. It is internal and surface-feature-specific, but the pattern is relevant. |
| FrictionLab hole-family policy/shape labs | dead experiment / test-only artifact | Labs classify threaded/knurled/arbitrary swept features as deferred/Forge. Useful evidence, not production API. |
| `aetheris.client` package/plugin references and `.gitignore` NuGet comments | unrelated | General frontend/package metadata, not Forge CAD extension design. |
| STEP/NIST ASME/ISO text occurrences | unrelated data fixture | Standards text appears in STEP data fixtures and smoke tests, not fastener/standards library implementation. |

## 4. Current Forge/Standard Library assumptions

### Forge today

Current Forge is a small geometry helper project. Its public surface assumes:

- extensions are C# helpers compiled with the host solution;
- the useful output can be a `PolylineProfile2D` or `BrepBody`;
- rounded corners are tessellated by segment count;
- validation is immediate numeric argument validation;
- execution can call BRep extrusion directly.

It does **not** currently model:

- Firmament concepts;
- Firmament templates;
- feature schemas;
- semantic field schemas;
- validators as declarative package metadata;
- deterministic diagnostic IDs per concept;
- lowering contracts such as `lowersTo: AirHoleFeature`;
- capability declarations;
- host feature/version requirements;
- NuGet/package discovery;
- package fixtures/examples/LLM guidance.

### Standard Library today

The current Standard Library is a helper library for executable geometry/reusable parts. It is closest to:

- BRep/materializer helper library;
- geometric primitive wrapper library;
- reusable part fixture/demo library.

It is not yet:

- a Firmament syntax library;
- an AIR feature library;
- a semantic feature vocabulary;
- a concept/template pack;
- a validator package;
- a standards/fastener library.

### Obsolete assumption to reject

The most important obsolete assumption is that Forge means “external code that can generate geometry.” That shape would drift toward arbitrary kernel plug-in chaos, obscure authoring intent, and encourage source-level features to silently collapse into BRep booleans. The better model is that Forge packages declare enforceable semantic capabilities first, and only lower through approved AIR/materializer contracts when authorized.

## 5. What survives

The following should survive conceptually:

- **Bounded helper implementations** such as rounded-rectangle profile validation/tessellation can remain internal geometry utilities where already used.
- **Deterministic validation style** from `ForgeRoundedRectangleProfile.Validate(...)` is valuable, but future Forge diagnostics should use stable package/concept diagnostic IDs.
- **Connector seam idea** from `FirmamentPartLibraryConnector` survives as proof that library references can be resolved through a controlled boundary.
- **Surface-feature descriptor pattern** survives: feature kind, host surface family, path/profile kinds, capability target, validation status, and deterministic diagnostics are aligned with semantic package descriptors.
- **Hole semantic doctrine** survives: `AirHoleFeature` / stack/materialization plans should preserve source intent before profile-stack/BRep execution.
- **Template/concept DFM docs and fixtures** survive as design input for declarative Firmament-level constraints.
- **Tests for current behavior** should remain regression tests for the existing rounded-corner/slot/reusable-part behavior until those paths are deliberately migrated.

## 6. What should be retired

The following should be retired as Forge architecture assumptions:

- Forge as a bag of arbitrary geometry atomics.
- Forge packages as arbitrary C# that runs inside kernel execution by default.
- Standard Library as only `CreateFooBrep(...)` helpers.
- Reusable library parts as the primary integration model for semantic features.
- Silent fallback from semantic feature intent to raw 3D booleans.
- Parser grammar extension as the default extension mechanism.
- NuGet package availability as a trust decision.
- Tessellated profile details as source-level authoring truth.
- Thread/knurl/fastener standards as immediate Core BRep features.

Retiring these assumptions does not require deleting current code in this milestone. Current production behavior should remain unchanged until a separately scoped migration exists.

## 7. Recommended Forge architecture

Recommended package/layer split:

```text
Aetheris.Forge.Abstractions
  Stable descriptors/interfaces for:
    concepts
    templates
    field schemas
    validators and diagnostic metadata
    capability declarations
    lowering contracts
    package identity/version/host requirements
    examples/fixtures/LLM guidance metadata

Aetheris.Forge.KernelSDK
  Authoring and conformance helpers:
    descriptor builders
    schema validation
    package validation CLI/test harnesses
    fixture runners
    standards-table validation helpers
    golden diagnostic helpers
    lowering-contract test harnesses

Aetheris.StandardLibrary
  Blessed core semantic CAD concept/template pack:
    Box
    ProfileExtrude
    Hole
    Slot
    Pocket
    Boss
    Pattern
    EdgeFinish
    Revolve eventually
    Sweep eventually

Third-party Forge packages
  NuGet-distributed semantic extension packs:
    concepts/templates
    validators/derived values
    standards tables where applicable
    optional approved lowerers
    fixtures/examples
    LLM-facing guidance
```

Important dependency direction:

- `Aetheris.Forge.Abstractions` must be small and stable.
- `Aetheris.Forge.KernelSDK` can depend on test/utilities but should not be required by the runtime kernel path.
- `Aetheris.StandardLibrary` should become a semantic pack over the abstractions, not a BRep helper project that depends on arbitrary Forge geometry helpers.
- Runtime hosts should consume descriptors and declared capabilities, not discover and execute arbitrary package code by default.

Recommended descriptor families:

```text
ForgePackageDescriptor
  packageId
  semanticVersion
  vendor
  trustTierRequested
  hostFeatureRequirements[]
  concepts[]
  templates[]
  validators[]
  loweringContracts[]
  examples[]
  fixtures[]
  llmGuidance[]

ForgeConceptDescriptor
  conceptId
  category
  fields[]
  defaults[]
  derivedFields[]
  validationRules[]
  diagnostics[]
  manufacturingAssumptions[]
  loweringTarget
  capabilityRequirements[]

ForgeTemplateDescriptor
  templateId
  parameters[]
  constraints[]
  expandsToConcepts[]
  validationHooks[]
  examples[]
```

## 8. Concepts and templates as Firmament enforcement

Forge concepts and templates should operate at the Firmament semantic level.

### Concept

A concept enforces meaning and validity. It defines what a semantic feature **is**.

A concept should define:

- semantic feature category;
- required fields;
- optional fields;
- field types and units;
- defaults;
- derived values;
- validation rules;
- diagnostic IDs and severities;
- manufacturing/process assumptions;
- lowering target;
- capability requirements;
- examples;
- LLM guidance.

Example shape:

```text
concept ISO.ClearanceHole
  category: Hole
  fields:
    screwSize: ISO.MetricScrewSize required
    fitClass: ClearanceFit optional default Normal
    entryFace: FaceSelector required
    center: FaceLocalPoint2D required
    endCondition: HoleEndCondition required
  validators:
    screw size exists
    entry face is planar
    center is face-local and inside admissible region
    end condition removes material
  lowersTo:
    AirHoleFeature
```

### Template

A template provides convenient construction syntax/patterns. It defines how authors can reuse a source-level pattern that expands into semantic features.

A template should define:

- reusable source-level construction pattern;
- parameters;
- constraints;
- expansion into semantic features;
- required concepts;
- examples;
- validation hooks.

Example shape:

```text
template FourBoltPattern<TConcept : Standard.Hole>
  parameters:
    entryFace
    boltCircleOrRectangle
    center
    spacing
    endCondition
  expandsTo:
    four TConcept features with shared pattern identity
  validates:
    generated centers are face-local and admissible
    selected concept supports requested end condition
```

The distinction must remain explicit:

```text
concept:
  enforces meaning and validity

template:
  provides convenient construction syntax/patterns
```

Templates must not become hidden geometry macros. They should expand to semantic feature declarations that can still be validated, diagnosed, traced, lowered, and explained.

## 9. Capability and trust tiers

Recommended capability tiers:

| Tier | Name | Allowed contents | Default trust posture |
| --- | --- | --- | --- |
| Tier 1 | Semantic/docs only | Concept/template descriptors, schema metadata, examples, LLM guidance, fixtures with expected diagnostics | Safe by default if descriptor validation passes |
| Tier 2 | Validation/derivation | Validators, derived fields, standards tables, deterministic diagnostics | Allowed through restricted deterministic APIs/sandboxable execution model |
| Tier 3 | Lowering provider | Lower approved semantic concepts into approved AIR feature families, for example `AirHoleFeature` | Requires explicit host capability match and conformance tests |
| Tier 4 | Materializer provider | Emit restricted BRep/materializer operations through explicit safe APIs | Privileged; narrow allow-list; strong fixture/artifact obligations |
| Tier 5 | Unsafe/native/experimental | Native code, arbitrary kernel access, broad geometry algorithms, experimental APIs | Explicit user/org trust required; never implied by NuGet install |

Most Forge packages should live in Tier 1–3. Standards/fastener packages usually need semantic schemas, tables, validation, diagnostics, and perhaps lowering to canonical AIR features; they should not need raw BRep access. Tier 4 should be rare and reserved for host-approved bounded materializer families. Tier 5 should be opt-in experimental infrastructure, not normal package behavior.

NuGet distribution is packaging, not trust. A NuGet package can carry descriptors and assets, but it does not automatically earn permission to execute validators, lowerers, or materializers.

## 10. Safety rules

Forge must not become arbitrary kernel plug-in chaos. Recommended rules:

1. Parser grammar should remain stable early.
2. Forge packages should not freely extend grammar with arbitrary syntax.
3. Extension syntax should go through typed feature declarations, templates, and concepts.
4. Concepts/templates must be representable as deterministic descriptors.
5. Raw BRep/materializer access must be privileged, capability-gated, and restricted to safe APIs.
6. Packages must declare requested capability tier, host feature requirements, semantic version, and supported lowering targets.
7. Packages must ship tests, fixtures, examples, and expected diagnostics.
8. Packages should ship LLM-facing guidance so generated Firmament source uses the concept correctly.
9. Diagnostics must be deterministic and stable enough for tests.
10. Lowering failures must be explicit and preserve source intent in diagnostics/traces.
11. No package should silently fall back to raw 3D booleans as authoring truth.
12. Lowerers should target canonical AIR feature families before any materializer-specific route.
13. Standards tables must be versioned and sourceable; derived values must identify the table/rule used.
14. Host applications must be able to enumerate package capabilities without executing unsafe code.
15. Package fixtures should include valid and invalid cases, not only happy paths.

## 11. Relationship to semantic holes

Semantic holes are the main proof case for the new Forge direction.

The desired path is:

```text
Firmament semantic hole source
  -> AirHoleFeature
  -> semantic stack/materialization plan
  -> profile-stack/BRep execution
```

The source concept is one semantic manufacturing feature even if the lowered implementation emits multiple coaxial profile-stack components or BRep faces.

Recommended future split:

```text
Aetheris.StandardLibrary:
  Standard.Hole
  Standard.ShaftHole
  Standard.CounterboreHole
  Standard.CountersinkHole
  Standard.HolePattern

Forge.Fasteners.ISO:
  ISO.ClearanceHole
  ISO.CounterboreForSocketHead
  ISO.CountersinkFlatHead
  ISO.TappedHole metadata

Lowering:
  all approved variants emit canonical AirHoleFeature / stack components
```

For example, `Forge.Fasteners.ISO` should validate that an ISO screw size exists, derive clearance/counterbore/countersink dimensions from a declared standards table, require a planar entry face where the concept demands one, verify face-local center admissibility, and lower to `AirHoleFeature` with explicit stack components. It should not materialize an arbitrary cylinder/counterbore boolean as the authoring truth.

Threaded/tapped holes are especially important: early packages can preserve tap/thread metadata semantically and lower the drill/tap hole envelope to `AirHoleFeature` while leaving full thread geometry or helix topology deferred or explicitly Tier 4/5. This matches the current doctrine that thread/helical surface features route to Forge/deferred rather than first-wave Core materialization.

This audit does **not** propose implementing standards tables, fastener catalogs, or thread geometry now.

## 12. Recommended next milestone

Recommended smallest implementation milestone:

```text
FORGE-X1 — Forge concept/template descriptor scaffold
```

Suggested scope:

- Add abstractions only.
- No dynamic NuGet loading.
- No arbitrary plug-in execution.
- Define descriptors for:
  - package identity/version;
  - concept;
  - template;
  - field schema;
  - validator metadata;
  - diagnostic metadata;
  - capability metadata;
  - lowering contract metadata;
  - examples/fixtures/LLM guidance links.
- Add descriptor validation tests.
- Add one built-in example descriptor for `Standard.Hole` or `Standard.CNC`.
- Keep parser integration out of scope unless it is trivial metadata-only enumeration.
- Do not migrate current rounded-rectangle/slot/cube-with-hole product behavior.

If the existing projects are reused, an acceptable title is:

```text
FORGE-X1 — refactor existing Forge/StandardLibrary descriptors into semantic concept/template scaffolds
```

However, reuse should be limited to project structure and tests. Existing BRep helper APIs should not define the public Forge architecture.

## 13. Non-goals

This milestone explicitly does not include:

- implementation of the new Forge architecture;
- dynamic NuGet package loading;
- arbitrary plug-in execution;
- parser grammar extension;
- standards/fit library;
- fastener tables;
- thread geometry;
- BRep/materializer extension API;
- product behavior changes;
- Standard Library rewrite;
- parser/lowering/kernel behavior changes;
- migration of old Forge code into a public semantic SDK;
- weakening or deleting existing tests.

See also: [FORGE-X1 concept/template descriptor scaffold](../implementation/forge-x1-concept-template-descriptor-scaffold.md).
