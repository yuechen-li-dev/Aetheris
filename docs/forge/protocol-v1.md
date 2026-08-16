# Forge Host Protocol v1

Forge Host Protocol v1 is the stable language-neutral process boundary for public Firmament Templates. It has four discovery/invocation commands and deliberately exposes no AIR, CIR, BRep, parser, solver, EF Core, CLR type, or extension-loading surface.

```text
Aetheris.Forge.Host info
Aetheris.Forge.Host list
Aetheris.Forge.Host describe <template-id>
Aetheris.Forge.Host invoke <template-id> [--request <file|->] --out <directory>
```

Standard output is exactly one JSON document. `invoke` reads UTF-8 JSON from the named file or standard input when `--request` is absent or `-`. Human-readable progress is not written to standard output. Successful commands return exit code `0`; command/request errors return `2`, missing templates return `3`, and invocation failures return `4`.

## Identity and discovery

`info` returns the independent protocol version, Aetheris implementation version, capabilities, and concurrency policy. Protocol v1 is not coupled to an AIR, CIR, BRep, parser, or CLR schema version.

`list` returns public templates in ordinal template-ID order. The built-in IDs use semantic names such as `Standard.SheetMetal.ElectronicsEnclosure`; CLR type names are not identities. Template versions contain a protocol-facing major followed by a digest of the authoritative embedded Firmament module.

`describe` returns the template's parameters, nested Record fields, Firmament types, dimensions, preferred transport units, enum cases, defaults, and declared artifact kinds. This metadata comes from `FirmamentTemplateHostBridge.InspectModule`, which uses the same immutable parser/binder IR as invocation. The host does not maintain a second JSON schema.

## Invocation request

```json
{
  "protocolVersion": 1,
  "arguments": {
    "width": "120 mm",
    "height": "40 mm",
    "depth": "80 mm",
    "thickness": "1.5 mm",
    "lidLipHeight": "8 mm",
    "insideRadius": "2 mm",
    "kFactor": 0.42,
    "reliefPolicy": "Rectangular"
  },
  "artifacts": ["StepAp242", "FlatStep", "Svg"]
}
```

The authoritative enclosure parameter is the `EnclosureSpec` Record shown by `describe`. Callers may send it as `{ "Spec": { ... } }`. When a template has exactly one Record parameter, v1 also accepts its fields at the top level, as above. Names are matched case-insensitively and rebound to authoritative Firmament spelling; unknown names remain errors.

Transport values retain engineering meaning:

- `Length` and `Angle` are strings with units; optional whitespace before the unit is normalized before the Firmament binder validates dimension and unit.
- `integer`, `number`, and `boolean` use the matching JSON primitives.
- `string` uses a JSON string.
- Firmament enums use their declared case as a JSON string.
- Record parameters use JSON objects and are validated recursively by the Firmament binder.

No unit or constraint semantics live in clients. For example, `"12 kg"` transported for a `Length` reaches the normal binder and returns `firmament-template-record-field-type-mismatch`.

## Invocation response

```json
{
  "success": true,
  "identity": {
    "protocolVersion": 1,
    "template": "Standard.SheetMetal.ElectronicsEnclosure",
    "templateVersion": "1+b1ab02987f0a",
    "aetherisVersion": "2.0.0-preview.2+...",
    "specialization": "template:94546352d5d67afa"
  },
  "diagnostics": [],
  "artifacts": [
    {
      "kind": "StepAp242",
      "name": "part.step",
      "contentType": "model/step",
      "size": 52386,
      "sha256": "114cd7c0c6a8a364b2943cc955a12d8a96b576a187dfc1957ea9f769296872be",
      "path": "part.step"
    }
  ],
  "executionMilliseconds": 335.0
}
```

Artifact paths are UTF-8 file names relative to the requested output directory, never developer-machine absolute paths. The host chooses bounded file names and verifies that resolved paths remain directly inside that directory. STEP and SVG are written as UTF-8 without a byte-order mark. `sha256` is the lowercase SHA-256 of the exact artifact bytes and excludes response timing or timestamps.

Diagnostics contain `code`, `severity`, `message`, and optional `target` and `source`. Binder, semantic/DFM, lowering, export, request, and path failures retain distinct codes. Managed exception objects and stack traces are not protocol values.

## Lifecycle, concurrency, and security

Each CLI execution accepts one bounded request, writes its result, and exits. `ForgeProtocolHost` also serializes invocation per host instance; protocol v1 does not claim parallel invocation safety. Independent processes may run concurrently subject to normal output-directory isolation.

Only templates in the explicit public catalog are invocable. The process protocol cannot load assemblies, select CLR types, submit Firmament source, execute C#, invoke arbitrary methods, or control host configuration. The request controls values, declared artifact kinds, and one explicit output directory only.

## Compatibility

Additive optional response properties and new templates/artifact kinds may appear within v1. Existing property meaning, diagnostic code meaning, semantic template IDs, and engineering-value transport rules will not be silently changed. A request with an unsupported protocol version fails before binding or lowering.

C# callers continue to use `ForgeHost`, generated Forge bindings, or `SheetMetalProductFamilies` directly. They do not need to serialize JSON or launch a subprocess.
