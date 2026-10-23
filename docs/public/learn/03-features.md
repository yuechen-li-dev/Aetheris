# 3. Features

Machinists think in features: start from stock, raise a boss, mill a pocket,
drill and counterbore, break the edges. Firmament lets you write a part the same
way, and each feature keeps its name and meaning all the way into STEP.

## The source

[`03-machined-block.firmament`](../../../fixtures/Canonical/Learn/03-machined-block.firmament):

```firmament
// Lesson 3: one body built from a stack of manufacturing features.
Model MachinedBlock {
  units: mm

  Profile stockOutline = Rect2 { center: [0mm, 0mm]; size: [70mm, 44mm] }
  Profile padOutline = Rect2 { center: [-16mm, 0mm]; size: [14mm, 14mm] }
  Profile recessOutline = Rect2 { center: [10mm, 0mm]; size: [20mm, 16mm] }

  Compose body {
    Base stock { profile: stockOutline; from: 0mm; to: 12mm; role: Stock }
    Boss pad { on: Top; profile: padOutline; height: 6mm }
    Pocket recess { on: Top; profile: recessOutline; depth: 4mm; minimumFloorThickness: 3mm }
    Hole<Shaft> bore { on: +Z; center: [-16mm, 0mm]; diameter: 6mm; end: ThroughAll }
    Hole<Counterbore> mountNorth {
      on: +Z
      center: [26mm, 13mm]
      diameter: 6mm
      counterboreDiameter: 11mm
      counterboreDepth: 3mm
      end: ThroughAll
    }
    Hole<Countersink> mountSouth {
      on: +Z
      center: [26mm, -13mm]
      diameter: 6mm
      countersinkDiameter: 12mm
      countersinkAngle: 90deg
      end: ThroughAll
    }
  }

  Modify body {
    EdgeFinish<Chamfer> perimeterBreak {
      target: stockOutline.Outer
      on: Top
      distance: 1mm
    }
  }
}
```

## Compose

`Compose body { ... }` builds one body from an ordered list of operations.

| Operation | What it does | Key fields |
|---|---|---|
| `Base` | the starting stock, a Profile between two heights | `profile`, `from`, `to` |
| `Boss` | raised material on a support | `on`, `profile`, `height` |
| `Pocket` | a finite-depth recess | `on`, `profile`, `depth`, `minimumFloorThickness` |
| `Hole<Shaft>` | a drilled hole | `on`, `center`, `diameter`, `end` |
| `Hole<Counterbore>` | a hole with a flat-bottomed recess | adds `counterboreDiameter`, `counterboreDepth` |
| `Hole<Countersink>` | a hole with a conical entry | adds `countersinkDiameter`, `countersinkAngle` |

`on: Top` means "the current top of the material under this feature". The pad
sits on the 12 mm stock top, so it runs from Z = 12 mm to Z = 18 mm. Order
matters: each operation sees the result of the ones before it.

`minimumFloorThickness` is a manufacturing requirement, not geometry. If a later
change made the pocket floor thinner than 3 mm, the build would fail instead of
producing a fragile part.

A Pocket never goes through. For a through cut, use a `Hole`, a slot, or
`Pocket` with `end: ThroughAll`.

## Hole variants

The part in angle brackets picks the variant, and each variant accepts only its
own fields. A `counterboreDepth` on a `Hole<Shaft>` is an error, not something
silently ignored. That rule applies across the language: an unknown or
misplaced field always rejects.

## Edge finishes

`EdgeFinish<Chamfer>` and `EdgeFinish<Fillet>` break edges. Here
`target: stockOutline.Outer` with `on: Top` selects the whole top rim of the
stock profile. A chamfer takes `distance`, a fillet takes `radius`.

Edge finishing is where CAD kernels get hard, so Aetheris supports specific,
well-defined selections, such as a profile rim, a Box's top boundary or every
edge of a convex stock, and rejects the rest with a diagnostic. The
[fillet and chamfer guide](../firmament/fillet-profiles.md) lists what combines
with what.

## Look at the result

```sh
aetheris build fixtures/Canonical/Learn/03-machined-block.firmament --output artifacts/local/block.step --json
```

The JSON `engineeringFeatures` list reports `Boss`, `Pocket` and `EdgeFinish`,
and `featureCount` is 6. That report is the compiler's account of what it
built, and every feature you wrote must appear in it. Run
`aetheris analyze artifacts/local/block.step --json` and look for
`"structuralAssessment": "enclosed-manifold"`: the STEP file reimports as one
closed solid.

## Try it

- Raise the pocket `depth` to 10 mm. That leaves a 2 mm floor, below the 3 mm
  you asked for, and the build fails with `firmament-pocket-minimum-floor-thickness`.
- Move `mountNorth` to `[12mm, 4mm]`, into the pocket, and read why its
  counterbore no longer has a flat face to sit on.
- Swap the chamfer for `EdgeFinish<Fillet>` with `radius: 1mm`. A fillet around a
  rim that already carries holes and a pocket is one of the combinations not
  supported yet, and the diagnostic says so instead of building a partial fillet.

Next: [4. Reuse](04-reuse.md).
