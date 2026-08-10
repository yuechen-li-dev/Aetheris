# Forge extension SDK

> Semantic Value M1: `ForgeCapabilityOutput.SemanticRoot` may expose a
> compiler-owned typed semantic tree. Claims are validated against exact
> profile/BRep/analysis bindings, BRep associations, deterministic identities,
> and extension/capability/version provenance. Arbitrary extension objects are
> not accepted. ForgeHost exposes `SemanticValueDescriptor`, not kernel
> internals or a permanent interchange schema.

Extension packages implement `IForgeExtension` and register immutable `IForgeCapability` instances explicitly with `ForgeExtensionRegistry`.

```csharp
public sealed class SecretGeometryExtension : IForgeExtension
{
    public string Id => "MyCompany.SecretGeometry";
    public Version Version => new(1, 0, 0);

    public void Register(ForgeExtensionRegistry registry) =>
        registry.RegisterCapability(new SecretCouponCapability());
}
```

There is no default assembly scan, reflection dispatch, global mutable registry, or `InvokeMethod("Assembly.Type.Method")` API.

## Capability descriptor

`ForgeCapabilityDescriptorV1` publishes:

- stable capability ID and version;
- extension/package ID and version;
- typed input schema and descriptions;
- output classification and supported lowering targets;
- deterministic/experimental classification;
- exactness, admission, and provenance contracts.

`ForgeHost.Capabilities` and `ForgeExtensionRegistry.InspectCapabilities()` return descriptors in ordinal ID order. Tooling and LLMs can discover what exists, understand parameters and output guarantees, and interpret typed admission failures without reading implementation source.

## Output tiers

M1 adopts capability levels through output classification and lowering targets rather than separate registration systems:

- `SemanticOnly`: compiler semantics with no geometry claim.
- `ConstructionIr`: preferred custom intent lowered to `ContinuumConstructionDescriptor` and a standard Forge materializer.
- `ExactBrep`: bounded low-level escape hatch using public Kernel BRep types and mandatory validation.
- `ContinuumRegion`: optional CIR construction lineage.
- `SurfaceMeshDerived`: explicitly non-exact and forbidden from masquerading as ExactBRep.
- `Analysis`: reserved seam for future analysis compilers.

The sample uses `ConstructionIr` and supports `ConstructionIr`, `Brep`, and `Cir` targets. It emits no topology lists and calls no Kernel internals.

## Validation

`ForgeCapabilityExecutor` validates parameter schema, requested targets, declared output classification, ConstructionIR invariants, exact BRep bindings, provenance completeness, and exception boundaries. `ForgeHost` then materializes via the standard executor, validates the BRep again at the acceptance boundary, exports AP242, and reimports the result. Requested CIR output receives an explicit association and consistency check.

An extension cannot state “trust me, this BRep is valid.” A SurfaceMesh-derived result cannot claim exactness. Unexpected exceptions become `forge-capability-exception` with capability and source identity.

## Manifest and resolution

```csharp
var manifest = new ForgeExtensionManifest([
    new ForgeExtensionRequirement("MyCompany.SecretGeometry", new Version(1, 0, 0)),
]);

var host = new ForgeHost([new SecretGeometryExtension()], manifest);
```

The manifest is the reproducible extension environment for compilation. M1 requires exact extension versions and diagnoses missing or conflicting registrations; it does not negotiate version ranges. Capability references in Firmament use stable IDs in `Construct` declarations. Ambient DLL load order never changes semantics.

## Packaging and lifetime

The normal distribution unit is a .NET package/assembly containing registration code and capabilities, with optional Firmament modules, generated bindings, and tests. Capabilities should be stateless. Per-invocation information arrives through `ForgeCapabilityInvocationContext`; output and provenance are immutable values. Extensions are trusted local compiler code and are not sandboxed in M1.

`Aetheris.Forge.Testing` provides focused helpers for successful fixture compilation, BRep validation, AP242 round trips, deterministic hash comparison, provenance completeness, and CIR association. It intentionally does not expose or duplicate internal test infrastructure.

## Sample extension

`Aetheris.Forge.SampleExtension` represents private `MyCompany.SecretGeometry`. `SecretCouponCapability` applies private sizing semantics and emits a generic exact prismatic construction descriptor. The Firmament Template remains domain code, the generated C# binding uses Forge, and the resulting body is an ordinary validated Aetheris artifact. The extension implementation contains zero capability-specific code in `Aetheris.Kernel.Core`.
