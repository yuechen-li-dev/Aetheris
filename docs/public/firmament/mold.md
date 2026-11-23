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
| `policy` | Optional `Static` whose fields override the material: `MinimumDraft`, `MinimumWall`, `MaximumWall`, `MinimumInsideRadius`, `MaximumFlowRatio`, `EjectorClearance`. |
| `minimumDraft` | Optional angle overriding the policy's minimum draft. |
| `stage` | `Concept` (default), `Prototype` or `Tooling`: how hard findings are (below). |
| `pull` | The direction one half opens: `+Z`, `-X` … or a vector `[0, 0, 1]`. The other half opens the opposite way. |
| `parting` | `Plane { at: face(+Z) }` (the plane through that face) or `Plane { origin: [x, y, z] }`. Its normal is the pull. |
| `cosmetic` | Faces that must not carry a parting line or an undercut. |
| `texture` | `{ faces: [...]; depth: 0.025mm }`: textured faces need 1° more draft per 0.025 mm of depth. |
| `inserts` | A margin such as `10mm`: cut the core and cavity inserts from a block that much larger than the part (below). |
| `Require name => Concept(…)` | A requirement the review's measurements must meet (below). |
| `Gate name { kind; at }` | Where the melt enters (below). |
| `Ejector name { at; diameter }` | A round ejector pin (below). |
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
- **Wall thickness.** At points across each face, the diameter of the largest ball that touches the face there and
  fits inside the part. Where that ball touches an opposite plane parallel to the face, or a coaxial cylinder or cone
  of the same angle, the wall is exact; elsewhere it is the sampled value. A ball held only by a face meeting this one
  at an edge measures the corner, not a wall, and is not counted. A face backing onto less than the policy's minimum
  wall is thin; one backing onto more than its maximum is a thick section, which sinks and voids. The nominal wall is
  the area-weighted median of the faces' thinnest walls.
- **Inside corners.** An edge where two faces meet at more than 5° with the material outside the turn (the floor of
  a pocket, a ceiling meeting a wall) is a sharp inside corner: it concentrates stress and stops flow. Round it to
  at least the policy's `MinimumInsideRadius` (0.5 mm by default), with the outside radius the inside plus the wall.
  A sharp corner is a warning at every stage; a `Require` makes it binding.
- **Report.** Projected area along the pull (for clamp tonnage), volume and shot mass from the material's density.

## Requirements

A house standard is a `Concept`, and a `Require` in the Mold block applies it to what the review measured:

```firmament
Concept HouseWall(minimum: Length, maximum: Length) {
  thinEnough: minimum >= 1.2mm
  thickEnough: maximum <= 3mm
}

Mold CoverTool {
  part: HollowCover
  material: ABS
  pull: +Z
  parting: Plane { at: face(-Z) }
  Require walls => HouseWall(wall.minimum, wall.maximum)
}
```

A requirement is any Boolean expression the [Concept checker](../reference/language/requirements.md) accepts, over
these measurements:

| Name | Type | Value |
| --- | --- | --- |
| `draft.minimum` | Angle | The least draft of any face either half forms. |
| `wall.minimum`, `wall.maximum` | Length | The thinnest and thickest wall measured. |
| `wall.nominal` | Length | The area-weighted median of the faces' thinnest walls. |
| `corner.sharp` | Int | The number of sharp inside corners. |
| `corner.minimumRadius` | Length | The smallest inside radius: 0 with a sharp corner. |
| `undercuts` | Int | Undercuts no action forms. |
| `actions` | Int | Declared side actions. |
| `gates`, `ejectors` | Int | Declared gates and ejector pins. |
| `flow.ratio` | Float | The largest flow ratio (path over wall) from the gates; only when a gate is declared. |
| `flow.length` | Length | The longest flow path from the gates; only when a gate is declared. |
| `knit.count`, `knit.cosmetic` | Int | Knit lines, and those that reach a cosmetic face. |

A requirement that fails is an error at every stage, and names the Concept member that failed
(`HouseCorners.rounded`). One that names an unknown Concept or measurement, or is not Boolean, is an error too. The
report lists each requirement and whether it held. See the
[cover requirements](../../../fixtures/Canonical/Mold/cover-requirements.firmament).

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

`inserts: 10mm` writes the tool's inserts beside the part: `<part>.cavity.step` (the +pull half),
`<part>.core.step`, and `<part>.slide-<action>.step` for each side action:

- each insert is the part's own faces of its half, on the part's exact surfaces and edges, closed by the parting
  face (the block's section at the parting plane less the part's outline) and the rest of a block 10 mm larger than
  the part on every side;
- each shut-off becomes a face across its opening in both inserts;
- a side action's pin is the action's faces, capped across the mouth where they meet the rest of the part; the
  half the mouth opens into carries the same cap, so the pin slides out of it along the action's pull. The mouth
  must be flat and square to the action's pull;
- the report states the block, every insert's volume and the part's: block = cavity + core + part + pins.

Faces that cross the parting plane are cut there first, and the cut part is reviewed again: planes along their
trace, a cylinder lying across the pull along its silhouette (the plane must pass through its axis), and cylinders
and cones about the pull along a circle. The [rod](../../../fixtures/Canonical/Mold/rod-split.firmament) is cut
into two half-round inserts; the [pierced block](../../../fixtures/Canonical/Mold/block-mid-parting.firmament),
parted at mid-height, has every wall, corner and its bore cut, and the bore's section at the plane is a shut-off.
A face cut where it cannot leave its new half (a wall drafted to open upwards, cut below its top) is an undercut,
and the inserts are refused with it.

