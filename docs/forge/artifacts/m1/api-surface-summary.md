# M1 API surface summary

## Host embedding

- `ForgeHost`: explicit extension environment, module loading, capability inspection.
- `ForgeModule`: source/module identity and Template catalog.
- `ForgeTemplate`: metadata plus invocation factory.
- `ForgeInvocation`: typed bindings, resources, requested targets, compilation.
- `ForgeCompilationResult` / `ForgeCompilationArtifact`: diagnostics, timings, AP242, deterministic hash, BRep/CIR evidence, capabilities, provenance.
- `ForgeValue`: Length, Angle, Integer, Real, Boolean, String, Type, and structural Record values.
- `ImportedStepResource`: canonical AP242 hash plus ordinary importer result.

## Compiler seam

- `FirmamentTemplateHostBridge.Inspect`: stable Template signature metadata.
- `FirmamentTemplateHostBridge.Expand`: direct binder-IR invocation and specialization evidence.
- `FirmamentBuildAndExport.CompileSource`: in-memory entry into the same materialization/validation path as file builds.

## Extension SDK

- `IForgeExtension`, `IForgeCapability`, `ForgeExtensionRegistry`.
- `ForgeCapabilityDescriptorV1`, typed parameters, output classifications, lowering targets, determinism, admission/exactness/provenance contracts.
- `ForgeExtensionManifest`, exact extension requirements.
- `ForgeCapabilityExecutor`: typed admission, output-contract validation, ConstructionIR materialization, exact BRep validation, exception wrapping.
- `Aetheris.Forge.Testing.ForgeExtensionAssertions`: focused author test kit.

## Proof packages

- `Aetheris.Forge.SampleExtension`: external/private capability and Firmament module.
- `Aetheris.Forge.SampleExtension.Bindings`: deterministic generated binding proof using the same host path.
- `Aetheris.Forge.Sdk.Tests`: native Template invocation, full extension stack, deterministic output, collision/version/missing diagnostics, resources, CIR association.
