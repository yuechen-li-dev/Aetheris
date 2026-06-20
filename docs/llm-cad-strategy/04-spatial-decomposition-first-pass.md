# 04 — Spatial decomposition first pass

## 1. Purpose

This lesson teaches LLMs how to begin decomposing a complex CAD model spatially before inventing Firmament syntax, parser behavior, feature names, or a final feature tree.

The first pass is a reasoning artifact. It organizes likely spatial intent from BRep/analyze evidence so later modeling work has a stable construction strategy instead of a pile of faces or one overloaded sketch.

## 2. The hypothesis

For prismatic/CNC parts, begin by finding the gross blockout and simple prismatic operations before reaching for arbitrary sketches.

This is a default heuristic, not a universal law. Some parts genuinely require sketches, sweeps, lofts, freeform surfaces, or profile-driven sheet outlines. But CTC-like mechanical parts should first be tested against a blockout decomposition before an LLM reaches for a large arbitrary `Profile2D` and `ProfileExtrude`.

The doctrine is:

> A good CAD model is not the shortest path to the final shape; it is the most stable explanation of how the shape should exist.

## 3. Evidence from CTC-01

Source model studied:

```text
testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp
```

Aetheris CLI commands used:

```bash
dotnet run --project Aetheris.CLI -f net10.0 -- analyze testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --json > artifacts/llm-cad-notes-x1/ctc01/analyze.json

dotnet run --project Aetheris.CLI -f net10.0 -- analyze map testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --top --rows 24 --cols 32 --json > artifacts/llm-cad-notes-x1/ctc01/analyze-map-top.json
# The same map command was also run for --bottom, --front, --back, --left, and --right.

dotnet run --project Aetheris.CLI -f net10.0 -- analyze section testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --xy --offset 0 --json > artifacts/llm-cad-notes-x1/ctc01/analyze-section-xy-0.json

dotnet run --project Aetheris.CLI -f net10.0 -- analyze section testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --xy --offset -25 --json > artifacts/llm-cad-notes-x1/ctc01/analyze-section-xy--25.json

dotnet run --project Aetheris.CLI -f net10.0 -- analyze section testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --xz --offset 0 --json > artifacts/llm-cad-notes-x1/ctc01/analyze-section-xz-0.json

dotnet run --project Aetheris.CLI -f net10.0 -- analyze section testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --yz --offset 0 --json > artifacts/llm-cad-notes-x1/ctc01/analyze-section-yz-0.json

dotnet run --project Aetheris.CLI -f net10.0 -- canon testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp --out artifacts/llm-cad-notes-x1/ctc01/nist_ctc_01_canonical.step --json > artifacts/llm-cad-notes-x1/ctc01/canon.json
```

Summary facts from `analyze`:

- The model imports as one body and one shell.
- Topology count: 117 faces, 318 edges, and 206 vertices.
- Structural assessment: enclosed manifold.
- Bounding box: `x = -400..400`, `y = -225..225`, `z = -100..50`.
- Principal dimensions from that box: `800 x 450 x 150` in the imported length unit. Aetheris currently reports the length unit as assumed millimeters because STEP import length units are not yet preserved.
- Surface families: 56 planes, 57 cylinders, 4 cones, 0 spheres, 0 tori, 0 B-splines, and 0 other surfaces.

These counts are important. CTC-01 is not an organic or spline-heavy part. Its imported BRep is almost entirely planar and cylindrical, with a small number of conical faces. That supports a prismatic/mechanical reading: block/web masses, planar cuts, cylindrical holes or bosses, conical countersinks/chamfers, and edge finish.

Section evidence:

