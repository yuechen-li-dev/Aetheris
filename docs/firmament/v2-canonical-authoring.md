# Firmament V2 canonical authoring

`Hole<Shaft>`, `Hole<Counterbore>`, and `Hole<Countersink>` are canonical
families. Counterbore requires `CounterboreDiameter` and `CounterboreDepth`;
Countersink requires `CountersinkDiameter` and `CountersinkAngle` in `deg`.
Unknown families (for example `Hole<Blind>`) are rejected immediately.

Canonical `InlineStep`, `Recognize`, and `Replace` use the sequential face IDs
reported by `aetheris analyze`; raw STEP entity IDs are an explicit advanced
form only. See [semantic labeling](v2-semantic-labeling.md) for a copyable
analyze-to-replacement workflow.

Firmament V2 is one language. Backend specialization is not an author-facing dialect. Parse the document first; route its semantics after parsing.

### Concept Path

For ordinary connected line/arc scaffolds, use `Concept Path`: its ordered entries emit named guides and endpoints and can be consumed directly with `Profile Name From PathName`, or with explicit `Loop Outer From` / `Loop Inner From` declarations. It does not replace low-level `Segment` authoring or validate a material boundary. See [Concept Path M1](v2-concept-path.md) and the [canonical fixtures](../../fixtures/FirmamentV2/Canonical/valid/concept-path-rectangle.firmament).

Use this form for ordinary parts:

```firmament
Model Bracket {
    Units: mm

    Box Base {
        Size: [80mm, 50mm, 25mm]
    }

    Modify Base {
        Hole<Shaft> Mount {
            On: +Z
            Center: Point2(0mm, 0mm)
            Diameter: 8mm
            End: ThroughAll
        }

        EdgeFinish TopBreak {
            Face: +Z
            Target: Boundary
            Kind: Chamfer
            Distance: 1.5mm
        }
    }
}
```

## Rules

- Keywords and declaration kinds use PascalCase: `Model`, `Units`, `Box`, `Modify`, `Hole`, and `EdgeFinish`.
- Every canonical document has `Units: mm`. All canonical dimensions carry `mm`; angles carry `deg`.
- `Size` is the one context-directed array literal: `[xmm, ymm, zmm]`.
- Points are typed: `Point2(xmm, ymm)` and `Point3(xmm, ymm, zmm)`. Vectors are `Vector2(x, y)` and `Vector3(x, y, z)`. Bracket points are legacy-only and rejected by the canonical grammar.
- Bare `Box`, `Cylinder`, `RoundedBox`, and `Frustum` declarations require no `Modify` block.
- A `Modify` block may be empty, or contain any currently admitted feature family. A backend route, not parsing, reports unsupported feature combinations.
- A shaft hole uses `On`, `Center`, `Diameter`, and `End`. `End: ThroughAll` is the simple through-hole; `End: Blind <depth>mm` is the canonical blind spelling.
- An edge finish uses `Face`, `Target`, `Kind`, and either `Distance` (chamfer) or `Radius` (fillet).

### PMI

`Pmi` is a canonical top-level block. Use PascalCase record and field names;
it binds to semantic features rather than raw topology IDs. See
[PMI authoring](v2-pmi-authoring.md) for the complete `Datum` plus
`HoleDiameter` example and tolerance syntax.

The currently proven low-ceremony production shape is one Box, one top-face ThroughAll shaft Hole, and one disjoint `+Z Boundary Chamfer`. It routes to `CombinedHoleEdgeFinish`, then authoritative STEP export and STEP reimport verification.

### Profile/Compose holes

An admitted prismatic `Profile`/`Compose` body supports deterministic
Pattern-generated `Hole<Shaft>` features and a direct `Hole<Counterbore>` on
its `+Z` entry face. The counterbore is currently `ThroughAll`, must have its
full circular footprint inside the profile material, and must be disjoint from
all other cavities. Profile/Compose bodies also use the normal `Pmi` export
route. A complete L-bracket with Pattern shafts, Counterbore, Datum, and a
toleranced diameter callout is maintained at
`fixtures/FirmamentV2/Canonical/valid/profile-compose-l-bracket-counterbore-pmi.firmament`.

Other Counterbore orientations/end conditions, touching or overlapping
cavities, non-prismatic composition, and polygon-boundary EdgeFinish remain
unsupported and diagnose explicitly.

## Advanced declarations in the same document

Advanced source uses the same `Model { Units: mm }` root. Declarations may be
ordered before their material `Struct`; the parser resolves the bounded static
graph before materialization.

```firmament
Model SideHolePart {
    Units: mm

    Concept Struct SideLayout {
        Datum: Plane { Origin: [-50mm, 0mm, 0mm]; Normal: [1, 0, 0]; Up: [0, 0, 1] }
    }
    Construction Plane PositiveXWorkplane { Trace: SideLayout.Datum }

    Struct Bracket {
        Box Base { Size: [100mm, 60mm, 12mm] }
        Modify Base {
            Hole<Shaft> SideMount {
                From: PositiveXWorkplane
                Center: Point2(10mm, 6mm)
                Diameter: 8mm
                End: ThroughAll
            }
        }
    }
}
```

Profiles and `Compose` use that same root and retain their production
materializers. `Profile`, `Compose`, and `Selection` are recorded in the
normalized V2 document; the build route consumes the parser-admitted body,
not a raw leading-token dialect choice. See the canonical fixtures below.

### Semantic Slots and Selections

Slots are compiler-owned semantic removals inside an admitted `Compose`. Their
geometry still lowers through the established exact profile route; the
canonical root is the only document entry point.

