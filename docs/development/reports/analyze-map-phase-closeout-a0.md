# ANALYZE-MAP-PHASE-CLOSEOUT-A0 — first LLM-oriented analyze-map phase closeout

## 1. Executive summary

`aetheris analyze map` is now a CAD sonar / ray-probe tool for LLMs. It samples STEP/BRep geometry from named orthographic views, reports measured hits, depths, and heights, discloses analytic versus fallback provenance, summarizes six orthographic views, finds connected regions, and suggests local probes and section commands for follow-up inspection.

The lane is intentionally measurement-oriented. It is **not** feature reconstruction, **not** machine vision, **not** a replacement for exact volume analysis, and **not** full decompilation of feature history. Its role is to give an LLM compact, grounded spatial facts and safe next questions, not to assert final CAD intent.

This closeout records the state after A0–A6 and before a soft pivot. The phase succeeded in turning `analyze map` from a coarse single-view ray sampler into an LLM-readable spatial cognition primitive with provenance, six-view summaries, components, ranked probes, and bridge suggestions into section analysis.

## 2. Phase timeline

- **ANALYZE-MAP-A0 — generalized ray/height-map probing.** A0 established the basic map lane: cast rays through BRep geometry over a bounded grid or at a point, report first-hit depth/height, and make map inspection available from the CLI. This mattered because it moved the workflow away from screenshots and toward measured geometry queries.

- **ANALYZE-MAP-A1 — analytic-first backend provenance and exact plane hits.** A1 made provenance explicit and preferred analytic intersections where possible, initially for planes. This mattered because LLM consumers need to know whether a reported height is exact analytic evidence or an approximation.

- **ANALYZE-MAP-A2 — exact cylinder/sphere hits.** A2 expanded analytic support to cylinders and spheres. This mattered because many mechanical features surface as cylindrical or spherical intersections, and relying on tessellation for those common families would make downstream reasoning less trustworthy.

- **ANALYZE-MAP-A3 — exact cone/torus hits and torus truth pass.** A3 added cones and tori to the analytic hit set and pressure-tested torus behavior. This mattered because countersink-like/conical evidence and toroidal/round geometry can now be reported as exact supported families rather than approximate mesh artifacts.

- **ANALYZE-MAP-A4 — six-view compact LLM summaries.** A4 introduced the six named orthographic view route, compact ASCII grids, dominant bands, surface-family counts, backend counts, and LLM-oriented measured summaries. This mattered because an LLM can scan a complete spatial sketch without parsing dense ray samples or using image vision.

- **ANALYZE-MAP-A5 — connected components and suggested probes.** A5 grouped no-hit islands, height/depth bands, surface-family regions, and fallback regions, then emitted centroid point-probe commands. This mattered because FTC-09 dogfooding showed that raw compact grids were useful but still forced the LLM to manually decode topology.

- **ANALYZE-MAP-A6 — ranked probes, evidence bundles, and map/section bridge suggestions.** A6 ranked component-derived questions, added evidence-bundle output, summarized point probes compactly, and suggested section commands through component centroids. This mattered because CTC-01 dogfooding showed that global maps are expensive and the right next move is usually a bounded local question rather than a higher-resolution full map.

## 3. Current capabilities

Current `aetheris analyze map` capabilities include:

- single-view ray grids;
- point probes;
- six-view summaries;
- compact ASCII grids for small enough resolutions;
- dominant height/depth bands;
- analytic provenance reporting;
- exact analytic plane, cylinder, sphere, cone, and torus ray hits;
- tessellated fallback diagnostics;
- connected components;
- no-hit island classification into interior-opening candidates versus border-touching silhouette/exterior gaps;
- surface-family components;
- ranked suggested probes;
- section command suggestions;
- evidence bundle output.

These capabilities remain conservative. Component labels and ranked probes are measurement-derived hints and follow-up questions, not declarations that a model contains a named hole, slot, boss, fillet chain, or feature operation.

## 4. Truth/provenance model

The map lane uses a measurement ladder:

1. **Analytic exact.** Supported analytic intersections with known surface families, currently including planes, cylinders, spheres, cones, and tori, are the strongest map evidence.
2. **CIR/evaluable future hook.** The schema leaves room for CIR-evaluated measurements where future internal representations can provide exact or authoritative geometry evaluation.
3. **Tessellated fallback approximate.** Unsupported or complex surfaces can be inspected through bounded tessellated fallback, but the output must mark those measurements as approximate.
4. **Unsupported/unknown.** When a surface family or hit path cannot be evaluated with adequate authority, the output must expose that instead of silently upgrading it to truth.

This matters because the LLM must know what to trust. A compact grid cell produced by an exact cylinder hit and a compact grid cell produced by tessellated fallback are both useful, but they should not carry the same confidence in downstream reasoning.

## 5. Dogfood results

### FTC-09

FTC-09 succeeded at both 16x16 and 32x32 six-view map resolutions. The artifacts provided useful broad envelope, plate-like side-view behavior, dominant planar evidence, cylindrical-hit evidence, and analytic provenance with zero tessellated fallback in the saved six-view runs.

The same run also exposed the central limitation of A4-era summaries: the broad shape and surface families were visible, but feature topology was not grouped. Holes, slots, cutouts, and no-hit regions still had to be inferred manually from compact grids. That friction directly motivated A5 connected components and suggested probes.

### CTC-01

CTC-01 is the most important stress case in this phase. The old map path previously failed on this non-narrow BRep, while the current map path succeeded at 16x16 and produced a compact six-view artifact.

