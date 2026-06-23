# ANALYZE-MAP-DOGFOOD-CTC01-A0 — CTC-01 with current analyze-map tooling

## 1. Executive summary

- **CTC-01 file used:** `testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp`.
- **Commands run:**
  - `dotnet restore Aetheris.slnx`
  - `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`
  - `dotnet run --project Aetheris.CLI -- analyze testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --json`
  - `dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --views six --resolution 16x16 --llm --json`
  - Attempted `dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --views six --resolution 32x32 --llm --json`, but stopped it after roughly eight minutes total wall time for the combined script, after the 16x16 run had already completed and the 32x32 run was still active.
  - Seven bounded suggested point probes from the 16x16 output.
- **16x16 result:** succeeded and produced a compact six-view summary artifact.
- **32x32 result:** did not complete within the bounded dogfood window; no useful JSON was retained.
- **Suggested probes:** useful for confirming representative no-hit exterior/silhouette gaps, exact cylindrical side-wall/round hits, and top/bottom plane stack hits at the central raised region.

**Verdict:** the current `analyze map` path materially changes CTC-01 from “old map path failed on non-narrow BRep” into a measurable, LLM-readable spatial summary. It does not reconstruct feature history, but it gives enough evidence to test the old semantic candidate: a broad prismatic web/plate with rounded lobes, holes/slots/side gaps, cylindrical families, a central raised region, stepped/lowered plateaus, and approximate bilateral repetition are all more legible. The remaining hard part is not raw ray intersection; it is correlation and ranking: components do not yet group into named holes/slots/bosses across views, and point-probe output is too verbose for a model to use without a human-written reduction.

## 2. Baseline facts from `aetheris analyze`

The base analyze artifact is `docs/reports/artifacts/analyze-map-ctc01/ctc01-analyze.json`.

- **Body/shell count:** 1 body and 1 shell.
- **Topology count:** 117 faces, 318 edges, and 206 vertices.
- **Bounding box:** min `[-400, -225, -100]`, max `[400, 225, 50]`.
- **Surface families:** 56 planes, 57 cylinders, 4 cones, 0 spheres, 0 tori, 0 B-splines, and 0 other surfaces.
- **Structural assessment:** `enclosed-manifold`, based on imported topology edge-to-face coedge incidence counts.
- **Unit assumption:** `mm`, with the caveat that the STEP import length units are currently assumed rather than preserved.
- **Diagnostics:** the base analyze `notes` array was empty.

## 3. What the new analyze-map output now makes legible

The 16x16 six-view output is `docs/reports/artifacts/analyze-map-ctc01/ctc01-six-view-16x16.json`. Compared with the old “map failed” state, this is the main change: Codex can now read a structured spatial sketch instead of only a topology count and a visual screenshot.

### Broad envelope

The map confirms a long, wide part in XY with a smaller Z extent: X spans 800 mm, Y spans 450 mm, and Z spans 150 mm. The top/bottom views have about 49.2% hit coverage because much of the rectangular bounding box is outside the part silhouette. Side/front/back views have higher hit coverage where the silhouette fills more of the projected bounds.

### Likely thickness/build direction

The top view casts on `xy` in `-z`; the bottom view casts on `xy` in `+z`. Dominant top bands include z `0`, z `-50`, z `-100`, z `-60`, and a small z `50` central component. The central top probe at approximately `(11.954, 1.0345)` hit z `50` first and z `-50` last, while the corresponding bottom dominant plateau probe hit z `-50` first and z `50` last. This suggests the main coordinate frame is XY with meaningful vertical plate/build variation along Z; the local central raised candidate reaches z `50`, while broad plate regions include lower plateaus.

### Top/bottom/front/side views

- **Top:** shows a dominant plane plateau around z `0`, lower/lowered bands around z `-50`, z `-60`, and z `-100`, plus a small central z `50` component. Cylindrical hits appear around rounded lobes and cut features.
- **Bottom:** mirrors the same XY silhouette, but dominant first-hit bands differ because rays enter from below; broad bottom evidence centers around z `-50`, with other lower/side features at z `-100`, z `-60`, z `0`, and z `-70`.
- **Right/left:** side maps show strong outer bands at x `400` and x `-400`, paired no-hit silhouette gaps near upper side regions, and large cylindrical components along rounded end/tab outlines.
- **Front/back:** front/back maps show substantial no-hit silhouette regions, plane-dominated central components, many cylindrical side/edge components, and a small cone count visible in the back view.