- `analyze section --xy --offset 0` reports 71 loops, 38 closed loops, 33 line segments, 39 arc segments, and no unsupported section segments. The section bounds match the full `x = -400..400`, `y = -225..225` plan footprint.
- `analyze section --xy --offset -25` reports the same section summary as the `z = 0` XY section. This suggests that a large plan-view footprint persists through an internal height interval, which is consistent with a main plate/web or repeated through features rather than a single top-only decorative outline.
- The XY section includes many circular/arc-only closed loops at mirrored or repeated-looking coordinates, including radius-like values near 50, 25, 20, 17.5, 12.5, and 10. Treat these as hole/boss/round-feature candidates, not as proof of the original feature tree.
- `analyze section --xz --offset 0` reports 8 loops, 2 closed loops, 9 line segments, 2 arc segments, and section bounds `x = -400..400`, `z = -50..50`. This central section shows a broad lengthwise spine with local raised or cut structure around the middle, plus small arc features.
- `analyze section --yz --offset 0` reports 5 loops, 2 closed loops, 12 line segments, 0 arc segments, and section bounds `y = -125..125`, `z = -50..50`. This narrower central YZ envelope is evidence that the central material spine is not simply the full outer bounding box in every section.

Map evidence and limitation:

- `analyze map` was run for all six orthographic views, but the current map implementation returned `analysis-failure` for this imported STEP body. The error says orthographic map v1 currently supports bodies accepted by `BrepSpatialQueries.Raycast`, and spatial query v1 only supports primitive BRep bodies from `BrepPrimitives.CreateBox/CreateCylinder/CreateSphere`.
- This does not contradict the spatial hypothesis. It is an Aetheris CLI capability limitation for imported complex BReps, and it should be recorded as tool friction for future analysis work.

Symmetry and repetition observations:

- The bounding box is centered at the origin in X and Y, with `x = -400..400` and `y = -225..225`.
- Many section-loop coordinates appear in positive/negative pairs or repeated families: for example features near `x = +/-160`, `+/-325`, `+/-300`, `+/-250`, `+/-225`, and smaller paired central features. This supports at least tentative bilateral/repeated-feature reasoning.
- The `z` range is asymmetric (`-100..50`), while central XZ/YZ sections report visible section bounds around `z = -50..50`. That means an LLM should not blindly assume symmetry in Z or infer one centered extrude without checking lower features.

Evidence for plate/web plus bosses/cuts/holes/slots/edge finish:

- The large `800 x 450 x 150` bounding envelope gives a natural stock/block coordinate frame.
- The dominant plane count supports planar webs, steps, pockets, notches, and reliefs.
- The nearly equal cylinder count supports many semantic holes, round bosses, rounds, or cylindrical edge/feature details.
- The conical face count is small and likely belongs late in the interpretation as countersink/chamfer/edge-finish-like detail unless a specific functional cone is identified.
- The XY sections expose a busy plan-view feature field with repeated arcs and closed loops. That is exactly the kind of evidence that should be classified as semantic holes/features after the gross blockout, not hidden inside a single arbitrary outer profile.

## 4. What the naive LLM would do

A naive LLM strategy is:

```text
final silhouette looks like an extruded profile
therefore create arbitrary Profile2D and ProfileExtrude
```

This is tempting because it is compact, geometrically plausible, and often matches the final outline from a single view.

It is strategically risky because it can create:

- sketch self-intersection;
- profile region ambiguity;
- high constraint-solver burden;
- a brittle, hard-to-edit main feature;
- poor recovery of feature intent;
- functional holes, slots, blocks, and reliefs hidden inside one sketch instead of represented as semantic operations.

The BRep facts for CTC-01 make this failure mode especially visible. There are many planar and cylindrical features, repeated-looking section loops, and central sections that do not simply equal the full outer bounding box. A single silhouette sketch would explain the final outline, but it would not be the most stable explanation of the part.

## 5. A more resilient spatial decomposition

A more resilient first-pass interpretation is:

```text
gross block / web
  subtract large rectangular reliefs
  add simple boss/tab masses
  subtract notches/pockets
  add/cut slots and holes
  edge finish last
```

For CTC-01, keep the exact feature order tentative. The CLI evidence supports the family of interpretation more strongly than it proves the original authoring sequence.

A useful LLM vocabulary for this pass is:

- **shape spine**: the gross mass or web that carries the part's coordinate frame and main dimensions;
- **feature details**: secondary additions and removals that refine the spine;
- **semantic holes**: cylindrical or slot-like features that probably serve fastening, clearance, locating, or tooling roles;
- **edge finish**: chamfers, rounds, conical transitions, and small end treatments applied after functional massing.

