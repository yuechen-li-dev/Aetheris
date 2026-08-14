# Current authoring and kernel boundaries

This page is the canonical map after AETHERIS-ARCHAEOLOGY-M6. Firmament V2 is the sole current authoring language. Historical formats remain readable through explicit compatibility boundaries; they do not define current semantics.

```text
CURRENT AUTHORING
  Firmament V2 (.firmament and the Assembly .firmasm profile)
    -> semantic construction / ConstructionIR
    -> recognized bounded Recipe
    -> internal BRep Surgery
    -> validated BRep -> STEP AP242

HISTORICAL COMPATIBILITY
  explicitly versioned Firmament V1 TOON or JSON
    -> V1 compatibility reader/compiler/executor
    -> bounded BrepBoolean where historical execution requires it

  JSON-shaped legacy .firmasm
    -> LegacyFirmasmJsonReader
    -> transform-preserving migration/direct Preview compatibility

GENERIC COMPATIBILITY
  server or external body Boolean
    -> BrepBoolean
    -> recognized bounded family or typed rejection

GEOMETRY QUERIES
  SignedSide / Distance / Intersection / Contact
    -> evidence only; never topology authority
```

## Boundary rules

- File builds recognize V1 only after its compatibility reader proves an explicit `firmament.version` of `1`. Successful V1 builds emit one compatibility warning.
- `FirmamentBuildAndExport.CompileSource`, used by Forge, assemblies, and drawings, is V2-only. Malformed or legacy source cannot cross that in-memory boundary through V1 fallback.
- `.firmasm` means the current Firmament V2 Assembly document profile. JSON-shaped input is detected as legacy migration data; rigid transforms become `Placement LegacyExplicit`. No hierarchy, Interface, Role, or Mate is invented.
- Recipes own reusable recognized exact-construction intent. They are internal today and are the first plausible future advanced construction surface.
- Surgery realizes caller-authorized topology and remains internal. Identity, provenance, and mutation-safety contracts are not mature enough for Forge.Host or KernelSDK exposure.
- `BrepBoolean` is a bounded compatibility/generic facade, not a universal exact Boolean kernel or the preferred semantic-construction API.
- Geometry queries may support validation and recognition. They do not select trims, loop roles, surviving faces, identity, or feature history.

## Development policy

```text
Template / Module
  -> ConstructionIR or semantic construction
  -> existing Recipe
  -> new bounded reusable Recipe when product use justifies it
  -> internal bespoke Surgery when exact topology is already known
```

A new central `BrepBoolean` family requires a compatibility-critical reason, a documented owner, and a migration/removal plan. Numerical intersection never becomes topology authority.

See [BRep Boolean lessons](../kernel/brep-boolean-lessons.md), [BRep Surgery](../kernel/brep-surgery.md), and the [M6 closeout evidence](artifacts/archaeology-m6/README.md).
