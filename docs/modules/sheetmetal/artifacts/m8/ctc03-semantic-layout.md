# Semantic layout and Firmament syntax

## Implemented boundary

`SheetMetalSemanticLayoutParser` is a bounded compile-time IR builder, not a general constraint solver. It resolves local region coordinates before BRep construction. The result contains structs, datums, tabs, patterns, and constraint evidence. Generated pattern members become ordinary `AuthoredSheetCutSpec` values; tabs are consumed by exact planar flange-profile construction. The downstream formed and flat paths remain authoritative.

Concept contracts expose `SheetRegion`, `SheetPattern`, and `FlatPattern`. Concept Paths include each semantic struct, datum, tab, pattern, constraint, and generated member. Thus inspectability survives semantic erasure into exact profiles.

## Real working syntax

```firmament
Concept Struct Ctc03Layout {
    Datum RearMountCenter {
        On: RearMountingFlange;
        At: (112.395mm, 25.40254mm);
    }

    Pattern BaseFastenerPattern {
        On: MainDeck;
        Feature: Circle { Diameter: 15.875mm; };
        Center: MainDeck.Center + (82.55mm, -78.78826mm);
        Count: 4;
        Pitch: (0mm, 44.45mm);
    }

    Tab ServiceConnectorTab {
        On: AngledServiceFlange.Outer;
        Center: 63.5mm;
        Width: 101.6mm;
        Extension: 12.7mm;
    }
}
```

The service flange uses reusable partial-edge attachment syntax:

```firmament
Flange AngledServiceFlange {
    From: RightWall.Outer;
    Length: 48.514mm;
    Span: 127mm;
    SpanOffset: 2.3876mm;
    Angle: 45deg;
    Radius: 6.35mm;
    Direction: Down;
}
```

## Diagnostics proved by tests

Deliberate contradictory sources are rejected before formed lowering:

- Equal-size: `Require 'StatedIntent' is contradicted by 'Right': feature kind or nominal size differs from 'Left'.`
- Equal-pitch: the diagnostic identifies the declared pitch and the first member spacing that differs.
- Mirror: the diagnostic identifies the member that is not a size-preserving mirror about the declared datum/axis.
- Required member: missing Concept members report `sheetmetal-semantic-required-member` with the absent semantic path.

Diagnostic codes are stable: `sheetmetal-semantic-equal-size`, `sheetmetal-semantic-equal-pitch`, `sheetmetal-semantic-mirror`, and `sheetmetal-semantic-required-member`.

## Why this reduced mistakes

- The four deck holes are one diameter, center, count, and pitch declaration instead of four unrelated coordinates.
- The service-hole groups share local flange coordinates, so the 45-degree formed placement is delegated to region lowering.
- The two mounting pairs use named datum centers; changing a recovered center cannot silently leave one member behind.
- Stable generated paths make the CLI comparison identify the intended pattern member rather than a transient face/edge ID.

The improvement is real but bounded. Irregular wall and mounting-flange outer contours still lack a semantic edge-profile representation and would currently require awkward raw profile coordinates.

## Complexity comparison

Counts use nonblank/noncomment LOC, top-level/nested engineering declarations, and literal two-dimensional millimetre tuples.

| Source | LOC | Declarations | Raw coordinate tuples | Semantic entities | Templates | Resolved constraints |
|---|---:|---:|---:|---:|---:|---:|
| M1 forensic recovery | 263 | 23 | 165 | 0 | 0 | 0 |
| M2 idiomatic skeleton | 19 | 11 | 3 | 0 | 0 | 0 |
| M8 final | 102 | 24 | 19 | 13 (3 datums, 9 patterns, 1 tab) | 0 | 18 |

M8 is longer than the incomplete M2 skeleton because it contains the full opening intent. It is substantially less coordinate-heavy than the forensic representation. Templates were deliberately not used: CTC-03 is not a standard bounded product family, while the introduced constructs are generic.
