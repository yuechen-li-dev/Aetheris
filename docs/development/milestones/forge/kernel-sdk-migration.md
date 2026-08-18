# Forge SDK package migration

## Preview 2 safety model

Host users do not need KernelSDK. Application configurators should reference
`Aetheris.Forge.Host`; KernelSDK is for advanced C# extension development.

`IForgeExtension.Safety` defaults to `ForgeExtensionSafety.Safe`. A Safe
extension receives typed inputs, semantic/construction builders, requested
lowering targets, deterministic math, and explicit resources supplied by the
host. Aetheris does not provide process, network, environment-secret, arbitrary
filesystem, or host-internal services.

This capability surface is enforced at the Aetheris API boundary, including
explicit registration, argument validation, output validation, and typed
diagnostics. It is not a CLR security sandbox: in-process C# can call the .NET
base class library directly. Safe is a reviewed capability contract, not
isolation from malicious code.

An extension needing arbitrary C# authority must declare:

```csharp
public ForgeExtensionSafety Safety => ForgeExtensionSafety.UNSAFE;
```

The host rejects it with `forge-extension-unsafe-consent-required` unless the
application deliberately opts in:

```csharp
new ForgeHost(extensions, options: new ForgeHostOptions(AllowUnsafeExtensions: true));
```

The canonical SecretGeometry extension explicitly declares Safe and uses typed
ConstructionIR/SemanticValue output only.

The former `Aetheris.Forge.Sdk` name mixed two audiences and has been removed before a stable NuGet release made compatibility forwarding necessary.

Use:

- `Aetheris.Forge.Host` for application-side loading, typed Template invocation, compilation, diagnostics, specialization identity, provenance, and generated artifacts.
- `Aetheris.Forge.KernelSDK` for advanced construction/semantic/compiler capability development and Aetheris kernel prototyping.
- `Aetheris.Forge` for the low-level contracts that sit below Firmament; application code should normally reach them through one of the packages above.

Migration:

1. Ordinary host applications replace the project/package reference with `Aetheris.Forge.Host` and change `using Aetheris.Forge.Sdk;` to `using Aetheris.Forge.Host;`.
2. Extension implementations replace the reference with `Aetheris.Forge.KernelSDK`. Existing extension contract namespaces remain under `Aetheris.Forge.*` so capability code does not need a cosmetic namespace rewrite.
3. Generated binding packages reference `Aetheris.Forge.Host`; they should not pull KernelSDK into their consumers.

No compatibility package is retained. The old project had not reached a stable package contract, and a forwarding package would preserve the ambiguous dependency boundary the rename is intended to remove.
