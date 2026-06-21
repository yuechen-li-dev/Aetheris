# FORGE-X1 concept/template descriptor scaffold

FORGE-X1 adds the first production scaffold for Forge semantic extension descriptors. It follows the FORGE-A0 direction that Forge packages are semantic CAD concept/template packs, not arbitrary geometry plug-ins.

## What was added

The scaffold lives in the existing `Aetheris.Forge` project under the `Aetheris.Forge.Abstractions` namespace. This keeps the milestone lightweight and lets the descriptor model coexist with the current Forge geometry helpers without migrating or deleting them.

Descriptor families now cover:

- `ForgePackageDescriptor`
- `ForgeConceptDescriptor`
- `ForgeTemplateDescriptor`
- `ForgeFieldDescriptor`
- `ForgeDiagnosticDescriptor`
- `ForgeCapabilityDescriptor`
- `ForgeLoweringContractDescriptor`
- `ForgeExampleDescriptor`
- `ForgeFixtureDescriptor`
- `ForgeLlmGuidanceDescriptor`

`ForgeDescriptorValidator` performs deterministic metadata validation and returns stable diagnostic codes/paths/messages. It validates required ids, id shape, semantic versions, duplicate concepts/templates/capabilities/fields/diagnostics, invalid enum values, unknown same-package template concept references, and lowering contracts that reference undeclared capabilities.

## Trust tiers

FORGE-X1 encodes the FORGE-A0 trust model as metadata only:

1. `SemanticDocsOnly`
2. `ValidationDerivation`
3. `LoweringProvider`
4. `MaterializerProvider`
5. `UnsafeNativeExperimental`

The validator rejects undefined tier enum values. Declaring a capability or requested package tier does not load code, execute code, or grant trust.

## Built-in Standard.Hole example descriptor

`Aetheris.Forge.Examples.StandardHoleForgeDescriptor` provides a descriptor validation fixture for package `Aetheris.Standard` and concept `Standard.Hole`. Its fields mirror descriptor-level equivalents of recent semantic-hole work:

- `entryFace`: `FaceSelector`, required
- `center`: `FaceLocalPoint2D`, required
- `shaftDiameter`: `Length`, required
- `endCondition`: `HoleEndCondition`, required

The example includes diagnostic metadata and a lowering contract whose target AIR feature family is `AirHoleFeature`. This is contract metadata only; no lowerer is invoked.

## Deferred work

FORGE-X1 intentionally does not implement:

- dynamic NuGet package loading;
- assembly scanning;
- plug-in execution;
- runtime package trust decisions;
- parser grammar extension or Firmament source integration;
- Standard Library behavior changes;
- fastener, ISO, ASME, thread, or tap standards tables;
- BRep/materializer extension APIs;
- lowering execution;
- STEP, DisplayIR, frontend, or product behavior changes.

## Relationship to FORGE-A0

FORGE-A0 recommended evolving Forge into a semantic CAD extension SDK made of concepts, templates, validators, semantic feature schemas, lowering contracts, examples, fixtures, and LLM guidance. FORGE-X1 implements the first stable descriptor/validation layer for that architecture while preserving the old Forge project behavior.

## Validation commands

Run these checks for this milestone:

```bash
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.slnx -f net10.0 --no-build --filter "Forge|Standard|Hole|FirmamentV2|Air"
git diff --check
git status --short
```

## Forward link: FORGE-X2 built-in Standard concept pack

FORGE-X2 promotes the FORGE-X1 `Standard.Hole` descriptor fixture into a built-in metadata-only Standard concept pack scaffold. See `docs/implementation/forge-x2-standard-concept-pack-scaffold.md` for the package-level descriptor, added concepts, and deferred runtime behavior.
