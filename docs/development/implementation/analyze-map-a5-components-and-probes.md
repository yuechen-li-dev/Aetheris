# ANALYZE-MAP-A5: connected components and suggested probes

ANALYZE-MAP-A5 extends `aetheris analyze map --views six --llm --json` with conservative connected-component summaries and ready-to-copy follow-up point probes. The goal is to make the six-view map more directly useful to an LLM without asking it to infer topology manually from ASCII grids.

## Purpose

The A4 six-view summary already reports envelope, hit coverage, dominant height/depth bands, surface-family counts, analytic-vs-fallback provenance, and compact grids. Dogfooding on FTC-09 showed that the next useful observation layer is not higher default resolution, but measured regions:

- connected no-hit islands;
- connected same-height/depth bands;
- connected first-hit surface-family clusters;
- connected tessellated fallback/approximate regions;
- concrete point probes derived from those regions.

These are measurement-derived hints. They are not semantic feature reconstruction, and A5 does not claim that a region is a hole, slot, boss, or cutout as fact.

## Connectivity rule

A5 uses **4-connected** grid components. Cells connect only through horizontal or vertical neighbors, not diagonals. This is intentionally conservative for topology: diagonal contact is not treated as a single opening or plateau unless future adaptive refinement proves continuity.

## Output shape

Each six-view entry may include bounded component arrays and per-view suggested probes:

```json
{
  "name": "top",
  "plane": "xy",
  "direction": "-z",
  "components": {
    "noHit": [
      {
        "componentId": "top.nohit.0",
        "kind": "no-hit",
        "view": "top",
        "cellCount": 4,
        "coverage": 0.015625,
        "touchesBorder": false,
        "bboxCells": { "minI": 7, "minJ": 7, "maxI": 8, "maxJ": 8 },
        "centroidCell": [7.5, 7.5],
        "centroidUv": [0.0, 0.0],
        "classificationHint": "interior-opening-candidate",
        "confidence": "medium"
      }
    ],
    "heightBands": [],
    "surfaceFamilies": [],
    "fallback": [],
    "truncated": false,
    "omittedCount": 0
  },
  "suggestedProbes": [
    {
      "probeId": "top.nohit.0.center",
      "view": "top",
      "plane": "xy",
      "direction": "-z",
      "point": [0.0, 0.0],
      "reason": "Center of interior no-hit component; probe to distinguish through-opening, recess, or missing hit.",
      "command": "aetheris analyze map part.step --plane xy --direction -z --point 0,0 --json",
      "sourceComponentId": "top.nohit.0"
    }
  ]
}
```

The top-level `suggestedProbes` array contains the first bounded probes across all views for convenient global consumption.

## Component types

### No-hit components

No-hit cells are grouped where a ray produced no first hit. Each component reports cell count, coverage, border contact, cell bbox, centroid cell, centroid UV coordinate, and a conservative classification hint:

- `interior-opening-candidate` when the component does not touch the sampled view border;
- `silhouette-or-exterior-gap` when it touches the border.

This distinction is the main A5 value: interior no-hit islands are places to inspect, while border-touching no-hit regions are usually silhouette/exterior gaps or edge cutouts.

### Height/depth band components

Hit cells are grouped by the same rounded first-hit scalar used by the compact grid. These components identify dominant planar plateaus and isolated raised/lowered bands. Tiny one-cell noise is filtered unless it covers enough of the view to remain significant.

### Surface-family components

Hit cells are grouped by first-hit surface family, including `plane`, `cylinder`, `cone`, `sphere`, `torus`, `linear-extrusion`, `surface-of-revolution`, `b-spline`, and `unknown` where present. Curved-family components are especially useful when holes or rounds are visible as cylindrical/conical/torus hit clusters rather than no-hit islands.

### Fallback components

Cells whose first hit used tessellated fallback are grouped separately. These regions tell downstream agents where measurements are approximate and where future analytic support or targeted probes may be warranted.

## Suggested probe strategy

A5 generates bounded point probes from component centroids:

- no-hit components: center probe, distinguishing interior candidates from border-touching silhouette gaps;
- curved surface-family components (`cylinder`, `cone`, `sphere`, `torus`): center probe to inspect possible round or curved features;
- height-band components: dominant plateau and isolated plateau/recess representative probes;
- fallback components: center probe with an explicit approximate-measurement reason.

Commands use the native CLI form consistently:

```bash
aetheris analyze map <file.step> --plane xy --direction -z --point <u>,<v> --json
```

## Limits and truncation

Output is bounded to keep LLM summaries readable:

- at most 10 components per category per view;
- at most 10 suggested probes per view;
- at most 30 global suggested probes;
- if component category limits omit entries, `components.truncated` is true and `components.omittedCount` reports the omitted count.

## FTC-09 motivation

FTC-09 dogfooding showed that broad six-view facts were useful but not actionable enough: an LLM still had to parse no-hit islands, bands, and curved regions from compact ASCII. A5 moves that work into the measurement tool, allowing the next LLM step to choose specific point probes instead of visually decoding topology.

## Limitations

- A5 is still grid-sampling, not feature reconstruction.
- Classification hints are conservative and may depend on resolution.
- 4-connected components can split diagonally touching regions.
- Centroid probes are representative, not guaranteed feature centers.
- Fallback regions remain approximate; A5 surfaces them rather than hiding them.
- Detailed ray sample output for non-six-view map analysis is unchanged.

## Next milestone ideas

- Adaptive refinement around hit/no-hit and high-gradient boundaries.
- Face-id grids and connected face-region summaries.
- View-pair correlation to relate a top-view island to front/side evidence.
- Conservative hole/slot candidate hints that remain measured and explicitly non-semantic.

## A6 follow-up

ANALYZE-MAP-A6 builds on these components by ranking them for LLM CAD reasoning, adding section-probe suggestions, evidence-bundle output, and compact point-probe summaries. See `docs/development/implementation/analyze-map-a6-ranked-probes-and-section-bridge.md`.

## Phase closeout

The first LLM-oriented analyze-map phase is summarized in `docs/development/reports/analyze-map-phase-closeout-a0.md`.