Inserts are refused, with the reason, when a face crosses the plane on another surface, a face is formed by
neither half, the parting line leaves the plane, or an action's mouth is not flat. The
[housing](../../../fixtures/Canonical/Mold/housing-inserts.firmament), a drafted hollow shell with a vent through its
top, is cut into inserts with one shut-off; the [latch](../../../fixtures/Canonical/Mold/side-action-latch.firmament)
into a core, a cavity and its pin.

## Gates and ejectors

[The cover's tool layout](../../../fixtures/Canonical/Mold/cover-tool-layout.firmament):

```firmament
Mold CoverTool {
  part: HollowCover
  material: ABS
  stage: Tooling
  pull: +Z
  parting: Plane { at: face(-Z) }
  cosmetic: [face(+Z)]
  Gate main { kind: Edge; at: [40mm, 0mm, 0mm] }
  Ejector e1 { at: [-20mm, 0mm, 18mm]; diameter: 4mm }
  Ejector e2 { at: [20mm, 0mm, 18mm]; diameter: 4mm }
}
```

A gate's `at` is a point on the part (within 0.1 mm). Its `kind` says where it can be:

| Kind | Where |
| --- | --- |
| `Edge` | On the parting line (within 0.5 mm), fed from a runner in the parting plane. |
| `Submarine` | On a face the −pull half forms; tunnelled through it. |
| `Pin` | On a face the +pull half forms, fed from a runner plate (a three-plate tool). |
| `Direct` | On a face the +pull half forms, straight from the sprue. |

No gate may be on a side action's face. A gate on a cosmetic face leaves a vestige there, which is a finding.

An ejector's `at` is a point on the part where the pin pushes. The pin must push on a face the −pull half forms,
square to the pull (within 1°). Its footprint, and the policy's `EjectorClearance` around it (0.5 mm by default), must
lie on such faces at its height: clear of walls, holes and edges. The report gives each pin's seat faces and the pins'
total area. A pin on a cosmetic face leaves a witness mark, which is a finding.

## Fill and knit lines

With a gate declared, the review fills the part from its gates. The interior is sampled in cells a third of the
thinnest wall across or larger, up to 600,000 cells. Each cell gets the shortest path to it through the material from
the nearest gate. The **flow ratio** is that path measured in walls (each step divided by the wall behind the nearest
face, summed): over the material's `MaximumFlowRatio` (ABS 150, PC 100, PP 250), the part may not fill, and the report
names the last point to fill. Filling the volume, not the surface, keeps both skins of a wall one front.

A **knit line** is where fronts meet head-on: from two gates, or from one front split around a hole or a wall and
closing behind it. The [gated plate](../../../fixtures/Canonical/Mold/gated-plate.firmament) has one, from its hole to
its far end. A knit line is weaker than the wall around it and shows on the surface; one that reaches a cosmetic face
is a finding. Fronts meeting at less than 45° (meld lines) are not reported.

The fill is sampled: it is a geometric proxy (shortest paths, no viscosity, no pressure, no cooling) that catches parts
that will not fill and knit lines where they will show. It does not replace a flow simulation.

## Stages

| Finding | Concept | Prototype | Tooling |
| --- | --- | --- | --- |
| Undercut, straddling face | warning | error | error |
| Draft, wall too thin or too thick, parting line off plane, parting line on a cosmetic face | warning | warning | error |
| Flow ratio over the material's, a part of the interior the gates do not reach, a knit line, gate or ejector pin on a cosmetic face, an ejector pin off square or inside its clearance | warning | warning | error |
| Sharp inside corner | warning | warning | warning |
| Failed `Require` | error | error | error |

An error fails the build. Problems in the Mold block itself (an unknown material, a selector that names no face, an
action that releases nothing, inserts that cannot be cut, a gate or ejector pin off the part or on the wrong half, an
edge gate off the parting line) are always errors.

## Not yet

An `Extrude` and a `Compose` take `draft:` ([drafted Extrude](../reference/language/solids-and-features.md#drafted-extrude),
[drafted Compose](../reference/language/solids-and-features.md#drafted-compose)), including hollow ones; their walls
are exact drafted planes and cones, and pass the draft check at their angle. The
[drafted hollow cover](../../../fixtures/Canonical/Mold/drafted-hollow-cover.firmament) passes at the `Tooling` stage
with an exact 2 mm wall; its only findings are the sharp edges where the ceiling meets the inner walls, since a
drafted Compose does not yet round them. The
[drafted housing](../../../fixtures/Canonical/Mold/drafted-housing.firmament), a hollow shell with a hollow
boss and a pocket, passes too.

Lifters, slides for undercuts whose mouth is not flat, stepped and contoured parting surfaces, rib and boss rules,
ejector sleeves and blades, runner and cooling layout, and fillets and chamfers on drafted walls are later phases. An undrafted part reports its
vertical walls as short of draft; the `Concept` stage keeps that a warning. `PlasticShell` remains the bounded route
for its frustum enclosures.
