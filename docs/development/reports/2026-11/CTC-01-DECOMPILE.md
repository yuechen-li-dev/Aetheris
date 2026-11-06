# CTC-01 decompile

## What changed

- `fixtures/Regression/Decompile/ctc-01.firmament`: NIST CTC-01 reconstructed as design intent in
  current Firmament (about 130 lines). It replaces the design-only `ctc-01-candidate-v2.firmfixture`,
  which did not parse, and its `missing-capabilities.json`. Everything on that list (line/arc profiles,
  slots, mirror, construction geometry) now exists.
- `aetheris analyze compare --shape` (`Aetheris.CLI/StepShapeComparison.cs`) is the fidelity oracle.
  - Carrier matching: faces are grouped by canonical analytic surface, so the comparison does not
    depend on face ids. The report lists missing and extra carriers, with face ids and bounds.
  - Material overlap: Z-ray column intervals through both tessellations give reference-only and
    candidate-only volume, IoU, and the connected regions of difference.
  - CTC-01 compares in about 6 s.
- `analyze volume` bug: the "box minus Z/X holes" exact formulas accepted any all-plane body with
  axis-parallel cylinders. They took the bounding box as stock and returned confident wrong numbers:
  47.3M mm³ for CTC-01, and 28498 for a 15634 plate with a boss. Planes must now lie on the bounding
  box. Planes normal to the hole axis are also allowed, for shoulders and floors.
- `ShapeComparisonCliTests` cover: a missing hole is named, located and measured; self-comparison is
  exact; CTC-01 holds IoU ≥ 0.996 with no extra carriers and only known-gap missing carriers; the
  volume guard; the CLI JSON.

## Evidence

| | Original | Reconstruction |
| --- | --- | --- |
| Bounds | ±400, ±225, −100..50 | identical |
| Volume (ray columns, 2 mm) | 14 655 778 | 14 672 268 |
| IoU | | 0.9967 |
| Analytic carriers | 85 | 71, all on the original's surfaces (0 extra) |

The 14 missing carriers are exactly three features, all inexpressible today:

| Missing | Volume | Why |
| --- | --- | --- |
| 2 × Ø20 blind drilled holes with 118° points, from the −Y face | 2 × 16 039 | Compose holes are `+Z` `ThroughAll` only; no subtract |
| R10 fillets where the lugs' inner walls meet the plate underside (mitred) | 4 × ~3 515 | No selection of individual concave edges |
| R5 fillet at the hex boss's foot | ~1 840 | One finish per body (the boss top chamfer uses it); concave boss foot refused |

The first comparison found one real modelling error, which carrier matching made obvious: the hexagon
was rotated 30° (flats on ±y instead of ±x). It showed as six extra and six missing planes at the boss.
The hex top chamfer, added with `EdgeFinish<Chamfer> { on: Top; target: HexBoss.Outer }`, then matched
all twelve chamfer and flat planes.

## Language friction, ranked by value

1. **Holes on any planar face, with a drill point.** For example
   `Hole<Shaft> { on: PlateStock.Front; center: …; end: Blind 45mm; tip: Drill 118deg }`. This is the
   only CTC-01 gap that loses volume rather than a blend, and side or angled holes are routine on
   machined parts. There is no workaround, because public Boolean subtraction is deliberately absent.
2. **Junction fillets by feature relationship.** For example
   `EdgeFinish<Fillet> { where: BackLeftLug meets PlateStock; radius: 10mm }` or `at: Hex.Foot`. This
   would also mean allowing more than one finish per body when their edge sets are disjoint. Both fillet
   gaps are "round the edges where feature A meets feature B", which is how designers say it. The
   current selectors only name a profile rim on Top or Bottom.
3. **Path vertex rounding and coordinate-terminated lines.** For example `round: 10mm` on a step, and
   `Line { until: y = -95mm }`.
   - Every arc in the CTC-01 outline is a fillet of a sharp polygon corner. Each V-notch needed three
     √3-derived Statics, and each rectangular notch takes five steps.
   - Rounding hand-derived lengths to three decimals left 2 µm gaps. These surfaced as arrangement
     "dangling edge" rejections at an unrelated vertex, a trap with no pointer to its cause.
4. **Trig and `Sqrt` in Static arithmetic.** `Tan(60deg)` is rejected, so `Pow(3, 0.5)` stands in.
   The diagnostic talks about `tol` placement, which is unrelated.
5. **`Close` on an already-closed path** fails with `concept-path-zero-length`. It should accept
   closure within tolerance. The workaround was to restart the path mid-edge.
6. **`Profile X = RegularPolygon2<N> { … }`** is rejected (`firmament-boundary2-profile-value-unsupported`),
   although `profiles.md` lists it beside `Rect2` and `Slot2`. It works through a Concept Struct and
   `TraceLoop`.

