# Firmament V2 syntax

## Frontend schema

`schema` is an optional file-level frontend selector, not a namespace or data-validation schema. Put it before `Model` (comments and whitespace may precede it). Schema-free files retain compatibility routing; new specialized sources should state their owner explicitly.

| Schema | Use it for |
| --- | --- |
| `Mechanical` | Ordinary V2 mechanical parts and features. |
| `WireForm` | Formed wire, bends, coils, and knots. |
| `Sweep` | Circular-section Concept Path sweeps. |
| `SectionChain` | Standalone ruled/G1 section-chain bodies. |

For example, begin a formed-wire program with `schema WireForm`. An explicit schema is authoritative: if its frontend rejects the program, Firmament does not retry another frontend. Historical V1 `schema:` mappings remain compatibility metadata; they are distinct from this `schema <Name>` header.

A canonical native part begins with `Model`, declares `Units`, creates named solids, and applies semantic features in `Modify` or `Compose`:

```firmament
Model Plate {
    Units: mm
    Box Body { Size: [80mm, 50mm, 8mm] }
    Modify Body {
        Hole<Shaft> Mount { On: +Z Center: Point2(0mm, 0mm) Diameter: 8mm End: ThroughAll }
    }
}
```

Firmament-owned vocabulary is canonically PascalCase: declarations, semantic features, built-in values, and field names such as `Size`, `Body`, and `Region`. User-defined identifiers are case-preserving and stylistically unrestricted. External engineering identifiers such as `Standard.Materials.Aluminum.5052_H32`, standards, imported identities, and part numbers preserve their source spelling. New Firmament-owned V2 vocabulary does not use snake_case.

Accepted lowercase or historical spellings remain compatibility inputs where they are cheap and unambiguous. They select the same semantic route and produce no style warning. Canonical documentation, snippets, and fixtures use only the preferred spelling; casing is not a type-system distinction.

Canonical fields use a colon between name and value. Braces delimit declarations, brackets delimit lists, and semicolons are optional where the owning line/block grammar is unambiguous. `Model`, `SheetMetal`, `Analysis`/import, and Assembly retain domain-appropriate target grammars; the [target reference](../reference/targets.md) records the intentional distinctions.

Firmament V1 is compatibility history rather than canonical V2 authoring. Bodies embedded in `.firmfixture` entries marked future, not implemented, or invalid are corpus evidence and do not define the public language.

An untransformed `Box` stock is centered in X/Y with `Bottom` at Z=0 and `Top` at Z=`Height`. Its local feature coordinates use that same stock frame: adding holes or edge finishes does not translate the stock.

Firmament admits declarations through typed semantic namespaces. Construction `Profile` names may coincide with `Boss` or `Pocket` feature names because feature `Profile:` references are explicitly typed; these identities remain distinct in structured inspection. Names may not otherwise shadow another declaration in the same semantic namespace.

Use `aetheris validate file.firmament --json` before building. Diagnostics are codes intended for automation plus short corrective messages. See [diagnostics](../reference/diagnostics.md).
