# Firmament V2 canonical authoring

Firmament V2 is one language. Backend specialization is not an author-facing dialect. Parse the document first; route its semantics after parsing.

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

The currently proven low-ceremony production shape is one Box, one top-face ThroughAll shaft Hole, and one disjoint `+Z Boundary Chamfer`. It routes to `CombinedHoleEdgeFinish`, then authoritative STEP export and STEP reimport verification.

Concept-first authoring remains available for construction planes, profiles, composition, templates, semantic selections, and spatial contracts. It is an escalation for spatial complexity, not a prerequisite for a primitive or ordinary host-relative Hole.

## Canonical examples

- `fixtures/FirmamentV2/Canonical/valid/bare-box.firmament`
- `fixtures/FirmamentV2/Canonical/valid/bare-cylinder.firmament`
- `fixtures/FirmamentV2/Canonical/valid/bare-rounded-box.firmament`
- `fixtures/FirmamentV2/Canonical/valid/bare-frustum.firmament`
- `fixtures/FirmamentV2/Canonical/valid/box-through-hole.firmament`
- `fixtures/FirmamentV2/Canonical/valid/box-hole-chamfer.firmament`

## Common diagnostics

- `firmament-v2-canonical-units-invalid`: use `Units: mm`.
- `firmament-v2-canonical-primitive-malformed`: a primitive is missing a required canonical field or has an invalid dimension.
- `firmament-v2-canonical-point2-invalid`: use `Point2(xmm, ymm)`, not `[x, y]`.
- `firmament-v2-canonical-modify-malformed`: a Hole or EdgeFinish declaration is missing a required canonical field.

Legacy lowercase, phase-style, and Concept/Struct sources remain compatibility inputs during migration. New authoring should use the form above.
