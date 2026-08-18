# Forge Host M2 package and API audit

## Prior state

| Surface | Prior classification | Finding |
|---|---|---|
| `Aetheris.Forge` | kernel/extension contracts | Below Firmament; owns descriptors, concept packs, extension registry/executor contracts, and standard capabilities. Moving compiler hosting here would create a dependency cycle. |
| `Aetheris.Forge.Sdk` | mixed ordinary host and advanced extension use | Owned `ForgeHost`, module/template/invocation/result, typed values, extension registration, resource/analysis paths, and capability dispatch. The name did not identify an audience. |
| generated SecretGeometry bindings | sample/generated proof | Strongly typed but referenced the ambiguous SDK. |
| SecretGeometry extension/manifests | advanced sample | A real private capability and manifest proof, not ordinary configurator API. |
| CLI integration | application host | Compiles and inspects real Firmament/AP242, but is file/source oriented rather than the typed application binding seam. |

## Decision

- `Aetheris.Forge.Host` owns the existing supported compiler-facing host path: module inspection, direct binder/IR Template arguments, diagnostics, compilation results, specialization identity, provenance, resources, and artifacts.
- `Aetheris.Forge.KernelSDK` is the explicit advanced extension-author dependency and references Host plus the low-level Forge contracts.
- `Aetheris.Forge` remains below Firmament because its contracts are compiler dependencies. It is not advertised as the ordinary application entry point.
- `Aetheris.Forge.Sdk` is removed. No compatibility forwarder is justified before a stable package release.
- SecretGeometry's extension implementation now references KernelSDK; its generated consumer binding references Host.

The database sample references Host only. KernelSDK is absent from both direct and transitive restore graphs.
