# Firmament V2 syntax

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

Use `aetheris validate file.firmament --json` before building. Diagnostics are codes intended for automation plus short corrective messages. See [diagnostics](../reference/diagnostics.md).