### Interior no-hit components

At 16x16, the largest no-hit components are classified as border-touching silhouette/exterior gaps rather than interior holes. That is useful but incomplete: the output did not give a ranked interior through-hole candidate list in the first few suggestions. Circular holes and rounded slots may be represented indirectly by hit-band discontinuities and cylindrical clusters, but the coarse grid does not yet turn them into explicit “hole candidate” objects.

### Border-touching silhouette gaps

Top/bottom report multiple border-touching no-hit components around the outer silhouette, including large gaps above and below the central web and side gaps near the left/right extents. The top probe `top.nohit.1.center` returned no hit, confirming that at least one large suggested component is exterior/silhouette rather than an internal through-opening. The right-side probe `right.nohit.0.center` likewise returned no hit.

### Cylindrical clusters

Cylinders are now first-class in both baseline and map evidence. The base topology reports 57 cylindrical faces. The six-view map reports cylindrical first hits in every view: 20 top, 20 bottom, 50 right, 72 left, 62 back, and 63 front samples. Suggested cylindrical probes found exact analytic cylinder hits, e.g. `top.surface.8.center` on face 66 and `right.surface.0.center` with 16 ray hits through many cylindrical crossings. This strengthens the old rounded lobes/tabs and holes/slots hypothesis, while still not grouping which cylinders belong to which semantic feature.

### Conical clusters

The base topology reports 4 cones. The 16x16 six-view map only surfaced cone hits in the back view summary, with 2 cone samples. That is evidence that conical/countersink-like geometry is present but under-sampled and not yet legible as a modeled feature from the LLM summary alone.

### Central raised or lowered regions

The top view has a small central height-band component at z `50`, with centroid `(0, 0)` in the 16x16 component list. A selected top central plateau probe hit a plane at z `50` first and a plane at z `-50` last. This strengthens the old “central raised boss” candidate, conservatively: the map proves a raised central plane is present along that ray, but it does not prove the exact boss outline, feature history, or whether it is an additive boss versus a remaining island after cuts.

### Symmetry/repetition

The maps suggest approximate bilateral repetition across X and Y: left/right side views are similar but not identical, top and bottom share the same footprint, and paired cylindrical/silhouette components appear at comparable positive and negative coordinates. This strengthens a symmetry/repetition interpretation but does not prove exact mirror operations.

### Dominant plateaus

The top dominant non-empty plateau is z `0` with 71 samples; the bottom dominant non-empty plateau is z `-50` with 70 samples. Side maps have strong bands at outer X positions (`400` and `-400`), while front/back have strong bands at Y positions (`125`, `175`, `225`, `-125`, `-175`, `-225`). For an LLM, these plateaus are very useful anchors: they provide a coordinate scaffold for “web,” “side/end,” and “raised/lowered” language.

### Fallback/provenance ratios

All six view summaries reported fallback ratio `0`. The backend counts show analytic and unsupported/no-hit samples, but no tessellated fallback samples. That is a major dogfood win: for this AP242 file, the map evidence is analytic-first rather than a purely mesh-derived approximation.

## 4. Comparison with old CTC-01 semantic candidate

| Old hypothesis | New map evidence | Status |
|---|---|---|
| Main plate/web | Bounding box is wide/long in XY and shallow in Z; top/bottom dominant plane plateaus and side views support a prismatic web/plate interpretation. | strengthened |
| Rounded lobes/tabs | Baseline reports 57 cylinders; top/bottom/side/front/back map summaries all hit cylinder families, and selected cylindrical probes returned exact cylinder hits. | strengthened |
| Upper/lower holes | Coarse 16x16 suggested probes emphasized border-touching silhouette gaps rather than interior through-hole candidates; cylinders and discontinuities are consistent with holes, but the map did not explicitly group them. | unchanged to mildly strengthened |
| Left/right slots | Top footprint and height-band discontinuities are consistent with slot/cutout regions, but no explicit slot grouping exists. | unchanged |
| Central boss | Small top height-band component at z `50` centered near `(0, 0)` and point probe through central raised region strengthen the raised-region hypothesis. | strengthened |
| Pockets/notches | Multiple lower bands and side silhouette gaps suggest stepped notches/pockets, but component labels are not specific enough to separate pockets from exterior cutouts. | mildly strengthened but ambiguous |
| Edge finish | Many cylinder hits along side/front/back views are consistent with rounded edge finish or rounded outline features, but the map does not identify fillet chains or late feature ordering. | unchanged |
| Symmetry/repetition | Paired components and similar left/right/top/bottom structures suggest repetition/mirroring, but exact symmetry is not detected or scored. | strengthened but not proven |

