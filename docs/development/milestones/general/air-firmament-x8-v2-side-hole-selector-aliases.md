# AIR-FIRMAMENT-X8 — V2 side-hole selector aliases

## Purpose and scope

AIR-FIRMAMENT-X8 proves that parser-backed Firmament V2 `expose { ... }` aliases are source-level semantic references that can drive the controlled side-hole feature target path. Scope remains one `Box` solid named `base`, `modify base`, one `region sideHole`, one `cut Cylinder`, FaceRef aliases only, and the existing controlled `+X` attach to `-X` through route.

## Relationship to X3, X4, X6, and X7

- X3 introduced parser-backed semantic references and `=>` exposure aliases.
- X4 introduced the direct selector V2 side-hole syntax.
- X6 carried controlled radius variation through the same side-hole path.
- X7 carried face-local center offset through the same side-hole path.
- X8 connects X3 aliases to the X4/X6/X7 controlled side-hole intent path.

## Supported source syntax

```firmament
solid base: Box {
    size: [10, 8, 6]
    expose {
        face(+X) => right
        face(-X) => left
    }
}

modify base {
    region sideHole on right {
        cut Cylinder {
            radius: 1
            center: [1, 0]
            through: left
        }
    }
}
```

## Alias resolution semantics

Alias resolution happens after parsing against the modified solid's exposure table and before side-hole semantic intent finalization. For X8, aliases in `modify base` resolve only against `base` exposures. The preserved semantic intent records both source target facts (`right`, `left`) and resolved face facts (`+X`, `-X`).

## Alias scope

X8 aliases resolve against the modified solid's exposure table only. There is no arbitrary alias scope across solids, no cross-model aliasing, no mutation, and no rebinding.

## Supported alias target type

Only `FaceRef` aliases are supported as side-hole attach and through targets.

## Unsupported alias target types

`LoopRef`, `EdgeRef`, `VertexRef`, and feature-output refs are not feature targets in X8. LoopRef aliases are rejected when used as side-hole attach/through targets.

## Trace JSON/text alias evidence

Trace text includes exposure rows, alias source targets, resolved selectors, and lowering aliases such as `right -> face(+X)` and `left -> face(-X)`. Trace JSON includes stable region fields for source target, target kind, resolved selector, and ref type.

## Artifact/manifest behavior

The generated-on-demand artifact workflow writes `side-hole-aliases-v2.step`, `side-hole-aliases-v2.trace.json`, `side-hole-aliases-v2.trace.txt`, and `manifest.json`. The manifest records source aliases, resolved selectors, radius, center, center frame, controlled-fixture-only status, and that general side-hole support is still false.

## Valid and invalid fixtures

Valid fixture:

- `fixtures/Regression/Region/valid/side-hole-aliases-v2.valid.firmfixture`

Invalid fixtures:

- `fixtures/Compatibility/LegacyAliases/Invalid/Region/side-hole-alias-unknown-on-v2.invalid.firmfixture`
- `fixtures/Compatibility/LegacyAliases/Invalid/Region/side-hole-alias-unknown-through-v2.invalid.firmfixture`
- `fixtures/Compatibility/LegacyAliases/Invalid/Region/side-hole-alias-loopref-on-v2.invalid.firmfixture`
- `fixtures/Compatibility/LegacyAliases/Invalid/Region/side-hole-alias-wrong-face-v2.invalid.firmfixture`

## What this proves

`=>` exposure aliases can drive controlled side-hole feature intent in the real parser-backed V2 path and reach `region-parent-integrated` / `Integrated` / `Closed` / `Succeeded`.

## What this does not prove

X8 does not add arbitrary alias scope, arbitrary faces, general side-hole support, LoopRef/EdgeRef/VertexRef feature target support, blind holes, generic Boolean admission, CIR topology authority, templates/concepts/PMI, or `where`.

## Tests run

Use the active .NET build/test path plus targeted CLI/kernel tests and trace/artifact commands for the alias fixture.

## Next milestone recommendation

AIR-FIRMAMENT-X9 — controlled V2 side-hole face-pair variation. Keep X9 controlled; possible routes are reverse `face(-X)` to `face(+X)` or a separately defined `+Y` to `-Y` path after local-frame semantics are specified.
