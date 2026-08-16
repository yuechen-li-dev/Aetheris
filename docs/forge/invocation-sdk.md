# Forge Host invocation API

Native C# remains the direct, first-class invocation path described here. Foreign-language callers use the deliberately smaller [Forge Host Protocol v1](protocol-v1.md), which resolves the same authoritative Firmament Template definitions without exposing managed object graphs or requiring C# callers to round-trip through JSON.

`Aetheris.Forge.Host` exposes the host workflow `ForgeHost -> ForgeModule -> ForgeTemplate -> ForgeInvocation -> ForgeCompilationResult`.

## Typed Template invocation

```csharp
var host = new ForgeHost();
var module = host.LoadModule("parts.firmament");
var template = module.ResolveTemplate("HostBox");

ForgeCompilationResult result = template.Invoke("FixtureBox")
    .Bind("Width", new ForgeLength(20))
    .Bind("Depth", new ForgeLength(12))
    .Bind("Height", new ForgeLength(5))
    .Compile();
```

The module source is parsed because it is a real source file. The host does not concatenate an application source string: `FirmamentTemplateHostBridge` creates the application and host Record bindings directly in the existing Template binder IR. Parameter names, kinds, types, defaults, Record types, and type-parameter constraints are available through `ForgeTemplateMetadata` before invocation. Missing, unknown, and mismatched bindings stop before compiler or extension lowering.

`ForgeCompilationResult` exposes typed diagnostics, stage timings, AP242 artifact text, deterministic artifact hash, capability evidence, provenance, optional BRep, and optional CIR association evidence.

## Generated binding proof

`Aetheris.Forge.SampleExtension.Bindings/ForgeTemplates.g.cs` demonstrates deterministic generated names and strongly typed C# records:

```csharp
var invocation = ForgeTemplates.SecretCoupon(
    module,
    new SecretCouponSpec(24, 16, 6),
    instanceName: "PrivateCoupon");

var result = invocation
    .WithTargets(ForgeLoweringTarget.Brep, ForgeLoweringTarget.Cir)
    .Compile();
```

The generated method only creates `ForgeValue` bindings and calls the same `ForgeInvocation` path. It contains no compiler or materialization logic. `ForgeGeneratedNames.TemplateMethod` defines the M1 deterministic identifier normalization seam; a production source generator remains future work.

## Record values

Records are supplied structurally:

```csharp
new ForgeRecord("SecretCouponSpec", new Dictionary<string, ForgeValue>
{
    ["Width"] = new ForgeLength(24),
    ["Depth"] = new ForgeLength(16),
    ["Height"] = new ForgeLength(6),
});
```

The binder checks the declared Record type and validates members through the normal Firmament Template binder before feature lowering.

## Imported STEP resources

```csharp
var resource = ImportedStepResource.Load("VendorPart", canonicalStepPath);
invocation.AddResource(resource);
```

`ImportedStepResource.Load` requires Aetheris-canonical AP242, computes SHA-256, and imports through the ordinary `Step242Importer`; it does not establish a second STEP pipeline. A capability parameter declared as `ImportedStepResource` can bind it with `$VendorPart` in a bounded `Construct` input. Native InlineStep Template parameter binding is not generalized in M1.

## Diagnostics

Expected failures are returned as `ForgeDiagnostic`; normal compiler UX never receives a raw extension exception. Important M1 codes include:

- `forge-template-parameter-missing`, `forge-template-parameter-unknown`, `forge-template-parameter-mismatch`
- `forge-extension-missing`, `forge-extension-version-conflict`
- `forge-capability-missing`, `forge-capability-id-collision`, `forge-capability-version-conflict`
- `forge-capability-parameter-missing`, `forge-capability-parameter-unknown`, `forge-capability-parameter-mismatch`
- `forge-capability-admission-rejected`, `forge-capability-output-contract-violation`
- `forge-capability-construction-invalid`, `forge-capability-brep-invalid`
- `forge-capability-lowering-target-unsupported`, `forge-capability-nondeterministic`
- `forge-capability-provenance-missing`, `forge-capability-exception`
- `forge-capability-cir-output-missing`, `forge-capability-cir-brep-inconsistent`

## M1 limitations

M1 materializes one `Construct` result per host invocation. Standard ConstructionIR materialization admits two ordered, corresponding, identical polygonal sections (an exact prism). CIR association is implemented for the sample's axis-aligned box region. Rich assembly composition, generalized loft materialization, nested host Record construction, native InlineStep Template parameters, and production C# source generation are deliberate next steps rather than hidden fallbacks.