In CTC-01, the shape spine is likely an X/Y-oriented prismatic web or main mass bounded by the large centered envelope. Large reliefs, steps, and pockets should be tested as simple subtractive boxes or wedges before resorting to an arbitrary plan sketch. Cylindrical and conical faces should be grouped into semantic holes, countersinks, bosses, or edge finish after the blockout.

## 6. Spatial decomposition procedure for LLMs

The first pass is not source generation. It is spatial intent organization.

Use this repeatable method:

1. **Find the bounding box and principal axes.** Record min/max, dimensions, centering, and unit confidence.
2. **Identify the likely stock/block orientation.** Ask which axis looks like thickness, length, width, or build direction.
3. **Identify the main mass spine.** Look for the largest persistent material envelope across sections.
4. **Classify empty regions as candidate subtractive blocks/wedges.** Prefer rectangular, stepped, or planar removals before inventing arbitrary profiles.
5. **Classify protrusions as candidate additive bosses/tabs.** Use planar/cylindrical groupings and repeated coordinates as clues.
6. **Delay holes/slots until after blockout.** Holes and slots should register against the spine and functional submasses.
7. **Delay fillets/chamfers until after functional features.** Edge finish should not drive the primary decomposition.
8. **Record confidence and alternative strategies.** Note which facts are measured, inferred, unsupported, or contradicted.

This procedure intentionally separates strategy from syntax. The output should be a structured explanation that a later Firmament/AIR candidate can justify.

## 7. When to use sketches anyway

Sketches and profiles are appropriate when the part is genuinely outline-driven or when they reduce ambiguity instead of hiding it. Examples include:

- organic or silhouette-driven parts;
- sheet profiles;
- 2D laser/waterjet-like shapes;
- cases where repeated block operations are more complex than a clean constrained profile;
- profiles expressible as a simple admitted line/arc chain with low ambiguity.

Do not use an arbitrary sketch as a garbage chute for unresolved reasoning. If an LLM cannot explain why a contour exists, stuffing it into `Profile2D` does not make the CAD model more robust.

## 8. Output format for future decompilation

Future LLM decompilation attempts should emit this first-pass template before proposing source:

```text
Coordinate frame:
Gross blockout:
Major additions:
Major removals:
Functional holes:
Slots/pockets:
Repetition/symmetry:
Edge finish:
Ambiguities:
Missing Firmament/AIR capabilities:
Recommended next modeling primitive:
```

The template forces the model to state its construction strategy, unresolved choices, and missing capabilities before committing to syntax.

## 9. CTC-01 verdict

Yes, CTC-01 strongly supports a prismatic blockout-first interpretation.

The strongest evidence is the combination of a centered mechanical bounding box, no B-spline or freeform surfaces, many planar faces, many cylindrical faces, a small conical-face set, and section data showing a persistent broad footprint with repeated circular/arc feature candidates. This is not evidence for a sketch-first doctrine. It is evidence that an LLM should first try to explain the part as a block/web spine plus simple prismatic additions/removals, semantic holes/slots, and late edge finish.

The verdict is still nuanced:

- Some details may require future sketch/profile support or specialized primitives.
- The actual original CTC-01 feature tree is not proven by these CLI facts.
- The model should not be represented primarily as one arbitrary `mainWebOutline` profile unless later evidence shows that was the best authoring strategy.
- The next language work should focus on prismatic block/add/cut semantics and semantic holes/slots, not arbitrary sketch-first modeling.

## 10. Implications for Firmament

Likely implications:

- Firmament should make blockout operations first-class.
- Firmament should support semantic holes and slots as first-class operations.
- Sketch/profile support should be admitted deliberately, not used as a default escape hatch.
- Decompilation candidates should include a modeling-strategy justification, not only a feature inventory.
- Missing capability matrices should distinguish:
  - blockout primitives;
  - functional features;
  - edge finish;
  - sketch escape hatches.

This lesson does not authorize parser, lowering, AIR, BRep, STEP, DisplayIR, tessellation, frontend, or product behavior changes. It only records a reasoning method and CTC-01 evidence for future design discussion.

## 11. Next lessons

Recommended next lessons:

```text
05-blockout-before-sketch.md
06-holes-slots-and-functional-features.md
07-edge-finish-last.md
08-feature-tree-recovery-from-brep.md
```

Do not create those files until a milestone explicitly requests them.