```firmament
Slot<Capsule> Relief {
    Center: Point2(0mm, 0mm)
    Direction: Vector2(1, 0)
    Length: 80mm
    Width: 40mm
    Extent: ThroughAll
}
Selection ReliefEntry {
    Target: SlotEntry
    Source: Slot(Relief)
    Require: ClosedLoop
}
```

`Slot<RoundedRectangle>` additionally requires `CornerRadius`. A selection is
source-grounded: use `ProfileSegments(...)`, `ProfileLoop(...)`, `Hole(...)`,
or `Slot(...)`, never a B-rep identifier. The canonical parser rejects a
missing field, duplicate selection name, unknown source, or an incompatible
selection result kind before materialization.

### Bounded static authoring

The current canonical static route admits typed records, static record arrays,
one typed Template parameter, Pattern expansion, and static Require checks.
They erase to ordinary admitted declarations before material lowering.

```firmament
Record MountSpec { Center: Point2 Diameter: Length }
Static Mounts: MountSpec[] = [
    MountSpec { Center: Point2(-20mm, 0mm) Diameter: 8mm }
    MountSpec { Center: Point2(20mm, 0mm) Diameter: 8mm }
]
Require ValidDiameter => 8mm > 0mm
Template MountHole(MountSpec spec) { /* Hole<Shaft> using spec.Center/spec.Diameter */ }
Pattern MountPattern Over Mounts { MountHole(Current) }
```

The bounded route emits `Hole<Shaft>` and `Slot<Capsule>` or
`Slot<RoundedRectangle>` from a `Pattern`. Generated feature names are stable:
`PatternName_0`, `PatternName_1`, and so on. A template may also emit a
`Profile`, but that form is deliberately a direct indexed invocation such as
`PlateProfile(Specs[0])`: a generated profile needs a declared identity, so a
profile is not admitted as Pattern output.

Its normalized static AST preserves record schemas, values, template source,
Pattern-generated IDs, and Require results; unsupported output kinds diagnose
rather than silently falling back to a compatibility parser.

Profile-guide `Point2` and `Rect2` declarations accept typed `Point2(x, y)`
literals, including values substituted from a static record. Bracket
coordinates remain accepted for existing fixtures during migration.

### Non-rectangular Profile composition

`Segment.From` and `Segment.To` reference named points only: a declared
`Point2` or a named `Rect2` corner such as `Guide.TopLeft`. Coordinate literals
are not endpoints. A segment traces the infinite `Line2` or Rect2-side
support, so aligned named points can be collinear even when they are not the
guide's original endpoints. Close the loop and use counter-clockwise winding.

The multi-guide L example is
`fixtures/FirmamentV2/Canonical/valid/profile-compose-l-bracket.firmament`.
It uses two overlapping `Rect2` guides plus a named notch point. The valid
fixture intentionally has no EdgeFinish: arbitrary Profile/Compose polygon
boundary chamfer and fillet materialization is not yet admitted. Such a finish
reports `EdgeFinishProfileComposeBoundaryUnsupported`, rather than a missing
semantic source; see the paired invalid fixture.

## Canonical examples

- `fixtures/FirmamentV2/Canonical/valid/bare-box.firmament`
- `fixtures/FirmamentV2/Canonical/valid/bare-cylinder.firmament`
- `fixtures/FirmamentV2/Canonical/valid/bare-rounded-box.firmament`
- `fixtures/FirmamentV2/Canonical/valid/bare-frustum.firmament`
- `fixtures/FirmamentV2/Canonical/valid/box-through-hole.firmament`
- `fixtures/FirmamentV2/Canonical/valid/box-hole-chamfer.firmament`
- `fixtures/FirmamentV2/Canonical/valid/profile-line-extrusion.firmament`
- `fixtures/FirmamentV2/Canonical/valid/profile-compose-base.firmament`
- `fixtures/FirmamentV2/Canonical/valid/profile-compose-l-bracket.firmament`
- `fixtures/FirmamentV2/Canonical/valid/box-hole-pmi.firmament`
- `fixtures/FirmamentV2/Canonical/valid/semantic-slot-capsule.firmament`
- `fixtures/FirmamentV2/Canonical/valid/semantic-slot-rounded-rectangle.firmament`
- `fixtures/FirmamentV2/Canonical/valid/semantic-selection-chamfer.firmament`
- `fixtures/FirmamentV2/Canonical/valid/record-array-pattern-holes.firmament`
- `fixtures/FirmamentV2/Canonical/valid/record-array-pattern-slots.firmament`
- `fixtures/FirmamentV2/Canonical/valid/record-array-template-profile.firmament`

## Common diagnostics

- `firmament-v2-canonical-units-invalid`: use `Units: mm`.
- `firmament-v2-canonical-primitive-malformed`: a primitive is missing a required canonical field or has an invalid dimension.
- `firmament-v2-canonical-point2-invalid`: use `Point2(xmm, ymm)`, not `[x, y]`.
- `firmament-v2-canonical-modify-malformed`: a Hole or EdgeFinish declaration is missing a required canonical field.
- `firmament-v2-selection-malformed`: provide `Target`, `Source`, and `Require`.
- `firmament-v2-selection-unknown-source`: the named Profile, Hole, or Slot does not exist in the canonical document.
- `firmament-v2-selection-result-kind-invalid`: choose an admitted semantic topology role.

Legacy lowercase and phase-style inputs remain compatibility inputs during
migration. New authoring should use the canonical root shown above.
