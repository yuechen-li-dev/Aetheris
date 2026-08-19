# AIR-FIRMAMENT-X6 — V2 side-hole radius variation

## Purpose and scope

AIR-FIRMAMENT-X6 proves that the parser-backed Firmament V2 controlled side-hole path is parameterized by cylinder radius. The scope remains deliberately narrow: one `Box` solid named `base`, one `modify base` block, one `region sideHole on face(+X)` block, one `cut Cylinder`, positive numeric radius, and `through: face(-X)`.

## Relationship to X4 and X5

X4 introduced the parser-backed Firmament V2 side-hole source and lowered it into the existing AIR Region golden trace chain. X5 locked the generated-on-demand artifact workflow for the canonical radius-1 V2 fixture. X6 keeps those paths green and adds radius-only variation evidence.

## Controlled radius-only variation

The only varied field is `radius`. X6 does not add center offset, arbitrary face pairs, arbitrary cylinder direction, blind holes, multiple regions, multiple cuts, expressions, templates, concepts, PMI, `where`, material/FEA, shell, fillet, chamfer, surfacing, or pattern support.

## Supported source syntax

```firmament
model SideHoleRadius05V2 {
    units mm

    solid base: Box {
        size: [10, 8, 6]
    }

    modify base {
        region sideHole on face(+X) {
            cut Cylinder {
                radius: 0.5
                through: face(-X)
            }
        }
    }
}
```

## Valid radius fixtures

- `fixtures/Regression/Region/valid/side-hole-v2.valid.firmfixture` keeps canonical radius `1`.
- `fixtures/Regression/Region/valid/side-hole-radius-0_5-v2.valid.firmfixture` covers radius `0.5`.
- `fixtures/Regression/Region/valid/side-hole-radius-1_5-v2.valid.firmfixture` covers radius `1.5`.

Each valid fixture reaches `region-parent-integrated`, `Integrated`, `Closed`, `Succeeded`, with no blocker.

## Invalid radius fixtures

- `fixtures/Compatibility/LegacyAliases/Invalid/Region/side-hole-radius-zero-v2.invalid.firmfixture` reports `firmament-v2-cylinder-radius-invalid`.
- `fixtures/Compatibility/LegacyAliases/Invalid/Region/side-hole-radius-negative-v2.invalid.firmfixture` reports `firmament-v2-cylinder-radius-invalid`.
- `fixtures/Compatibility/LegacyAliases/Invalid/Region/side-hole-radius-too-large-v2.invalid.firmfixture` reports `firmament-v2-side-hole-radius-exceeds-clearance`.

## Radius admissibility / clearance rule

The radius must exist, parse as a numeric finite value, and be greater than zero. For the controlled `[10, 8, 6]` box, the side-hole axis is +X to -X, so the transverse dimensions are Y=8 and Z=6. X6 uses a conservative clearance rule: radius must be strictly less than half of the smaller transverse dimension. For this box, radius must be `< 3`; exact tangency at `3` is rejected rather than treated as a fragile boundary case.

## Trace JSON/text radius evidence

Trace JSON preserves the semantic intent radius and the modify-region tool radius. The AIR Region trace receives the same radius so profile, entry-loop, and exit-loop evidence no longer collapse to radius `1` for non-1 fixtures. Trace text prints `Tool: Cylinder`, `Radius: 0.5` or `1.5`, `Through: face(-X)`, `Stage: region-parent-integrated`, `Parent integration: Integrated`, `Shell closure: Closed`, and `STEP smoke: Succeeded`.

## Artifact behavior for radius-specific fixtures

The generated-on-demand artifact workflow remains active. Canonical radius `1` keeps the `side-hole-v2.*` filenames. Non-1 radius fixtures derive radius-specific stems such as `side-hole-radius-0_5-v2.step`, `side-hole-radius-0_5-v2.trace.json`, and `side-hole-radius-0_5-v2.trace.txt`, plus `manifest.json`. The manifest records the actual radius, tool, and through selector. Separate output directories avoid overwrites.

## What this proves

- V2 side-hole lowering is parameterized by radius.
- Radius `0.5`, `1`, and `1.5` preserve golden-path structural facts.
- Non-1 radius artifacts can be emitted without faking radius `1`.

## What this does not prove

- no center offset;
- no arbitrary face pair;
- no arbitrary side-hole support;
- no blind holes;
- no generic Boolean admission;
- no CIR topology authority.

## Tests run

- `dotnet build Aetheris.slnx -f net10.0 --no-restore`
- `./scripts/test-active.sh`
- focused CLI and kernel filtered tests for Firmament V2 side-hole radius behavior
- CLI trace commands for canonical, radius `0.5`, radius `1.5`, and invalid radius fixtures
- artifact emission for radius `0.5`

## Next milestone recommendation

AIR-FIRMAMENT-X7 — controlled V2 side-hole center offset (implemented as the next bounded variation; see `docs/development/milestones/general/air-firmament-x7-v2-side-hole-center-offset.md`).
