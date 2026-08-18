# Sheet Metal M10 — generic profile programming

Status: **meaningful progression**. The requested generic programming substrate and
the motivating CTC-03 right-wall service profile work through the real authored,
formed, flat, exact-blank, STEP, and comparison paths. CTC-03 as a whole remains
`NeedsReview`; this report does not call it complete.

## What changed

- Existing typed `Template <...>` specialization now accepts `ProfileDelta` targets
  and the typed `ProfilePath` value.
- `SemanticProfileDeltaIr` carries named local levels, ordered semantic members,
  bounded carrier ownership, polarity through inward/outward side, stable descendant
  identities, and exposed-path capabilities.
- The shared semantic-edge resolver supplies deterministic placement, untouched
  carriers, overlap/corner conflict rejection, exact descendants, and provenance.
- Ordinary Profile extrusion and Sheet Metal use the same IR/resolver.
- The embedded `Use Profile.Modifications;` library supplies generic `Tab<T>` and
  `Recess<T>` examples.
- CTC-03's user template constructs both the full right-wall recessed outline and
  `RightWall.ServiceFlangeAttachment`; the compiler contains no CTC/recess feature
  switch.
- The review corrections use the same substrate for two R12.7 service-tab rounds,
  two rear and four front R6.35 mounting-recess rounds. Authored `Slot` features now
  remain analytic capsules through formed BRep, flat STEP, and SVG materialization.
- The four base-flange `Auto` relief requests were removed from this source because
  the recovered blank has open stitched corners, not separate relief cut loops.

## CTC-03 evidence

The canonical source remains
[`../m8/ctc03-final.firmament`](../m8/ctc03-final.firmament), now advanced to the
M10 program while preserving its source-independent construction contract.

- formed STEP: `artifacts/sheetmetal-m10-review/ctc03-m10-formed.step`
- flat STEP/SVG: `artifacts/sheetmetal-m10-review/ctc03-m10-flat.step` and `.svg`
- exact flat status: `Valid`; exact blank and DFM exact-contour checks pass
- topology: one closed body/shell, 15 semantic regions, seven bends, 17 openings
- attachment: 127 mm at 2.6416 mm inset and 2.3876 mm span offset
- all seven bend comparisons and all 17 opening comparisons pass
- two vent profiles contain exact R9.525 semicircular ends; the empty SVG
  `corner-reliefs` group confirms that no extra corner cuts are emitted

The first M10 review and the corrected review compare as follows. The corrections
remove the visible false details; the point-sampled global statistic is not monotonic
because removing four false relief vertices and adding analytic arcs changes both
sample populations.

| Direction | First M10 review RMS / p95 / max (mm) | Corrected M10 RMS / p95 / max (mm) |
|---|---:|---:|
| source → native | 3.286634 / 8.941152 / 9.141310 | 3.546889 / 8.941152 / 9.141310 |
| native → source | 2.790436 / 8.940881 / 12.735724 | 2.667873 / 6.423564 / 8.940881 |

Flat comparison also improved:

| Direction | First M10 review RMS / p95 / max (mm) | Corrected M10 RMS / p95 / max (mm) |
|---|---:|---:|
| source → native | 2.816233 / 5.667441 / 10.563949 | 2.954972 / 8.901978 / 9.094054 |
| native → source | 3.178755 / 6.976011 / 13.258954 | 2.472902 / 4.548127 / 8.945418 |

Width/height residuals remain only 0.002447/0.007820 mm.

## Honest remaining limitation

The global flat comparison is still `NeedsReview`. Eight recovered source-region
outer contours retain localized residuals from 0.972277 to 9.09405 mm, led by
`region-p-0065-0069.Outer`. The visible review targets are now materialized, but the
sampled historical per-region boundaries still differ at shared/released edges.
Therefore M10 completes the generic substrate and this review's explicit corrections,
but not full CTC-03 geometric parity.

M11 corrected the generic `aetheris validate` routing inconsistency: module-shaped
Sheet Metal source now uses the same domain compiler as `build`, `inspect`, `paths`,
and `flatten` instead of the unrelated edge-finish parser fallback.

## Follow-up profile/corner review

`Round` transitions now admit `Concave: true`. The front mounting recess uses it on
the two re-entrant inner transitions, and the rear mounting recess uses it on both
transitions. All six mounting-recess arc directions now match the recovered CTC-03
contours. Exact blank construction composes material attachments in bend-graph order
and splices authored regions along their shared analytic bend edge; nested through-cuts
become inner loops directly. This preserves the concave outline while keeping exact
flat STEP generation valid.

TODO: add partial base-edge attachment/root-trim programs. The four deck/wall junction
spikes cannot be removed faithfully with current flange-outer `ProfileDelta`, endpoint
`CornerProfile`, or automatic relief operations: `Auto` produces separate diagonal
relief cuts that do not exist in the recovered blank. Partial base attachment paths
must own those shared-edge trims.
