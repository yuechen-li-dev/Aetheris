# ANALYZE-MAP-DOGFOOD-FTC09-A0 — FTC-09 LLM utility report

## 1. Executive summary

- **FTC-09 file used:** `testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp`.
- **Representative artifacts saved:**
  - `docs/reports/artifacts/analyze-map-ftc09/ftc09-six-view-16x16.json` (~66 KiB)
  - `docs/reports/artifacts/analyze-map-ftc09/ftc09-six-view-32x32.json` (~74 KiB)
  - `docs/reports/artifacts/analyze-map-ftc09/ftc09-point-probes.json` (~25 KiB)
- **Commands run for dogfooding:**

```bash
dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp --views six --resolution 16x16 --llm --json

dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp --views six --resolution 32x32 --llm --json

dotnet run --project Aetheris.CLI -- analyze map testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp --plane xy --direction -z --resolution 16x16 --point 0,0 --json
```

- **Result:** import and map generation succeeded for 16x16, 32x32, and the point probe.
- **Six-view usefulness:** useful as a coarse measured orientation/thickness/provenance sketch, but not yet sufficient to let an LLM confidently recover FTC-09-style holes, slots, and cutouts from the compact summaries alone.

**Verdict:** `analyze map` is already valuable as a non-vision measurement primitive: it gives bounded coordinates, six named orthographic views, hit coverage, height bands, surface-family counts, and analytic-vs-fallback provenance. On FTC-09, however, the current compact six-view summary communicates the broad envelope and dominant planar/cylindrical intersections much better than it communicates feature topology. The next improvement should make holes, slots, cutouts, and other discontinuities explicit as connected components plus suggested follow-up probes.

## 2. What Codex could infer from the map

These observations are intentionally conservative and limited to what the map output supports.

- **Plate-like / thin-web behavior is visible from the side views.** The `back` and `front` views use the `xz` plane and report hit coverage of about 52% at 16x16 and about 54% at 32x32, with many `.` no-hit cells around an irregular occupied region. That suggests a comparatively thin or plate-like model when viewed along `+y` / `-y`, rather than a fully solid rectangular block.
- **Dominant broad planar surfaces are clear.** Top and bottom views have 100% hit coverage in the sampled `xy` domain and are dominated by one rounded first-hit band covering 62.5% at 16x16. The side views are also mostly plane hits. This makes the part feel like a bounded mechanical plate or bracket with large flat faces.
- **Cylindrical intersections are present.** The top and bottom summaries report `cylinder` samples alongside `plane` samples. At 16x16, each top/bottom view reports 32 cylindrical first-hit samples; at 32x32, each reports 128. This suggests circular or rounded feature families are being intersected, but the compact grid does not identify them as specific holes, bosses, or blends.
- **Side silhouette is irregular.** The front/back compact grids contain scattered no-hit cells and nonzero height-band islands near the margins and interior. This suggests cutouts, notches, or non-rectangular outline changes, but not enough to reconstruct exact boundaries.
- **There are repeated band patterns that suggest long, uniform extents.** Top/bottom compact grids repeat identical rows at 16x16 and nearly identical rows at 32x32. That implies the first-hit height pattern is mostly invariant along one sampled axis in those views. This may be a coordinate-orientation consequence of FTC-09's plate layout, or it may expose that this view is not the best one for seeing holes/slots.
- **Point probing gives useful exact ray detail.** The `xy/-z` point probe at `0,0` returned bounds, first/last hits, face indices, surface families, normals, and a multi-hit list. That is much richer than the compact summary and confirms that exact follow-up probes can expose interior ray intervals and surface families.
- **Analytic provenance is strong.** All six-view samples reported zero tessellated fallback. That gives an LLM more confidence that reported first-hit heights and surface-family counts are not approximate tessellation artifacts.

## 3. What the map made hard to understand

- **Holes versus exterior silhouette gaps are not explicit.** The compact grids use `.` for no-hit cells, but they do not group no-hit regions. An LLM cannot tell whether a no-hit island is an internal through-hole, a slot, a window, or exterior empty space without manually reasoning from row strings.
- **Top/bottom maps hide much of the expected FTC-09 feature topology.** The top and bottom views show full hit coverage and no no-hit cells, so through-features are not obvious from those views even though cylindrical intersections appear in the surface-family counts. The summaries therefore say “cylinder exists” but not “there is a candidate hole here.”
- **Slots and elongated holes are not legible at low resolution.** At 16x16, front/back views show a noisy silhouette-like pattern, but it is too coarse to distinguish elongated slots from separated holes, edge cutouts, or small islands.
- **The 32x32 output is better but still not self-interpreting.** More rows and columns expose additional bands, but the LLM still has to parse ASCII topology itself. There is no component list, bounding box, centroid, or confidence for candidate holes/slots.
- **View naming requires careful reading.** The six-view convention is documented in help and the JSON includes `plane`/`direction`, but `top xy/-z` on this model produced bounds that read as `x` by a very small `y` extent with large `z` values. Without an explicit axis/bounds summary per compact grid in the LLM text, it is easy to misread which physical dimension is thickness.
- **Fallback and unsupported counts are useful but hard to localize.** `unsupported` counts appear in front/back backend counts, while fallback remains zero. The compact legend reserves `?` for unsupported/unknown first hits, but the observed front/back grids mostly showed `.` and numeric bands. The summary does not say where unsupported candidates occurred or whether they affected final hit/no-hit decisions.
- **No face IDs in compact summaries.** The point probe includes face indices, but the six-view compact grids do not expose face IDs, face-id bands, or surface-family grids. This blocks correlation between “same feature seen from multiple views” and “same CAD face.”
- **No automatic next probes.** The tool did not suggest points near high-gradient cells, no-hit boundaries, cylindrical hits, or ambiguous islands. An LLM has to invent probe coordinates from bounds and row/column indexing.
- **No adaptive sampling around boundaries.** Uniform 16x16 and 32x32 sampling spends many samples on large planar areas while under-sampling holes/edges, which are precisely the regions a recovery workflow needs.