Diagnostic quality: the items 5 and 6 diagnostics repeat their code as the message. The boss-foot fillet
refusal speaks in planner terms ("single-stock prism rebuild", "section-stack planner") instead of
naming the unsupported geometry.

What worked well: Turtle Paths with named steps, `Mirrored Profile` and `Mirrored Feature` across
traced construction planes, `Slot2` and `RoundedRect2` one-liners, `Boss on: Top`, pockets through
`ThroughAll`, and holes through bosses and lugs. No construct had to be faked. The model reads as the
drawing would: plate, lugs, boss, cuts, holes, chamfer.

Not investigated: `analyze section` reports "no bounded geometry" on CTC-01 at z = −75, −55 and −25.

## Follow-up: items 4–6 and diagnostics fixed

- `Tan` and `Sqrt` join `Pow`, `Sin` and `Cos`. All five now come from one intrinsic list, which Function
  binding also uses for its reserved names.
  - Correction to item 4: `Sin` and `Cos` already worked. They passed only because the "deg" in
    `Sin(60deg)` contains an `e`, which the Static literal sniff reads as Float.
  - Static typing now takes an evaluated literal's Int/Float spelling from its own text, not from the
    authored span it replaced. `Sqrt(3)` was typed Int and rejected.
  - Unknown calls and out-of-domain calls (`Sqrt(-2)`, `Tan(90deg)`) get specific messages.
- `Close` after a path that already returns to its start adds no edge. It previously failed with
  `concept-path-zero-length`.
- `Profile X = RegularPolygon2<N> { … }` builds. One-line Profile values now accept specialized
  shape types.
- `concept-path-*` and `firmament-boundary2-*` codes get explanatory messages. The generated
  `__Boundary` suffix is no longer shown to authors.
- The CTC-01 fixture now uses `Tan(60deg)`, `Sqrt(2)`, a plain final `Close` and the one-line hexagon. Its
  geometry is unchanged (IoU 0.9967, 71/85 carriers).
- Tests: `AuthoringConvenienceCliTests`, with fixtures under `fixtures/Regression/AuthoringConvenience`
  and `fixtures/Invalid/AuthoringConvenience`.

Found while checking item 3: Paths already have semantic `Chamfer`, `Cutback`, `Step`, `Notch` and `Tab`
steps, but none are in the public docs. `Notch` is rectangular with sharp corners (width, depth), so
CTC-01's notches still need a corner radius, and its V-notches a V form with a tip radius. Item 3 is
better framed as extending these steps than as new path primitives.

## Follow-up: drilled holes (item 1)

Plane-hosted holes already existed as `Hole<Shaft> { From: <Construction Plane>; End: ShaftDepth(...);
Termination: DrillPoint }`. The public docs said they were unsupported. On CTC-01 they failed for reasons
that were bugs and limits, not missing design:

- **Mouth-face selection matched only plane and Z slab.** The front edge is three coplanar faces split by the
  V-notches, so the match was ambiguous. A mouth host must now enclose the mouth disk inside its own boundary.
  Faces the disk merely touches stay candidates, so tangency is still refused.
- **Only one drill per body.** Drills now apply in sequence to the exact plan. A pairwise check (segment
  distance against the sum of radii, through total depth) refuses touching tool envelopes, and an
  already-drilled face can host another mouth.
- **The drilled face's geometry binding was copied after the face was removed.** STEP preflight rejected the
  dangling binding in the composed-chamfer route.
- **The composed-chamfer route re-emitted the stack without its drills.** Both routes now apply drills through
  one helper.

New behaviour:

- **Blind means drilled.** A blind `Hole<Shaft>` ends in a 118° point. `termination: DrillPoint { pointAngle }`
  changes the angle and `termination: Flat` keeps a flat floor. `End: Blind <d>` is the full-diameter depth.
  On Box stock, the top-face drill is built in a plane derived from the box (X, −Y, −Z at the top centre).
- **`From: Outline.FrontMiddle`** names the plane a straight Path step sweeps. It is computed from source as
  the Concept plane an author would write. Right-handedness forces its u axis to run against the step for a
  counter-clockwise outline, the same frame an explicit plane with that normal and up gets.
- The canonical blind-hole example, the Learn lesson and the reference describe the new default. The
  all-edge rounding witness states `termination: Flat`.

CTC-01 now drills both side holes as `from: Outline.FrontMiddle; end: Blind 45mm` plus a mirror. Carriers are
75/85 with 0 extra, and IoU is 0.9989. Everything still missing is fillet: the lug underside R10 and the hex
foot R5.

