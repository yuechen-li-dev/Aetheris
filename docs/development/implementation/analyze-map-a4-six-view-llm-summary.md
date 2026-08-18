# ANALYZE-MAP-A4 Six-view LLM Summary

ANALYZE-MAP-A4 adds a compact six-view summary mode to `aetheris analyze map` for LLM-oriented spatial inspection. The mode keeps detailed single-view ray sample JSON intact, but adds a smaller measured representation that can be scanned without machine vision.

## Purpose

Dense ray samples are useful for tools. Six-view summaries are useful when an LLM needs quick evidence for silhouette, empty rays, height/depth plateaus, curved versus planar hit families, and exact-versus-approximate backend provenance.

The output reports measured ray-map patterns only. It does not reconstruct or name CAD features.

## CLI

```bash
aetheris analyze map part.step --views six --resolution 32x32 --llm --json
```

`--summary` is accepted as an alias for `--llm` in this route:

```bash
aetheris analyze map part.step --views six --resolution 32x32 --summary --json
```

Existing detailed modes remain supported:

```bash
aetheris analyze map part.step --plane xy --direction -z --resolution 8x8 --json
aetheris analyze map part.step --plane xy --point 0,0 --json
aetheris analyze map part.step --top --rows 8 --cols 8 --json
```

## Six-view conventions

The six-view route uses the same analytic-first ray backend as explicit `--plane/--direction` map probes:

| View | Plane | Direction |
| --- | --- | --- |
| `top` | `xy` | `-z` |
| `bottom` | `xy` | `+z` |
| `right` | `yz` | `-x` |
| `left` | `yz` | `+x` |
| `back` | `xz` | `+y` |
| `front` | `xz` | `-y` |

These names match the legacy view flag conventions used by `--top`, `--bottom`, `--front`, `--back`, `--left`, and `--right`.

## Output shape

The top-level JSON uses:

- `mode: "six-view-summary"`;
- `mapVersion: "analyze-map-v1"`;
- `resolution: [cols, rows]`;
- `views`: one entry for each of the six named views;
- `diagnostics`: de-duplicated view-prefixed diagnostics from the underlying map probes.

Each view includes:

- `name`, `plane`, and `direction`;
- `summary`;
- `compactGrid` when resolution is at most `64x64`;
- `measuredSummary`, a short non-feature textual summary.

## Summary fields

Each view summary reports:

- `sampleCount` and `hitCount`;
- `hitCoverage`;
- `heightRange`, using the underlying first-hit scalar range from the map backend;
- `dominantBands`, produced by rounding first-hit axis values to four decimals and counting the most common values;
- `surfaceFamiliesHit`, such as `plane`, `cylinder`, `sphere`, `cone`, `torus`, or fallback families like `linear-extrusion`;
- `backendCounts` for `analytic`, `cir-evaluated`, `tessellated-fallback`, and `unsupported`;
- `fallbackRatio`, computed from tessellated fallback hit intersections divided by hit intersections.

A no-hit dominant band is represented with `value: null` and `meaning: "no-hit"`.

## Compact grid

For resolutions up to `64x64`, each view includes a deterministic ASCII grid:

| Symbol | Meaning |
| --- | --- |
| `.` | no hit |
| `0` | most common rounded first-hit axis value |
| `1` | next most common rounded first-hit axis value |
| `2-9` | additional rounded first-hit axis value bands |
| `~` | tessellated-fallback or approximate first hit |
| `?` | unsupported or unknown first hit |

The grid is intended to show measured silhouette, gaps, and plateaus compactly. It is not an image rendering and does not use machine vision.

## Backend provenance

A4 preserves the A1/A2/A3 provenance model. Analytic plane, cylinder, sphere, cone, and torus hits remain exact where supported. Unsupported analytic families are surfaced through diagnostics and tessellated fallback counts rather than hidden behind the summary.

## Examples

### Box

A box sampled with `--views six --resolution 8x8 --llm --json` should report six views, full hit coverage for all views, dominant planar bands, analytic backend counts, and compact grids filled by a single dominant band symbol.

### Box with cylindrical through-hole

A box with a through-hole should report measured no-hit rays and/or `cylinder` surface-family hits where the ray sampling crosses the opening. The summary should stay measured: it may indicate empty rays or cylinder hits, but should not assert a named feature unless a future feature recognizer provides that evidence.

### Torus

A simple torus should report `torus` in ring samples, analytic backend hits, no tessellated fallback for supported torus intersections, and compact-grid no-hit symbols where rays pass through empty center/open space according to the selected view.

### RULED-A2 fallback

For `testdata/step242/generated/ruled-a2/ellipse-linear-extrusion-production.step`, six-view summary succeeds and reports fallback evidence such as `linear-extrusion` diagnostics, `tessellated-fallback` backend counts, `~` compact-grid symbols where fallback supplies first hits, and non-zero fallback ratios for affected views.

## Limitations

- Dominant bands use simple four-decimal rounding, not advanced clustering.
- Compact grids are omitted above `64x64` to keep output compact.
- The route summarizes first-hit measurements; it does not replace detailed single-view sample JSON.
- The route does not perform feature reconstruction, connected-component naming, rendering, or machine vision.
- `analyze volume` policy is unchanged.


Forward link: ANALYZE-MAP-A5 adds connected components and suggested probes; see `docs/development/implementation/analyze-map-a5-components-and-probes.md`.

## Phase closeout

The first LLM-oriented analyze-map phase is summarized in `docs/development/reports/analyze-map-phase-closeout-a0.md`.
