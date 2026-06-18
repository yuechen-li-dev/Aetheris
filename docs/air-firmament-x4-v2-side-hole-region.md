# AIR-FIRMAMENT-X4 — Firmament V2 side-hole region parser slice

## Purpose and scope

X4 adds the first parser-backed Firmament V2 region slice for one controlled side-hole fixture. The supported source shape is one `model`, `units mm`, one Box solid named `base`, one `modify base` block, one `region sideHole on face(+X)`, and one `cut Cylinder` tool with `radius: 1` and `through: face(-X)`.

## Relationship to X1-X3

X1 introduced the isolated V2 parser/frontend for typed-record `Box`. X2 added `Box with` derivation. X3 added Box semantic face references and `=>` exposure. X4 preserves those forms and extends the same V2 parser/AST surface with `modify`, `region`, `cut Cylinder`, and side-hole semantic intent records.

## Relationship to existing AIR Region golden path

The V2 side-hole fixture now lowers its controlled semantic intent to the existing AIR Region side-hole trace chain. The reached stage is `region-parent-integrated`, with parent integration `Integrated`, shell closure `Closed`, and STEP smoke `Succeeded`. The lowering reuses the already-proven controlled +X entry / -X exit side-hole path rather than adding general Boolean or topology authority.

## V1/V2 parser separation

X4 keeps V2 code in the existing `FirmamentV2` parser/AST and `ParseV2Only` frontend path. The V1 parser is not expanded and V2 side-hole fixtures are not routed through the legacy V1 parser.

## Supported X4 syntax

```firmament
model SideHoleV2 {
    units mm
    solid base: Box { size: [10, 8, 6] }
    modify base {
        region sideHole on face(+X) {
            cut Cylinder {
                radius: 1
                through: face(-X)
            }
        }
    }
}
```

`cut cylinder` is also normalized to `Cylinder` for this controlled slice.

## Unsupported constructs

Multiple regions, multiple arbitrary modifications, `add`, non-Cylinder tools, arbitrary faces outside the controlled +X/-X route, arbitrary axes, cylinder height, center offsets, blind cuts, selectors beyond plain `face(+X)` and `face(-X)`, feature-output refs inside regions, `=>` inside regions, `with` inside regions, templates, concepts, PMI, `where`, shell, fillet, chamfer, surfacing, material, pattern, and arbitrary expressions remain unsupported.

## Semantic intent model

The parser preserves a `SideHoleIntent` with target solid `base`, region name `sideHole`, attach face `+X`, through face `-X`, tool `Cylinder`, radius `1`, and units `mm`.

## Lowering attempt and JudgmentEngine usage

JudgmentEngine was not used. X4 has one deterministic controlled route after parser admissibility checks; adding a route scorer would add ceremony without competing admissible strategies.

## Outcome reached

Full golden path was reached for the controlled fixture: `region-parent-integrated`, parent integration `Integrated`, shell closure `Closed`, and STEP smoke `Succeeded`.

## Fixture changes

`fixtures/FirmamentV2/Region/valid/side-hole-v2.valid.firmfixture` is promoted to parser-backed implemented status. Focused invalid fixtures cover unsupported attach face, unsupported through face, invalid radius, and unresolved modify target.

## Diagnostics

Stable diagnostics added include `firmament-v2-modify-target-unresolved`, `firmament-v2-side-hole-only-plus-x-minus-x-supported`, and `firmament-v2-cylinder-radius-invalid`, plus adjacent parser diagnostics for unsupported region/cut/tool/selector cases.

## Tests run

Validation included CLI build/help/trace commands and focused parser/frontend tests. The trace command shows the V2 source facts, semantic intent, AIR Region route, parent integration, shell closure, and STEP smoke evidence.

## Next milestone recommendation

The next milestone should either generalize a second controlled side-hole variant with explicit admissibility limits or add a thin adapter object between `SideHoleIntent` and AIR Region construction so future V2 regions avoid direct trace-probe coupling. Do not broaden Boolean admission or arbitrary face/axis support until that adapter exists.

## AIR-FIRMAMENT-X5 artifact workflow

AIR-FIRMAMENT-X5 adds generated-on-demand artifacts for the same parser-backed V2 side-hole fixture. The command is:

```bash
dotnet run --project Aetheris.CLI -f net10.0 -- trace \
  --fixture fixtures/FirmamentV2/Region/valid/side-hole-v2.valid.firmfixture \
  --out-dir artifacts/air-firmament-x5/side-hole-v2
```

Expected files are `side-hole-v2.step`, `side-hole-v2.trace.json`, `side-hole-v2.trace.txt`, and `manifest.json` under `artifacts/air-firmament-x5/side-hole-v2/`. The artifact remains generated-on-demand and is parity-checked against the AIR-REGION-X13 controlled side-hole path on stable structural facts, not STEP byte equality.
