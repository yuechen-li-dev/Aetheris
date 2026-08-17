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

Use PascalCase for semantic/declarative constructs and camelCase for ordinary data slots where the owning domain uses them. The parser is permissive where case has no semantic value. New V2 vocabulary does not use snake_case; external identifiers such as `5052_H32` preserve their source spelling.

Punctuation is construct-specific: braces delimit declarations, lists use brackets, and field separators may be line breaks or semicolons in admitted grammar. Copy a qualified fixture when entering a specialized domain: `SheetMetal` and `Analysis` have their own target forms and property vocabulary. The [target reference](../reference/targets.md) calls out these distinctions.

Use `aetheris validate file.firmament --json` before building. Diagnostics are codes intended for automation plus short corrective messages. See [diagnostics](../reference/diagnostics.md).