## 4. JSON usability for LLMs

- **Output size:** manageable. The saved six-view JSON files were about 66 KiB at 16x16 and 74 KiB at 32x32. This is small enough to inspect or summarize in a development loop, although the point-probe JSON can become verbose because it includes full hit intervals.
- **Field names:** generally clear. `mode`, `mapVersion`, `resolution`, `views`, `plane`, `direction`, `summary`, `compactGrid`, `dominantBands`, `surfaceFamiliesHit`, `backendCounts`, and `fallbackRatio` are understandable.
- **Compact grids:** readable as raw data, but not enough as semantic data. The ASCII rows are compact and token-efficient, yet the legend only maps characters to height-band rank, not to actual height values, surface families, face IDs, or connected features.
- **Dominant bands:** useful for identifying large planar plateaus and repeated heights. They are less useful for holes/slots because no-hit topology is not banded into components and because small features disappear into low-coverage bands.
- **Backend counts/provenance:** very useful. Seeing analytic hits dominate and fallback remain zero materially improves confidence in the measurements. The output would be better if backend counts were separated into attempted intersections versus selected first-hit cells, because the counts can exceed sample count and may confuse readers.
- **Diagnostics:** sparse but acceptable for a successful run. The main gap is not error reporting; it is LLM guidance. The JSON does not yet tell the consumer what to inspect next.
- **Would an LLM know what to do next?** Only partially. A careful LLM can request point probes and compare views, but the output does not provide a ready-made list of “interesting coordinates,” “candidate holes,” “candidate slots,” or “ambiguous boundaries.”

Recommended schema improvements:

- Add per-view `bounds` to the six-view summary, not only point mode.
- Add a `bandLegend` mapping compact-grid characters to rounded values and sample counts.
- Add connected-component summaries for no-hit cells and same-height plateaus.
- Add optional `surfaceFamilyGrid` and `faceIdGrid` compact encodings.
- Add `suggestedProbes` with `(plane, direction, u, v, reason)` entries.
- Clarify backend counts as `intersectionBackendCounts` versus `selectedFirstHitBackendCounts` if they measure different things.

## 5. Suggested next features

### Must-have next

1. **Connected no-hit islands / silhouette components.** Report component count, bounding box, centroid, approximate area, touches-border flag, and view. This directly addresses the hole-vs-exterior ambiguity.
2. **Automatic interesting-point suggestions.** Emit point probes around component centers, boundaries, high-gradient cells, cylindrical-hit cells, and low-confidence/unsupported regions. Include exact command fragments.
3. **Connected same-height plateau regions.** Large planes, recesses, raised bands, and pockets become much more legible if numeric bands are grouped into regions rather than only rendered as ASCII strings.

### Nice soon

4. **Surface-family grid.** A compact plane/cylinder/cone/sphere/torus/fallback/no-hit grid would let an LLM locate cylindrical and analytic fallback regions, not just count them.
5. **Face-id heatmap / face-id grid.** Stable face IDs would let an LLM correlate features across views and point probes.
6. **Adaptive refinement around high-gradient / boundary cells.** Spend resolution where topology changes, especially around no-hit islands and height discontinuities.
7. **Ray interval / multi-hit summaries.** For through-holes, pockets, and internal features, first hit alone is insufficient. Summaries of first/last hit and interval counts by region would help.

### Later

8. **View-pair correlation.** Connect top/front/side evidence into candidate 3D features.
9. **Hole/slot candidate hints from map topology.** Once components and refinements exist, promote likely holes and slots with conservative confidence.
10. **Compact grid compression.** Repeated rows are common and could be represented as run-length blocks for token savings.
11. **Side-view thickness summaries.** Explicit min/max/median thickness and occupied-span summaries would reduce coordinate-orientation confusion.

## 6. Performance / reliability notes

- **16x16 runtime:** succeeded in about 124 seconds in this environment.
- **32x32 runtime:** succeeded in about 303 seconds in this environment.
- **Point probe runtime:** succeeded; the observed command completed after the main assemblies were already built and produced detailed ray-hit output.
- **Acceptability:** 16x16 is acceptable for dogfooding but slow for tight interactive LLM loops. 32x32 is usable as an offline report artifact but too slow to run repeatedly without caching or targeted/adaptive refinement.
- **Fallback:** six-view FTC-09 runs reported `tessellated-fallback: 0` and `fallbackRatio: 0` in all views.
- **Analytic hits:** analytic intersections dominated all reported views. Top/bottom and side views included plane and cylinder surface-family counts; front/back selected hits were plane-only in the compact summaries.
- **Crashes/timeouts:** no crashes or timeouts were observed for the required 16x16 or 32x32 commands.
- **Output size:** manageable for saved artifacts and future report review.

## 7. Proposed next milestone

**Recommended next milestone:** `ANALYZE-MAP-A5 — connected components and interesting point suggestions`.

FTC-09 shows that the current map already measures the model successfully, but it leaves too much topology interpretation to the LLM. Connected components and suggested point probes are the shortest path from “ASCII measurement grid” to “actionable CAD reasoning loop”: they would identify candidate holes/slots/cutouts conservatively, distinguish border-touching silhouette gaps from interior no-hit islands, and give Codex exact next commands to inspect ambiguous regions without requiring image vision or major feature reconstruction.
