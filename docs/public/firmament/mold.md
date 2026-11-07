# Mold

A `Mold` block declares how a part will be molded: the pull direction, the parting plane, the faces that must look
good, and the molding material's rules. The part is an ordinary `Model` and builds exactly as it would without the
block; the Mold only reads it and reports whether it will release from the tool.

```firmament
Model RoundedTray {
  units: mm
  Profile outline = RoundedRect2 { center: [0mm, 0mm]; size: [120mm, 80mm]; radius: 12mm }
  Compose<Hollow> body {
    wallThickness: 2mm
    openings: [Top]
    Base stock { profile: outline; from: 0mm; to: 24mm; role: Stock }
  }
}

Mold TrayTool {
  part: RoundedTray
  material: ABS
  stage: Concept
  pull: +Z
  parting: Plane { at: face(+Z) }
  cosmetic: [face(-Z)]
}
```

`aetheris build` writes the part STEP and a `.mold.json` report beside it, and prints a summary:

```text
Mold TrayTool on RoundedTray (ABS, Concept, pull [0, 0, 1])
  faces 27: 2 +pull, 1 -pull, 24 parallel, 0 straddling; 24 short of draft, 0 undercut
  projected area 9475.8 mm², volume 35369.8 mm³, shot 37.14 g; parting line 8 edges
```

## Fields

| Field | Meaning |
| --- | --- |
| `part` | The `Model` in the same file. Optional when the file has one Model. |
| `material` | Default rules: `ABS`, `PC`, `PC_ABS`, `PP`, `HDPE`, `PA66`, `PA66_GF30`, `POM`, `PMMA`. |
| `policy` | Optional `Static` whose fields override the material: `MinimumDraft`, `MinimumWall`, `MaximumWall`, `MaximumFlowRatio`. |
| `minimumDraft` | Optional angle overriding the policy's minimum draft. |
| `stage` | `Concept` (default), `Prototype` or `Tooling`: how hard findings are (below). |
| `pull` | The direction one half opens: `+Z`, `-X` … or a vector `[0, 0, 1]`. The other half opens the opposite way. |
| `parting` | `Plane { at: face(+Z) }` (the plane through that face) or `Plane { origin: [x, y, z] }`. Its normal is the pull. |
| `cosmetic` | Faces that must not carry a parting line or an undercut. |
| `texture` | `{ faces: [...]; depth: 0.025mm }`: textured faces need 1° more draft per 0.025 mm of depth. |

Faces are named by `face(+X)` … `face(-Z)` (the planar faces lying in the part's extreme plane in that direction) or
by half: `side(+pull)` is every face the +pull half forms, `side(-pull)` every face the other half forms.

The material defaults are common design-guide values (ABS: 1° draft, 1.14–3.56 mm wall). They are a starting policy;
a house rule goes in a typed Static:

```firmament
Record MoldingPolicySpec { MinimumDraft: Angle }
Static HouseRule: MoldingPolicySpec = MoldingPolicySpec { MinimumDraft: 1.5deg }

Mold CupTool { part: Cup; material: PP; policy: HouseRule; pull: +Z }
```

## What is checked

- **Side.** Each face is formed by the +pull half (its outward normal has a +pull component), the −pull half, either
  (parallel to the pull), or it straddles (faces both ways, so the parting line must split it). A face parallel to
  the pull goes with the half it can leave towards, and with the half on its side of the parting plane when both are
  open.
- **Draft.** The angle between each face and the pull, against the policy (plus texture). Exact on planes and on
  cylinders and cones about the pull; elsewhere the minimum over the face's mesh.
- **Undercuts.** Points across each face are traced along its half's pull; a face is an undercut where the part
  blocks the way. Steps smaller than a few thousandths of the part's size are below what this sees, and the absence
  of an undercut between samples is not proven.
- **Parting line.** The edges between the two halves. When it leaves the declared plane the part needs a stepped
  parting surface or another plane; where it runs along a cosmetic face it leaves a witness line.
- **Report.** Projected area along the pull (for clamp tonnage), volume and shot mass from the material's density.

## Stages

| Finding | Concept | Prototype | Tooling |
| --- | --- | --- | --- |
| Undercut, straddling face | warning | error | error |
| Draft, parting line off plane, parting line on a cosmetic face | warning | warning | error |

An error fails the build. Problems in the Mold block itself (an unknown material, a selector that names no face) are
always errors.

## Not yet

Side actions and lifters, gates, ejectors, wall-thickness, rib and boss rules, flow length, and draft on Extrude and
Compose walls are later phases. Until draft can be authored, a Compose part reports its vertical walls as short of
draft; the `Concept` stage keeps that a warning. `PlasticShell` remains the bounded route for its frustum enclosures.
