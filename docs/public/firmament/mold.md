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
| `inserts` | A margin such as `10mm`: cut the core and cavity inserts from a block that much larger than the part (below). |
| `Action name { … }` | A side action (slide, side core): `pull: -Y` is the direction it withdraws along; `near: [x, y, z]` picks among undercuts more than one action could release. |

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
  blocks the way. Undercut faces that touch are one undercut. Each is tried along the four directions across the
  pull, and the finding names the ones that release it, or says that none does (it needs a lifter, a collapsing core
  or another pull). Steps smaller than a few thousandths of the part's size are below what this sees, and the
  absence of an undercut between samples is not proven.
- **Actions.** An undercut that a declared `Action` can withdraw from is formed by that action instead: every point
  of it faces the action's pull or runs parallel to it, and is free along it. Its faces are checked for draft against
  the action's pull. An action that releases no undercut is an error that lists the undercuts and their directions.
- **Split faces.** A face that turns from one half to the other (a cylinder lying across the pull, a sphere) is split
  where it is parallel to the pull: exactly at a cylinder's axis or a sphere's centre, otherwise at the samples
  nearest parallel. With the parting plane through that silhouette it is no finding; off it, the face straddles.
- **Parting line.** The edges between the two halves, in closed loops. A loop on the declared plane is the parting
  line. A flat loop across the pull, off the plane, around an opening (a hole through a hollow's top) is a shut-off:
  the halves touch across the opening there. Any other loop leaves the plane, and the part needs a stepped parting
  surface or another plane. Where the line runs along a cosmetic face it leaves a witness line.
- **Report.** Projected area along the pull (for clamp tonnage), volume and shot mass from the material's density.

## Side actions

```firmament
Mold LatchTool {
  part: Latch
  material: ABS
  pull: +Z
  parting: Plane { at: face(-Z) }
  Action pinCore { pull: -Y }
}
```

The [latch](../../../fixtures/Canonical/Mold/side-action-latch.firmament) has a pin hole drilled from its front
face. Without `pinCore` the review reports it as an undercut that releases along −Y; with it, the hole is formed by
the action, and the report lists the action's faces and their draft along −Y.

## Core and cavity inserts

`inserts: 10mm` writes the two halves of a two-plate tool beside the part, as `<part>.cavity.step` (the +pull half)
and `<part>.core.step`:

- each insert is the part's own faces of its half, on the part's exact surfaces and edges, closed by the parting
  face (the block's section at the parting plane less the part's outline) and the rest of a block 10 mm larger than
  the part on every side;
- each shut-off becomes a face across its opening in both inserts;
- the report states the block, both inserts' volumes and the part's: block = cavity + core + part.

Inserts are refused, with the reason, when the tool needs more than two plates or a flat parting face: a side
action, a face that neither half forms alone, or a parting line that leaves the plane. The
[housing](../../../fixtures/Canonical/Mold/housing-inserts.firmament), a drafted hollow shell with a vent through its
top, is cut into inserts with one shut-off.

## Stages

| Finding | Concept | Prototype | Tooling |
| --- | --- | --- | --- |
| Undercut, straddling face | warning | error | error |
| Draft, parting line off plane, parting line on a cosmetic face | warning | warning | error |

An error fails the build. Problems in the Mold block itself (an unknown material, a selector that names no face, an
action that releases nothing, inserts that cannot be cut) are always errors.

## Not yet

An `Extrude` and a `Compose` take `draft:` ([drafted Extrude](../reference/language/solids-and-features.md#drafted-extrude),
[drafted Compose](../reference/language/solids-and-features.md#drafted-compose)), including hollow ones; their walls
are exact drafted planes and cones, and pass the draft check at their angle. The
[drafted hollow cover](../../../fixtures/Canonical/Mold/drafted-hollow-cover.firmament) passes at the `Tooling` stage
with no findings.

The [drafted housing](../../../fixtures/Canonical/Mold/drafted-housing.firmament), a hollow shell with a hollow
boss and a pocket, passes too.

Lifters and inserts for tools with side actions, stepped and contoured parting surfaces, gates, ejectors,
wall-thickness, rib and boss rules, flow length, and fillets and chamfers on drafted walls are later phases. An undrafted part reports its
vertical walls as short of draft; the `Concept` stage keeps that a warning. `PlasticShell` remains the bounded route
for its frustum enclosures.
