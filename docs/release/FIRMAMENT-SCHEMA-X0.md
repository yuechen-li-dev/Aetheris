# FIRMAMENT-SCHEMA-X0

**Verdict: Meaningful progression.** A compile-time generated, opt-in semantic schema now exists, Helix and solid Loft LX field help reads it, the Web SDK exposes it, and Helios can render its field/output descriptors generically. The mission's accepted criterion is not met because authored values/ranges and source rewrite are not supplied by the compiler/Web session, and Box/Hole metadata is not yet the parser's sole semantic declaration.

## Delivered

- Roslyn incremental generator in `Aetheris.Firmament.SchemaGenerator`; `FMS001` reports invalid declarations at build time.
- Static immutable registry with explicit construct, field, and output IDs, value kind, unit kind, required/default/choices, source editability, context, entry sketch, compatibility alias, and source output role.
- First descriptors: Box (Size or Bounds), Hole (On, Center, Diameter, End, PatternIdentity, Wall output), Helix (WireForm owner and AxisCoil compatibility), solid Loft (two profiles/frames, rule, correspondence, reference, twist), and Concept (dynamic semantic requirements).
- Existing Helix and solid Loft LX field lists replaced by generated schema projections. Hollow Loft extension fields remain parser-owned. Existing parser context recognition remains authoritative.
- Versioned `Aetheris.language.schema()` Web SDK API and generic Helios Inspector field/output metadata.

## Coverage and limits

| Construct | Generated field coverage | LX use | Helios Inspector | Output |
| --- | --- | --- | --- | --- |
| Box | Size, Bounds; alternative requirement described, not encoded as a group | No | Descriptor via compiler-projected `semanticConstruct`; authored value often unavailable | No generated output |
| Hole | Shaft fields; variants and blind-end details incomplete | No | Generic descriptor when model kind is Hole; authored value often unavailable | Wall, Face, `HoleWallFace` role |
| Helix | Axis Helix fields, units, defaults, choices | Yes | Descriptor only when a Helix tree node exists | None |
| Loft | Solid Loft fields; Hollow adds parser-owned Thickness and SeamGap | Yes for solid fields | Descriptor only when a Loft tree node exists | None |
| Concept | Dynamic member names/types remain in Concept IR | No | Descriptor only; no fixed member rows | None |

The canonical entries in the schema are syntax sketches. Loft still needs authored profiles and construction planes; the SDK does not yet provide a complete insertable model for it. Bare-context Helix/Loft discovery and the requested fresh-agent UI workflows were not qualified. This is a concrete remaining gap, not an accepted result.

## Verification

- `dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter 'FullyQualifiedName~FirmamentSchemaGeneratorTests|FullyQualifiedName~FirmamentSemanticSchemaTests|FullyQualifiedName~FirmamentLanguageServiceTests' -m:1` — 8 passed.
- `dotnet build Aetheris.Web.Runtime/Aetheris.Web.Runtime.csproj -m:1` — passed, with existing WebAssembly/trimming warnings.
- `dotnet build Aetheris.slnx -m:1` — passed; `dotnet test Aetheris.slnx --no-build --no-restore -m:1` — all discovered tests passed across the solution. The focused schema tests were rerun after the final build (8 passed).
- `node --test tests/language.test.mjs` in `Aetheris.Web.Runtime/sdk` — 2 passed.
- `npm run sdk:install` and `npm run build` in HeliosCAD — passed.
- Inspector unit tests — 5 passed.
- Helios Chromium correspondence workflows — Box and Hole passed with generic schema fields visible; the existing LX browser workflow also passed.

The generator test compares equivalent declarations in different orders and verifies a duplicate field ID produces `FMS001`. The generated source is emitted under `obj/Generated` for inspection.

## Duplication and next owner boundary

Helix's and solid Loft's separate LX field tables were removed. Parser constants and validation remain authoritative and still duplicate some field names. Box/Hole parser rules are still handwritten. Helios has no Box/Hole-specific schema field rendering branch; its existing specialized source actions and effective override editor remain. No generated docs pipeline was added; this document is handwritten.

The next bounded dependency is a compiler-owned projection from a compiled semantic instance to authored field values and source spans, serialized by the Web session. The current `ParameterInspector` exposes a few numeric model-level properties (`Width`, `Height`, `Thickness`, `HoleDiameter`) and does not associate Box `Size` or Hole `Diameter` with their construct instances. That is why the generic Inspector can show field definitions and Hole Wall but labels most authored values unavailable. A value/span projection would let Inspector show genuine authored/effective values and support a verified source-range rewrite. The schema itself must not become a setter or alternate state store.

## Reflection audit

The generated schema path performs no runtime assembly scan, `PropertyInfo`-based member walk, dynamic mutation, or generated runtime reflection. The schema adds no NativeAOT reflection-preservation attributes. The pre-existing Web host still uses reflection-enabled `System.Text.Json` serialization and produces IL2026 warnings; this milestone does not make that host fully trim-safe. NativeAOT qualification for the full Aetheris application was not run.
