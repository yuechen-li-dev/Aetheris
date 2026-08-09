# FORGE-SDK-M1 baseline audit

Before M1, `Aetheris.Forge` targeted `net10.0`, referenced only `Aetheris.Kernel.Core`, and contained three responsibility groups:

1. Descriptor/inspection scaffolding in `Aetheris.Forge.Abstractions`, including package, Concept, Template, capability, fixture, diagnostic, and LLM-guidance descriptors.
2. Firmament Concept interop in `Aetheris.Forge.Abstractions.FirmamentInterop`, including typed scalar views, Concept schemas and validators, a deterministic Concept registry, explicit trusted assembly loading, and built-in standard Concept packs.
3. Legacy/experimental geometry helpers (`ForgeAtomics`, `ForgeRoundedRectangleProfile`) directly used by Standard Library code and tests.

Firmament already depended on Forge for runtime Concept validation. The CLI could accept trusted external Concept-pack assemblies, but activation used reflection over `IForgeConceptPack` implementations and was limited to Concept validation/PMI obligations. There was no host Template invocation API, direct typed host binder, compilation result model, generated binding seam, construction capability registry, exactness classification, extension manifest, capability provenance, or CIR/BRep extension proof.

Compiler invocation lived in `Aetheris.Kernel.Firmament` through file/source parsers and `FirmamentBuildAndExport`. Modern Template declarations were parsed into an internal immutable binder IR, specialized, and erased before AIR, but applications had to appear in Firmament source. InlineStep consumed canonical STEP through `Step242Importer` and preserved canonical hashes/topology maps.

The accidental boundary pressure was that making the existing `Aetheris.Forge` assembly depend on the compiler would create a cycle (`Firmament -> Forge`). M1 therefore retained low-level contracts in `Aetheris.Forge`, exposed a narrow public Template-host bridge from Firmament, and added `Aetheris.Forge.Sdk` above the compiler. Existing Concept-pack behavior and legacy helpers remain compatible but are not the new construction-extension API.

Baseline classification:

| Area | Before M1 |
|---|---|
| Embedding | absent |
| Invocation | source-authored Template applications only |
| Compilation | Firmament file/source services, not Forge-owned |
| Inspection | descriptor and Concept metadata scaffolds |
| Artifact access | compiler/CLI-specific |
| Extension | Concept validation packs only; no construction output |
| Legacy/experimental | rounded-rectangle/extrusion helper API |
