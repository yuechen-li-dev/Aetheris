# Forge architecture

Forge is Aetheris's sanctioned host-language embedding and compiler-extension boundary.

The architectural contract is:

- **Kernel** is the small, proven, broadly reusable geometry/compiler substrate.
- **Firmament** is the typed compiler metaprogramming DSL for engineering intent, Concepts, Records/Tables, Templates, and deterministic source-controlled model or analysis generation.
- **Forge** is the C# embedding API and explicit extension SDK. It invokes Firmament Templates and lets trusted packages contribute typed compiler capabilities without modifying `Aetheris.Kernel.Core`.

Firmament is not a general-purpose runtime language and is not “C++ for CAD.” Host programming stays in C#. Firmament has no inline C#, inline Python, arbitrary scripting block, reflection escape hatch, or generic method invocation.

## Responsibility

Forge owns host compiler embedding, module and Template resolution, typed parameter binding, compilation requests, artifact access, extension registration and discovery, capability metadata, validation/provenance contracts, imported-resource handles, and the generated-host-binding seam.

Forge does not own mutable raw BRep editing, a duplicate C# CAD API, geometry algorithms that belong in the shared Kernel, Firmament language semantics, solvers, marketplace policy, process sandboxing, or arbitrary host-language execution.

## Project and dependency structure

The dependency direction intentionally separates contracts from compiler hosting:

```text
Aetheris.Kernel.Core
        ^
        |
Aetheris.Forge                    extension contracts and standard ConstructionIR executor
        ^
        |
Aetheris.Kernel.Firmament         typed Template binder and compiler
        ^
        |
Aetheris.Forge.Sdk                C# host embedding, capability dispatch, artifact/provenance access
        ^
        +-- Aetheris.Forge.Testing
        +-- generated binding packages / host applications

Aetheris.Forge.SampleExtension --> Aetheris.Forge only
```

The existing `Aetheris.Forge` assembly remains below Firmament because Firmament already consumes its concept contracts. Putting `ForgeHost` into that assembly would create a dependency cycle. `Aetheris.Forge.Sdk` is the sanctioned compiler-facing package while `Aetheris.Forge` remains the low-level extension-contract package.

The sample extension depends only on `Aetheris.Forge` and public Kernel contracts reached through it. It receives no `InternalsVisibleTo` access and contains no capability-specific Kernel code.

## Compilation model

A host loads a real `.firmament` module, inspects Template metadata, resolves a Template, binds typed values, and invokes it. Host calls enter `FirmamentV2TemplateExpansion` as binder IR. They do not synthesize a Firmament application declaration. Host Record values become immutable compiler-owned static-record bindings and Templates still erase before feature AIR.

A Template may contain the bounded domain declaration:

```firmament
Construct MyCompany.SecretGeometry.SecretCoupon SecretBody {
    Width: Spec.Width
    Depth: Spec.Depth
    Height: Spec.Height
}
```

`Construct` names a registered semantic capability ID. It is not C# syntax and cannot identify an assembly, type, or method. Forge resolves the ID against the compilation's explicit registry and manifest.

The preferred extension route is:

```text
private semantic intent
  -> admitted standard ContinuumConstructionDescriptor
  -> Forge standard prismatic materializer
  -> ordinary BRep validation
  -> ordinary AP242 export and reimport
```

The lower-level `ExactBrep` output tier exists for truly unsupported exact constructions. It still runs `BrepBindingValidator`, STEP preflight/export, and STEP reimport. A mesh-derived capability cannot claim `ExactBrep`.

Optional CIR output uses the same construction lineage. M1 admits the sample's exact axis-aligned prismatic CIR and runs `BrepCirConsistencyChecker` through an explicit `CirBrepAssociation`.

## Determinism, provenance, and trust

Registration is explicit; no arbitrary assembly scanning occurs. Capability enumeration and resolution use ordinal sorted IDs. Duplicate extension IDs, extension version conflicts, capability ID collisions, and capability version conflicts fail registration—there is no last-loaded-wins behavior.

M1 rejects non-deterministic capabilities. Deterministic compilation assumes identical typed inputs, compiler version, extension IDs/versions, and resource hashes. Artifact hashes include STEP text, Template specialization identity, capability/version evidence, and provenance.

Every extension artifact records host/module identity, Template specialization, capability ID/version, extension ID/version, construction identity, and final artifact validation. Raw plugin exceptions are wrapped in typed Forge diagnostics with capability and source context.

Forge extensions are trusted compiler plugins in M1. They are not sandboxed, process-isolated, or suitable for untrusted code. Capabilities should be stateless; descriptors are immutable and invocation state arrives through an explicit context.

## Package and future seams

A normal .NET package may contain extension registration, Firmament modules, generated bindings, and tests. The package is activated explicitly by a host and declared in `ForgeExtensionManifest`. No marketplace or semantic-version negotiation is part of M1.

The invocation model uses canonical values, dictionaries, resource hashes, module/template names, and extension identities rather than C# object identity. This is the future seam for Python, TypeScript, Go, RPC, and CLI descriptors; C# is the canonical implementation today.

Future Firmament analysis Templates such as `ProofLoadAnalysis` can use the same host invocation and capability registry. New analysis output classifications and lowering targets can be added without making Firmament a runtime language or implementing FEA inside Forge M1.

## Kernel policy

“Aetheris does not support my construction” is not automatically a Kernel feature request. First use existing Firmament and Kernel capabilities. If they are insufficient, implement and register a Forge capability, keep it private if appropriate, and upstream it only after it proves broadly useful, exact, deterministic, generic, well tested, and maintainable.
