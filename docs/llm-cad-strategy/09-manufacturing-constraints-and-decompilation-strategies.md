# 09 — Manufacturing constraints and decompilation strategies

## 1. Purpose

This lesson teaches LLMs how manufacturing constraints shape CAD language design and how to choose a semantic decompilation strategy. The manufacturing process is not just a downstream fabrication detail. It narrows or expands the set of plausible operations, the feature classes worth naming, the topology contracts worth protecting, and the search space an LLM must explore.

This is strategy guidance. It is not implemented syntax. It does not authorize parser changes, lowering changes, Firmament V2 syntax changes, AIR changes, BRep changes, STEP import/export changes, DisplayIR changes, tessellation changes, frontend changes, or product behavior changes.

## 2. 3D printing is the Python of manufacturing

Additive manufacturing is like Python in a careful, limited sense: it is flexible, expressive, excellent for prototyping, and good at enabling fast iteration. It can produce structures that CNC machining, injection molding, and sheet-metal fabrication cannot easily make: internal lattices, organic transitions, nested cavities, complex undercuts, consolidated assemblies, and geometry that is optimized more for shape freedom than for tool access.

That is not an insult to 3D printing or to Python. Flexibility is powerful. It lets designers test ideas quickly, encode complex structure directly, and defer some constraints that would dominate a subtractive or mold-driven design.

The tradeoff is that flexibility expands the design, language, and search space. When almost any surface, cavity, lattice, overhang, or internal feature might be intentional, an LLM has fewer constraints to lean on. Decomposition becomes less obvious. Lowering choices multiply. Validation needs different manufacturing knowledge. A language that starts by treating arbitrary shape freedom as normal must answer many hard questions before it can reliably author, decompile, and reject invalid models.

```text
Flexibility is powerful, but it expands the design/language/search space.
```

For an LLM-oriented CAD language, too much early flexibility can make decomposition and lowering harder. The lesson is not to avoid additive manufacturing. The lesson is to recognize that it is a broader design world than the first Firmament training ground should require.

## 3. Why start from CNC / prismatic constraints

Aetheris and Firmament should start with CNC-like, prismatic mechanical parts because they provide a narrower and more learnable semantic space. The dominant operation family is easier to name and reason about: blocks, bosses, pockets, holes, slots, counterbores, countersinks, edge finish, reliefs, and repeated patterns. These parts tend to rely on planar, cylindrical, and extruded features rather than arbitrary freeform surfaces.

CNC/prismatic constraints also make manufacturability more visible. A pocket must be reachable. A hole has through or blind semantics. Tool radii affect inside corners. Edge finish has cost and intent. A slot, boss, or relief usually has a recognizable functional role. Invalid operations are easier to detect because the rules are stricter and the plausible operation set is smaller.

For language design, this matters. Clearer feature classes make Firmament source easier to read. Fewer arbitrary surfaces make AIR and BRep topology contracts easier to specify. More predictable feature dependencies make lowering easier to validate. More explicit rejection reasons make failure modes easier to explain. LLMs can infer strategy from a smaller menu of plausible manufacturing operations instead of searching an open-ended shape universe.

```text
What we lose in expressive freedom, we gain in a narrower and more learnable semantic space.
```

This does not mean additive manufacturing is unimportant. It means CNC/prismatic modeling is the right first training ground for Firmament and LLM CAD strategy.

## 4. High-level source as recovered structure

Semantic decompilation is reverse engineering from lowered geometry back toward editable intent:

```text
lowered STEP/BRep data
  -> recovered feature intent
  -> structured high-level Firmament candidate
```

A BRep is like compiled machine code or lowered IR. It contains precise geometry and topology, but it usually does not preserve the author's feature tree, construction order, design rationale, or editable source structure. A feature tree or Firmament candidate is closer to higher-level intent: the base mass, functional cuts, semantic holes, patterns, edge finish, constraints, and dependency graph that make the model understandable and maintainable.

As in software decompilation, the exact original source may be unrecoverable. Two designers can create the same final body with different feature trees. A STEP file may have lost names, sketches, parameters, mates, or construction references. The goal is therefore not always to recover the original feature tree. The goal is a plausible, useful, editable, resilient source representation that matches the available evidence and states its confidence.

A good semantic reconstruction can be better than a literal reconstruction when the literal path would preserve accidental faces, fragile dependencies, or modeling noise.

## 5. Strategy A — strangler-fig feature recognition

Strangler-fig feature recognition starts from the final body and gradually peels away recognizable layers until the core shape is exposed:

```text
final body
  identify/remove edge finish
  identify/remove hole stacks
  identify/remove slots/pockets/cuts
  identify additive bosses/tabs
  recover base block/profile/revolve
```

This is like strangler-fig refactoring of a legacy codebase. You work around the existing final form, replace or explain one recognizable region at a time, and gradually expose the core structure without pretending the legacy body was authored cleanly. The method preserves more direct correspondence to the imported BRep because each recovered feature is grounded in observed faces, edges, cylinders, planes, loops, and adjacency.