`analyze compare --shape` also skips rays with an odd crossing count, near cone apexes and seams, on both
sides. It used to read them as empty, which showed about 1100 mm³ of false difference per drill tip.

## Follow-up: shape-based outline, delta notches, traced lugs

The fixture no longer turtle-paths the outline. The model now follows the part's own structure:
- **Outline:** two `RoundedRect2` ends (R50) and a `Rect2` bridge in a Concept Struct, joined by four R25
  waist `Arc2`s, with the back-right corner replaced by a chamfer `Line2`, all traced with `|>`.
- **Notches:** `Template<Center, Owner> ProfileDelta`s for the V-notch and the slot notch, applied four times
  to the traced `Front` and `Back` edges.
- **Lugs:** traced from the end shapes themselves, so corner radii and the chamfer are stated once; three are
  mirrors of one.
- **Side drills:** `from: Plate.Front`. Planes can now come from a named straight edge of a traced Profile,
  as well as from a Path step.

The result is identical to the turtle version: 75/85 carriers, 0 extra, IoU 0.9989. Against the turtle plate
alone, the shape-built plate matches all 37 carriers at IoU 1.0.

What this showed about the language:

| Finding | Effect |
| --- | --- |
| The V-notch tip **is** expressible: `Diagonal` flanks plus concave `Round` transitions give the exact R10 tip | Corrects item 3 above |
| `Round` needs `Concave: true` for both the V tip and the slot floor corners; the wrong value builds a plausible wrong outline with no diagnostic | Only `compare --shape` caught it; add a check that a round is tangent to its neighbours, or a clearer default |
| Each waist needs 3 `Point2`s and an `Arc2` (16 lines for four R25 waists) | Fixed by `|> Round(r)` (below). A corner-round stage in traced loops (`|> Round 25mm`, filleting the junction of the stages either side) would replace them; it would also give Path vertex rounding |
| `RoundedRect2` publishes no tangent points; they exist only as internal `Name_Path.Step.End` names | Made unnecessary by partial spans (below) |
| A partial span needs both `from` and `to`; `edge to P` alone is a stage-type error | Fixed (below) |
| Edges from two shapes collide (`Bottom`, `Left`, ...), so every stage needs `as` | Acceptable; the error names the collision |
| A delta's `CenteredAt` is a distance from the edge's start, not a coordinate | Notch centres become 115/335/75/375 mm, not x = ±110/±150 |
| Template arguments take scalars but not inline record literals (`P: Spec { Center: 115mm }`) | Use scalar parameters or a named Static |
| A `Feature` cannot return `Add`, and `Boss` only grows up from a top | No reusable lug/foot feature under a plate; lug reuse happens at Profile level |
| Two `ProfileDelta`s with one name reported `apply-unresolved` | Now reported as `profile-edit-apply-ambiguous`, naming the count |

`Template<…> Feature` is not a form. Features are already parameterized (`Feature X(params) -> Kind`); Templates
specialize Struct/Model/Concept Struct/Panel/SheetMetal/Profile/ProfileDelta, plus the separate record-array
feature-template form used by `Pattern … over`. For CTC-01 the useful reuse was `Template<…> ProfileDelta`.

## Follow-up: `Round(r)` stages and partial spans

- **`|> Round(r)`** between two traced stages fillets their junction tangentially. It trims both neighbours
  (lines or arcs), bends into whichever side the corner turns, and works before `Close`. It refuses
  tangent junctions, stages that do not meet, and radii that do not fit, naming the stage. The four
  waists are now `|> Round(25mm)`; their 12 `Point2`s and 4 `Arc2`s are gone.
- **Partial spans:** `guide from P` runs to the guide's end, and `guide to P` continues from the current
  point. The `RoundedRect2` tangent points no longer need restating, so all five tangent-point `Point2`s
  are gone. Publishing them from `RoundedRect2` is therefore unnecessary.
- **`analyze volume`:** it claimed an exact box-minus-holes volume for a rounded rectangle, 5943 instead of
  7114 mm³, by subtracting the corner cylinders as holes. A hole's disk must now lie strictly inside the
  box footprint; anything else is refused.

Evidence:
- The CTC-01 result is unchanged: 75/85 carriers, IoU 0.998904 against the NIST file, and IoU 1 with 75/75
  carriers against the previous fixture.
- A stage-rounded rectangle matches `RoundedRect2` on all 10 carriers at IoU 1.
- `ProfileCornerRoundTests` covers line/line convex, concave and oblique corners, plus line/arc, arc/line
  and arc/arc corners: tangency and continuity at both joins.
- The full parallel lane is green.

## Verdict

Meaningful progression. CTC-01 builds with every feature Firmament can express, and the comparison
proves the rest are exactly three language gaps. Items 1 and 2 are the features worth designing. Items
3–6 are small and independent.
