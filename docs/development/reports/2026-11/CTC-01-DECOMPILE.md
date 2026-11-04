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

## Verdict

Meaningful progression. CTC-01 builds with every feature Firmament can express, and the comparison
proves the rest are exactly three language gaps. Items 1 and 2 are the features worth designing. Items
3–6 are small and independent.