Baseline `aetheris analyze` facts for CTC-01 were: 1 body, 1 shell, 117 faces, 318 edges, 206 vertices, bounds `[-400,-225,-100]` to `[400,225,50]`, and surface families of 56 planes, 57 cylinders, and 4 cones. The 16x16 map strengthened the old semantic candidate of a broad prismatic web/plate with rounded lobes, holes/slots/side gaps, cylindrical families, a central raised region, stepped/lowered plateaus, and approximate bilateral repetition. It did not prove final feature history.

CTC-01 also made performance and locality unavoidable. The 16x16 six-view run took minutes, and 32x32 global mapping was too slow for tight loops in the dogfood window. That motivated A6: rank from existing coarse evidence, emit compact point-probe summaries, suggest sections, and prefer local follow-up over brute-force global resolution. Future work should add cross-view correlation, cached ranking, and local bounds.

### RULED-A2 artifact

The RULED-A2 linear-extrusion artifact demonstrated that maps can inspect ruled/swept artifacts while being honest about fallback. Six-view summaries can expose explicit `linear-extrusion` fallback diagnostics, fallback backend counts, approximate compact-grid cells, and non-zero fallback ratios.

This proves ruled and swept artifacts are inspectable through the map lane today, but exact linear-extrusion and surface-of-revolution map intersections remain future work. The important phase result is not that these surfaces became exact; it is that the tool surfaces where exactness is absent.

## 6. Performance and locality

Global maps are expensive. On large CTC-01-class STEP files, a 16x16 six-view map can take minutes, and 32x32 can be too slow for routine interactive loops. Raising global resolution is therefore the wrong default response to uncertainty.

The future direction should be locality-first:

- cache and reuse map artifacts;
- rank questions from existing artifacts before running new probes;
- support local window probes and bounded local maps;
- use adaptive refinement around hit/no-hit boundaries, curved clusters, fallback regions, and high-uncertainty components;
- avoid brute-force global resolution unless producing an offline report artifact.

This is especially important for LLM workflows: the tool should spend compute where the next reasoning step needs evidence, not uniformly across already-understood planar regions.

## 7. Known limitations

Known limitations at this closeout are:

- no analytic linear-extrusion or surface-of-revolution map hits yet;
- B-splines still require fallback or remain unsupported/unknown depending on the path;
- no cross-view component correlation yet;
- no face-id or surface-family compact grid beyond component summaries and counts;
- no automatic feature reconstruction;
- point-probe output can still be verbose despite the compact `pointSummary` improvement;
- section integration is currently suggested/bridged, not deeply fused with map components;
- exact volume remains separate from map and is not replaced by ray sampling;
- component classification remains resolution-dependent and conservative;
- local map bounds are recommended by A6 but not yet executed as a supported map command.

## 8. Recommended next milestones

Recommended future milestones, in suggested order:

- **ANALYZE-MAP-A7 — rank from existing map artifacts and cache/reuse six-view maps.** Make the common workflow cheaper by ranking questions from saved artifacts and avoiding repeated global remaps.

- **ANALYZE-MAP-A8 — local bounds / local map refinement.** Add explicit bounded local maps so high-ranked components can be sampled at higher density without paying for a full global 32x32 or larger six-view pass.

- **ANALYZE-MAP-A9 — face-id and surface-family compact grids.** Add compact grids or summaries that localize face IDs and surface families, making cross-view and point-probe correlation easier.

- **ANALYZE-MAP-A10 — cross-view component correlation.** Relate top/bottom/side/front/back components into conservative 3D candidate regions without claiming full feature reconstruction.

- **ANALYZE-SECTION-MAP-X1 — integrate section loops with map components.** Turn A6 section suggestions into a deeper workflow where section loop evidence can confirm or reject map-derived hypotheses.

- **ANALYZE-MAP-RULED-X1 — analytic linear-extrusion / surface-of-revolution ray hits.** Promote common ruled/swept fallback paths into exact analytic or evaluable intersections where possible.

Do not start these as part of this closeout. The purpose here is to freeze the phase state and preserve the lessons.

## 9. What this enables

The analyze-map lane matters because it gives LLMs measured spatial facts and next-probe commands, so they do not waste intelligence manually parsing screenshots or raw STEP metadata. It provides a bridge between low-level CAD geometry and high-level reasoning: exact where possible, approximate where necessary, and explicit about the difference.

In broader Aetheris terms, this supports a measured, evidence-first workflow. An LLM can ask bounded questions, inspect the returned facts, decide whether a hypothesis is supported, and request the next local measurement without pretending that a visual impression or topology count is enough.

## 10. Validation

This closeout is documentation-only. No new analyze-map features were added.

Validation commands for this closeout:

```bash
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj -f net10.0 --filter "AnalyzeMap|SixView|Components|Probes|EvidenceBundle"
dotnet run --project Aetheris.CLI -- --help
git diff --check
git status --short
```

## Related reports

- Backward: `docs/development/implementation/analyze-map-a4-six-view-llm-summary.md`
- Backward: `docs/development/implementation/analyze-map-a5-components-and-probes.md`
- Backward: `docs/development/implementation/analyze-map-a6-ranked-probes-and-section-bridge.md`
- Dogfood: `docs/development/reports/analyze-map-dogfood-ftc09-a0.md`
- Dogfood: `docs/development/reports/analyze-map-dogfood-ctc01-a0.md`