## 5. Suggested probes and what they revealed

The selected point-probe artifact is `docs/reports/artifacts/analyze-map-ctc01/ctc01-selected-point-probes.json`. I intentionally kept the probe count bounded; only seven selected probe IDs from the generated suggestions were present and run.

### Probe `top.nohit.1.center`

- **Command:** `dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --plane xy --direction -z --point -1.4035,-184.7368 --json`
- **View/point:** top, `[-1.4035, -184.7368]`.
- **Reason:** border-touching silhouette gap.
- **Key hits:** no hit; hit count 0.
- **Helped:** yes. It confirmed this suggested no-hit component is exterior/silhouette-like rather than an internal through-opening.

### Probe `top.surface.8.center`

- **Command:** `dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --plane xy --direction -z --point -346.6667,-105.0000 --json`
- **View/point:** top, `[-346.6667, -105]`.
- **Reason:** cylindrical hit component.
- **Key hits:** one exact analytic cylinder hit on face 66 at approximately z `-60`.
- **Helped:** yes. It confirmed a rounded/curved feature at a lobe/end-like coordinate.

### Probe `top.surface.10.center`

- **Command:** `dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --plane xy --direction -z --point 346.6667,-105.0000 --json`
- **View/point:** top, `[346.6667, -105]`.
- **Reason:** cylindrical hit component.
- **Key hits:** two exact analytic cylinder hits on face 69 near z `-60`.
- **Helped:** yes. It showed a corresponding curved feature on the opposite side, consistent with repeated/paired rounded geometry.

### Probe `top.band.13.center`

- **Command:** `dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --plane xy --direction -z --point 11.9540,1.0345 --json`
- **View/point:** top, `[11.954, 1.0345]`.
- **Reason:** representative point on dominant height plateau.
- **Key hits:** three hits; first is plane face 105 at z `50`, last is plane face 3 at z `-50`.
- **Helped:** yes. It confirmed a central raised vertical stack through the candidate boss/raised region.

### Probe `bottom.band.15.center`

- **Command:** `dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --plane xy --direction +z --point 5.4902,4.4118 --json`
- **View/point:** bottom, `[5.4902, 4.4118]`.
- **Reason:** representative point on dominant height plateau.
- **Key hits:** three hits; first is plane face 3 at z `-50`, last is plane face 105 at z `50`.
- **Helped:** yes. It cross-checked the same central stack from the bottom direction.

### Probe `right.nohit.0.center`

- **Command:** `dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --plane yz --direction -x --point -150.0000,30.0000 --json`
- **View/point:** right, `[-150, 30]` in YZ.
- **Reason:** border-touching silhouette gap.
- **Key hits:** no hit; hit count 0.
- **Helped:** moderately. It confirmed a side-view exterior/silhouette void, but not a functional hole.

### Probe `right.surface.0.center`

- **Command:** `dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --plane yz --direction -x --point -191.4706,-50.0000 --json`
- **View/point:** right, `[-191.4706, -50]` in YZ.
- **Reason:** cylindrical hit component.
- **Key hits:** 16 hits; first and last hits are cylinders, crossing from x about `397.209` to x about `-397.209`.
- **Helped:** yes, but also exposed verbosity. It strongly indicates this ray crosses many rounded/cylindrical features, but the raw point output is too detailed for quick semantic grouping.

## 6. What remains hard for Codex/LLMs

- **Component ranking is not semantic enough.** The first suggestions favored large border-touching no-hit components, which are useful for silhouette validation but less useful than interior holes/slots for decompilation.
- **No cross-view correlation.** A top cylindrical component, side cylindrical component, and front/back cylindrical component are not grouped into one candidate feature.
- **No explicit hole/slot candidate grouping.** The map shows no-hits, bands, and cylinders, but does not say “rounded slot candidate at approximately X/Y with length/orientation.”
- **Components lack enough face/topology context.** Point probes expose face IDs, but components themselves do not summarize representative face IDs or stable face-family clusters.
- **Point probes are verbose.** Useful evidence is present, but an LLM needs a compact point-probe summary with first/last hit, hit count, surface family sequence, and major face IDs.
- **32x32 was too slow for routine dogfood.** The 16x16 run took 3m33s. The 32x32 attempt was still active after several more minutes and was stopped. Adaptive refinement around candidate components would be more useful than blindly increasing the whole six-view grid.
- **Cones are under-legible.** The baseline says 4 cones, and back-view map saw 2 cone samples, but there is no conical feature grouping.
- **View axes require care.** Side-view point coordinates are in the view plane (`yz`/`xz`), not global XY; LLM summaries should restate axes to avoid coordinate mistakes.
- **No section integration.** For prismatic decompilation, `analyze section` loops could help distinguish actual through-holes/slots from silhouette gaps, but that evidence is not integrated here.