A practical recognition order is:

1. Detect edge finish:
   - fillets;
   - chamfers;
   - rounds;
   - hole-mouth countersinks and counterbores when they are part of hole stacks.
2. Detect semantic holes:
   - through holes;
   - blind holes;
   - countersinks and counterbores;
   - standard hole groups and patterns.
3. Detect slots, pockets, and functional cuts.
4. Detect large subtractive reliefs.
5. Detect additive bosses, tabs, and ribs.
6. Recover the base block, profile, revolve, or spine.
7. Produce a feature graph with evidence and confidence.

Advantages:

- Grounded in actual BRep faces and measured topology.
- Good for imported geometry where the body itself is the main evidence.
- Good when exact dimensional match matters.
- Can preserve direct evidence for face ownership, cylindrical features, loops, and adjacency.
- Useful for automated feature recognition and audit trails.

Disadvantages:

- Can inherit bad modeling decisions from the imported body.
- May produce feature soup instead of a clean dependency graph.
- Can mistake result geometry for authoring intent.
- Hard when edge finish, structural transitions, draft, and reliefs are conflated.
- May be brittle for complex, over-modeled, or low-quality STEP files.

## 6. Strategy B — parallel-lane redraw

Parallel-lane redraw uses the imported body as reference, but constructs a new clean feature graph from scratch:

```text
final body / screenshots / measurements
  infer clean design intent
  construct new resilient feature graph from scratch
  compare against imported body
  iterate
```

This is like rebuilding a subsystem beside the legacy implementation before replacing it. The old body remains the measurement and comparison target, but the new source does not need to preserve every accidental face, edge split, or fragile modeling decision. The goal is a better source representation, not a face-by-face biography of the STEP file.

A practical redraw order is:

1. Establish coordinate frame and bounding box.
2. Identify the shape spine or base mass.
3. Rebuild gross blockout using stable primitives.
4. Add major bosses, tabs, and cuts.
5. Add semantic holes, slots, and pockets.
6. Add patterns and mirrors.
7. Add edge finish last.
8. Compare dimensions, sections, visuals, and topology against the source.
9. Record intentional deviations.

Advantages:

- Produces a cleaner feature tree.
- Better for resilient modeling and future edits.
- Better for source authoring and human-readable Firmament candidates.
- Avoids reproducing accidental BRep complexity.
- Works well when the model is simple enough to understand visually and dimensionally.

Disadvantages:

- May lose exact details.
- Depends on good measurements, sections, screenshots, drawings, or inspection output.
- Risks LLM hallucination of intent.
- Requires validation against the source body.
- Less suitable when exact imported geometry or topology must be preserved.

## 7. Choosing between the two strategies

| Condition | Prefer strangler-fig recognition | Prefer parallel-lane redraw |
| --- | --- | --- |
| Need exact round-trip fidelity | Strongly prefer when face/topology correspondence matters. | Use only with strict comparison and tolerance gates. |
| Model complexity | Prefer for complex imported bodies that are hard to redraw confidently. | Prefer for visually understandable parts with simple dominant structure. |
| Presence of PMI / feature names | Use names and PMI as evidence for recognized features. | Use names and PMI to guide a cleaner reconstruction. |
| Clean prismatic design | Useful for confirming holes, pockets, and edge finish. | Often best for reconstructing the clean blockout and feature graph. |
| Heavy edge finish | Useful for detecting and peeling finish before deeper inference. | Useful after finish is understood, but redraw should add finish late. |
| Freeform/surface-heavy part | May be necessary to preserve observed surfaces. | Risky unless the intent is visually obvious and tolerances are loose. |
| Poor imported geometry | Can fail if topology is noisy or fragmented. | Prefer if the design intent is clear despite bad topology. |
| Human-readable source goal | Can provide evidence, but may be too literal. | Prefer when clean editable Firmament source is the main goal. |
| Manufacturing process goal | Prefer when process-specific face evidence must be retained. | Prefer when the process suggests a simpler intended feature sequence. |
| Availability of dimensions/drawings | Helpful, but not always required. | Strongly preferred because redraw needs independent constraints. |
| Confidence in visual interpretation | Less dependent on visual inference. | Prefer only when visual and dimensional interpretation is high-confidence. |

Use strangler-fig recognition when exact imported topology, faces, measured evidence, or auditability matter. Use parallel-lane redraw when the goal is clean editable Firmament source and the design intent is clear. Use a hybrid when needed: strangler recognition for holes, edge finish, repeated cylinders, and topology evidence; redraw for gross blockout, feature grouping, and the stable dependency graph.

## 8. Hybrid strategy

Many real decompilation tasks should combine both strategies:

```text
Use BRep evidence to detect:
  holes
  repeated cylinders
  edge finish
  thickness
  symmetry
  surface families

Use redraw strategy to propose:
  clean blockout
  feature grouping
  stable dependency graph
  Firmament candidate
```

This is likely the best LLM workflow. BRep analysis is strong at proving that certain geometry exists: cylinders align, holes repeat, faces are planar, loops are similar, edges share radii, and thickness appears consistent. Redraw is strong at proposing a human-readable construction that does not preserve every accidental split face or import artifact as a source feature.

For CTC-01, use BRep/analyze evidence for bounding box, face counts, cylindrical features, sections, and measurable agreement. Use visual CAD experience for blockout strategy. Do not preserve every face as a source feature. Use the missing capability matrix to guide the Firmament roadmap rather than forcing brittle parser, lowering, AIR, BRep, STEP, DisplayIR, tessellation, or frontend changes into a strategy note.

## 9. Manufacturing process as concept constraints

A manufacturing process acts like a `concept` constraint for CAD authoring. It changes which operations are natural, which dimensions matter, which failure modes are plausible, and which features should be first-class in the source representation.

For CNC/prismatic work, the constraints include:

- minimum tool radii;
- accessible holes and pockets;
- through and blind semantics;
- edge finish costs;
- no impossible internal cavities without setup changes, split parts, or process changes;
- preference for planar, cylindrical, and prismatic features.

For additive work, the constraints include:

- overhangs;
- support structures;
- wall thickness;
- lattice, gyroid, and internal structures;
- orientation;
- anisotropic strength.

Different manufacturing processes should eventually imply different Firmament concepts, templates, and rules. A CNC-like plate, a printed lattice bracket, a sheet-metal enclosure, a cast housing, and a surfacing-heavy consumer shell should not all be forced through the same default authoring doctrine. The first learning path should focus on CNC/prismatic modeling because its constraints are narrower.

## 10. Implications for Firmament

Firmament should make CNC/prismatic blockout easy. The language should make the common resilient path obvious before it asks an LLM to assemble arbitrary raw geometry.

Semantic features should be first-class:

- holes;
- slots;
- pockets;
- bosses;
- edge finish.

Arbitrary sketches and raw booleans should remain available, but they should not be the default authoring strategy for prismatic mechanical parts. A source candidate that describes a hole as a semantic hole, a slot as a slot, and edge finish as late edge finish is more useful than one that encodes everything as anonymous cylinders, profiles, and booleans.

Decompilation candidates should state which strategy was used:

- strangler recognition;
- parallel redraw;
- hybrid.

Candidate fixtures should distinguish between an exact imported topology goal and a clean source reconstruction goal. Those are different success criteria. A decompiler that optimizes for one while claiming the other will produce confusing results.

## 11. LLM workflow checklist

```text
1. What manufacturing process does this look designed for?
2. Is the object prismatic/CNC-like, additive-like, cast-like, sheet-like, or surface-heavy?
3. Is the goal exact reproduction or clean editable source?
4. Should I use strangler recognition, parallel redraw, or hybrid?
5. What is the shape spine?
6. Which features are functional?
7. Which features are likely edge finish?
8. Which details should not be preserved as source features?
9. What evidence supports each inference?
10. What capabilities are missing from Firmament/AIR?
```

An LLM should answer these questions before writing a Firmament candidate. If the answers are uncertain, the candidate should carry that uncertainty instead of pretending the feature tree is uniquely determined.

## 12. Relationship to previous notes

This note chooses the decompilation strategy. Earlier notes describe how to reason within the chosen strategy.

It connects directly to [04 — Spatial decomposition first pass](04-spatial-decomposition-first-pass.md): first understand bounding boxes, masses, sections, axes, and dominant volumes before committing to a feature tree.

It extends [05 — CAD as semantic dependency graph](05-cad-as-semantic-dependency-graph.md): whether using strangler recognition or redraw, the result should be a dependency graph with stable parents, functional children, and late decorators.

It relies on [06 — Profiles as constructive regions](06-profiles-as-constructive-regions.md): profiles should be recovered or redrawn as constructive regions, not boundary fragments copied from final faces.

It uses [07 — Holes are semantic features](07-holes-are-semantic-features.md): holes, hole stacks, countersinks, counterbores, and patterns are semantic features, not merely cylinder booleans.

It builds on [08 — Edge finish and fillet/chamfer intent](08-edge-finish-and-fillet-chamfer-intent.md): edge finish is often detected early during recognition but authored late in a clean feature graph.

## 13. Non-goals

This note does not provide:

- additive manufacturing doctrine;
- parser implementation;
- lowering implementation;
- an automatic decompiler;
- a claim that CNC-first is universal;
- a claim that exact original source can always be recovered.

It also does not change Firmament V2 syntax, AIR, BRep, STEP import/export, DisplayIR, tessellation, frontend behavior, product behavior, or test expectations. It is a field-manual note for LLM reasoning and future roadmap discussion.
