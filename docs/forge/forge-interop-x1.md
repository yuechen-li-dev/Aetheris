# FORGE-INTEROP-X1 report

FORGE-INTEROP-X1 establishes the process-oriented Forge Host Protocol v1 as Aetheris' first stable language-neutral template boundary. Aetheris remains entirely .NET; the four dogfood clients contain process and JSON plumbing only.

```text
foreign caller
  -> explicit protocol v1 DTO
  -> ForgeProtocolHost public catalog
  -> authoritative Firmament module metadata and Template resolver
  -> real Firmament typed Record/value binder and Require evaluation
  -> real SheetMetal Firmament compiler and Aetheris lowering
  -> formed STEP AP242 / flat STEP / SVG
  -> relative artifact manifest, SHA-256 hashes, structured diagnostics
```

`Aetheris.Forge.Host` remains the single host project. It is now both the same referenceable `Aetheris.Forge.Host` C# assembly and an executable process entry point. Its assembly identity and existing native `ForgeHost -> ForgeModule -> ForgeTemplate -> ForgeInvocation` API are unchanged, and its existing tests still execute directly. The interop host adds explicit wire DTOs rather than serializing `ForgeCompilationResult`, BRep bodies, or other managed domain objects.

## Production witness

The witness is the embedded production Firmament template `Standard.SheetMetal.ElectronicsEnclosure`, not a synthetic box. The request is [`samples/forge-interop-x1/request.json`](../../samples/forge-interop-x1/request.json): 120 × 80 × 40 mm, 1.5 mm sheet, 2 mm inside radius, 8 mm lip, K-factor 0.42, and rectangular relief.

The real specialization identity was `template:94546352d5d67afa`. It produced no diagnostics and these canonical artifacts in the validation run:

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| formed STEP AP242 | 52,386 | `114cd7c0c6a8a364b2943cc955a12d8a96b576a187dfc1957ea9f769296872be` |
| flat STEP | 59,019 | `88c437373fe4fdf91e8f0a5b5e0e5c135b290f0dfe18449ec3ee7c0970c1d075` |
| flat SVG | 4,630 | `1657e3bbc3ef418617b45c5d9ab76a96d70b0ba6356c0e88a7ba07edc18b6519` |

Execution was approximately 335 ms in the first Debug smoke run; timing is observational and is intentionally excluded from artifact identity. The formed and flat STEP artifacts reimport through the Aetheris AP242 importer and pass `Aetheris.CLI inspect`.

Python 3.10, Go, Rust 1.95, and Node 26 executing the TypeScript client each invoked the same host and produced byte-identical hashes for all three artifacts. Run the complete witness with:

```powershell
./scripts/test-forge-interop-x1.ps1
```

The clients live under [`samples/forge-interop-x1`](../../samples/forge-interop-x1). Each reads the same request, starts the Forge Host executable, parses success and artifact paths, and checks that files exist. None parses units, applies defaults, knows sheet-metal geometry, or duplicates Firmament logic.

## Protocol and NativeAOT decision

The complete contract is [Forge Host Protocol v1](protocol-v1.md). A process protocol was selected because it is immediately usable from shell, CI, Python, Go, Rust, C++, TypeScript/Node, and Java with no ABI-specific ownership model. JSON uses explicit DTOs and a source-generated `JsonSerializerContext`; no CLR polymorphic type metadata is enabled.

A Windows x64 NativeAOT publish completes and the native executable handles `info` and a production invocation. The publish reports pre-existing trim/AOT warnings in transitive subsystems that are outside this narrow execution path; the protocol's own serializer path uses source-generated metadata. A C ABI was deliberately deferred: it would add opaque-handle, buffer-allocation, lifetime, and export-versioning commitments without improving the X1 interoperability proof. The process boundary is the v1 product.

## Validation coverage

`ForgeProtocolV1Tests` covers host info, deterministic template ordering, authoritative introspection, valid production invocation, missing field, unknown field, wrong JSON type, wrong unit, unknown template, semantic `Require` failure, unsupported artifact, protocol-version rejection, deterministic rerun, artifact byte/hash agreement, STEP reimport, and structured-stdin/stdout command execution.

The X1 work also closed a binder inconsistency: synthetic host Record values now receive the same missing/extra/type field validation as source-authored Static Records before `Require` evaluation. This is why an interop wrong-unit error now belongs to the canonical Firmament Record diagnostic family instead of degrading into a later expression error.

## Limitations

- The v1 public catalog currently exposes the five embedded standard Sheet Metal product-family templates. Package/plugin template federation and arbitrary module discovery are not protocol capabilities.
- Public execution currently supports the parameter categories exercised by those templates: Length, Angle, integer, number, boolean, string, enum, and nested Record values. Material references, imported STEP resources, type parameters, collections, dates, versions, and profile paths are not yet exposed through this protocol even where native Firmament has related concepts.
- Declared artifact kinds are formed STEP AP242, flat STEP, and SVG. A template returns only requested kinds it declares; DXF and reports are not v1 artifact promises.
- Invocation is serialized per host instance. Thread-safe parallel in-process invocation is not claimed.
- NativeAOT process publishing is validated on `win-x64`; broader RID publication is not claimed by this milestone. There is no C ABI.
- Artifact filenames are host-selected and the manifest paths are relative to the caller-selected output directory. Inline base64 artifact transport and network transport are not supported.
- Protocol v1 stabilizes the boundary shape and semantics, not the membership of the public template catalog. New public templates and artifact kinds may be added compatibly.