## 7. Recommendations

### Must-have next

1. **Cross-view component correlation and face/surface-family grids.** This would let Codex connect top/front/side cylinders and bands into stable candidate holes, slots, bosses, and lobes.
2. **Suggested probe ranking and point-probe summarization.** Rank interior no-hit/cylindrical/high-discontinuity candidates above huge exterior silhouette gaps, and emit compact first/last/family-sequence summaries.
3. **Adaptive refinement around suggested components.** CTC-01 does not need global 32x32 everywhere; it needs denser sampling near slots, holes, cones, and boss boundaries.

### Useful soon

1. **Slot/hole candidate grouping.** Group elongated no-hit/cylindrical/band discontinuity patterns into candidate rounded slots and circular holes.
2. **Symmetry detection.** Score approximate mirror/repetition across X/Y and list paired components.
3. **Connected cylindrical clusters across views.** Turn many cylinder samples into compact clusters with representative face IDs and axis/direction hints.

### Later

1. **Integrate `analyze section` evidence with `analyze map`.** Section loops would likely improve prismatic decomposition.
2. **Conical feature summarization.** The four cone faces should become explicit countersink/chamfer/cone candidates when sampled.
3. **Feature-history hypothesis scoring.** Once grouping improves, JudgmentEngine-style candidate scoring could compare alternative decompilation strategies explicitly.

## 8. Updated CTC-01 modeling strategy

```text
Coordinate frame:
  Use imported global axes. Treat XY as the primary plan/profile frame and Z as the plate/build-height direction. Bounds are X -400..400, Y -225..225, Z -100..50 mm.

Gross blockout:
  Start from a broad prismatic web/plate footprint in XY with long X span, moderate Y span, and stepped Z plateaus. The map supports multiple planar levels rather than a single uniform slab.

Major additions:
  Candidate central raised region/boss near XY origin, reaching z 50 in the selected central top/bottom probes. Exact polygonal/rounded outline is not proven by the 16x16 map alone.

Major removals:
  Exterior side/end silhouette gaps and internal cut features are required. Treat large border-touching no-hit components as outer-profile cutouts/notches, not through-holes by default.

Functional holes:
  Circular holes remain plausible because of numerous cylinders and prior visual/semantic evidence, but 16x16 map suggestions did not explicitly isolate them. Use future refined map/section probes before modeling exact hole count and positions.

Slots/pockets:
  Rounded slots and pockets are consistent with cylindrical clusters and height discontinuities, but need grouped evidence. Do not commit final slot syntax from this report alone.

Repetition/symmetry:
  Use approximate left/right and upper/lower repetition as a working hypothesis. The map strengthens repetition but does not prove exact mirror-pattern feature history.

Edge finish:
  Many cylindrical faces are consistent with rounded outline/edge finish and rounded cuts. Fillet/round ordering remains unresolved.

Ambiguities:
  Interior holes vs coarse-grid misses, exact boss outline, cone/countersink locations, pocket depths, and whether repeated features came from mirror/pattern operations.

Missing Firmament/AIR capabilities:
  Profile/sketch line-arc chains, arbitrary prismatic profile extrusion, rounded-rectangle/slot cut syntax, robust through-cuts, fillet/round chains, mirror/pattern semantics, feature references, and stable face/loop naming remain relevant.

Recommended next modeling primitive:
  A profile-sketch/extrude primitive with line/arc outline support and named subtractive circular/slot cuts. Do not generate final Firmament source until map/section evidence can group holes and slots reliably.
```

## 9. Proposed next milestone

**`ANALYZE-MAP-A6 — cross-view component correlation and face/surface-family grids`**

This is the best next milestone for CTC-01. The blocker exposed by this dogfood pass is no longer “can rays hit this model?” The blocker is that the LLM receives uncorrelated components. Cross-view correlation plus component-level face/surface-family grids would make the current measured evidence directly usable for semantic decompilation: holes, rounded slots, lobes, boss boundaries, conical features, and symmetry pairs could become testable candidates instead of prose guesses.
