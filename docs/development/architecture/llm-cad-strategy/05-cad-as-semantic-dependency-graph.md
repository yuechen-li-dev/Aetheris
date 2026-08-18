# 05 — CAD as semantic dependency graph

## 1. Purpose

This lesson teaches LLMs to reason about CAD models as semantic dependency graphs that are later serialized into feature order.

A finished model may be displayed as a feature tree, exported as BRep, or described as visible faces and edges. None of those views is the whole modeling strategy. The useful question for decompilation is not only "what features are visible?" It is "what stable construction graph would make these features depend on the right things?"

For LLMs, this is a guardrail against treating CAD as a chronological inventory. First recover the likely root anchors, mass spine, functional children, repeated structures, and edge-finish decorators. Then propose a safe feature order.

## 2. The misleading feature tree

CAD systems show a feature tree because it is a convenient user interface. It lets users replay operations, suppress features, inspect sketches, and understand the apparent build sequence.

That tree is usually chronological. The first feature appears first, later cuts and additions appear later, and final edge treatments tend to appear near the end. Chronology is useful, but chronological order is not the same as semantic dependency.

A bad model can become brittle when arbitrary later features depend on fragile earlier references:

- a sketch projected from a face that later disappears;
- a cut dimensioned from an incidental edge instead of a datum;
- a hole placed from a filleted edge rather than a stable plane or axis;
- a boss constrained to a face created by a temporary workaround;
- a pattern whose members are edited as unrelated individual cuts.

Traditional feature trees can therefore become like HTML/CSS/DOM dependency spaghetti: visually tree-like, but semantically entangled. The UI shows a tree, while the real dependencies behave more like a graph of sketches, faces, edges, constraints, generated names, and downstream references.

An LLM should not assume that the displayed or guessed feature order is the modeling intent. The goal is to recover a cleaner semantic graph and only then serialize it.

## 3. The real graph

A resilient CAD graph is organized by dependency layers:

```text
Root / datum layer
Base mass / shape spine
Additive mass layer
Subtractive feature layer
Functional hole/slot/pocket layer
Pattern/symmetry layer
Edge-finish decorator layer
```

### Root / datum layer

This layer contains the origin, principal axes, datum planes, named coordinate frames, construction axes, and stable reference dimensions.

It should depend on the design coordinate system and measured global intent. It should not depend on generated faces, fillets, chamfers, or incidental imported edge IDs.

Most other layers may depend on root datums. Root datums should not depend on downstream features.

### Base mass / shape spine

This layer contains the primary block, plate, web, frame, housing, or other gross mass that carries the part's main dimensions and orientation.

It should depend on root anchors, datum planes, and high-confidence envelope dimensions. It may use simple sketches or profiles when the object is genuinely profile-driven, but it should not absorb every later detail into one overloaded sketch.

Functional holes, local pockets, bosses, and edge finish should not define the mass spine unless there is strong evidence that they are truly structural parents.

### Additive mass layer

This layer contains bosses, tabs, pads, ribs, webs, lugs, collars, mounting ears, and other material added to the base mass.

Additive features should depend on stable datums, the base mass, or named semantic faces/planes. They should not depend on small edge-finish geometry or arbitrary downstream cut edges.

Later cuts, holes, and finish operations may depend on additive masses. The additive masses should remain understandable if those later details are suppressed.

### Subtractive feature layer

This layer contains major cuts, reliefs, notches, pockets, steps, windows, and other material removals that shape the gross part.

Subtractive features should depend on root datums, stable faces, and named dimensions from the mass spine or additive children. They should not depend on filleted/chamfered edges, cosmetic rounds, or unstable face splits when a datum or semantic reference would work.

Subtractive features may create stable regions for later functional holes or slots, but they should not be used as a garbage bin for unrelated geometry.

### Functional hole/slot/pocket layer

This layer contains holes, counterbores, countersinks, threaded holes, clearance holes, locating holes, slots, and pockets that have functional meaning.

These features should depend on stable datums, axes, pattern definitions, mounting faces, and named semantic dimensions. They should not be flattened into anonymous cylinder cuts when the likely intent is clearance, threading, locating, fastening, or tooling.

Core massing should not depend on final hole-edge finish. If a later feature needs the hole centerline, reference the semantic hole or datum axis, not a generated circular edge after chamfering.

### Pattern/symmetry layer

This layer contains mirrored features, linear patterns, circular patterns, bilateral symmetry, and repeated families of holes, slots, bosses, or cuts.

Patterns should depend on stable seed features, root axes, datum planes, and explicit spacing/count parameters. Repeated instances should not be modeled as unrelated individual features when a pattern explains them better.

The mass spine should not depend on pattern members unless the repeated feature family truly defines the primary structure.

### Edge-finish decorator layer

This layer contains fillets, chamfers, rounds, small blends, deburr-like treatments, and similar finish operations.

Edge finish should depend on stable edges created by the functional model. It should normally be a leaf layer: many finish operations may depend on earlier geometry, but core geometry should rarely depend on finished edges.

Downstream structural features should not depend on this layer. If they do, the model becomes brittle because small edge changes can break major construction logic.

## 4. Serialization vs dependency

A CAD feature tree is a serialized execution order. A good modeling strategy first chooses the dependency graph, then serializes it safely.

A resilient feature tree is a topological sort of a semantic construction graph.

In simple terms:

- create root anchors before geometry that uses them;
- create the base mass before holes that pass through it;
- create major additions and removals before small functional details that locate on them;
- create holes and slots before fillets around their rims;
- put fillets, chamfers, and rounds late;
- make repeated features depend on stable datums, seed features, and pattern definitions;
- do not let details define the mass spine when the details are only children of the mass.

The serialized order is still important because kernels execute operations in order. But the order should express dependency, not merely mimic the order in which visible features caught the LLM's attention.

## 5. Why fillets and chamfers are decorators

Fillets, chamfers, and rounds modify edges after the functional model exists. They may improve manufacturability, reduce stress concentration, remove sharp edges, or match the final visual shape. They are important, but they are usually not the primary structure.

They are also fragile. A fillet can fail when an upstream face changes size, an edge disappears, two cuts merge, or a radius no longer fits. A chamfer can split faces and create generated edges that are poor anchors for later modeling.

For that reason, core features should not usually reference filleted or chamfered edges. A hole should be located from a datum or semantic face, not from a rounded edge. A boss should be dimensioned from the mass spine, not from a decorative blend.

Edge finish belongs late because it depends on stable edges.
If downstream core features depend on edge finish, the model becomes brittle.

Treat edge finish like visual or manufacturing edge treatment over a complete functional body. It can be required for the final part, but it should usually be a leaf in the dependency graph.

## 6. LLM decompilation rule

Use this rule when decompiling CAD models:

```text
Do not infer a CAD model as a chronological list of visible features.
Infer a semantic dependency graph first.
Then propose a feature order.
```

When decompiling:

- identify root anchors;
- identify the base mass;
- identify stable reference planes, faces, axes, and coordinate frames;
- identify additive children;
- identify subtractive children;
- identify functional details such as holes, slots, and pockets;
- identify pattern and symmetry relationships;
- identify decorators such as fillets, chamfers, and rounds;
- avoid making decorators parents;
- avoid using arbitrary sketch loops as garbage bins for unresolved reasoning.

The output should make dependency claims explicit. If a dependency is only guessed, mark it as tentative. If two graph interpretations are plausible, explain the ambiguity instead of collapsing the model into one accidental feature list.

## 7. CTC-01 example

### Topology-selection handoff

For later finishing, a dependency graph must retain authored Profile/Compose identities through the authoritative BRepPlan. SEMANTIC-LOOPS-X1 establishes this for direct Profile extrusion: source segment descendants are selected with explicit body/role/cardinality contracts, never by raw BRep id or geometric rediscovery. The CTC X3 lobe data is ready as a source side of that relation, but its arrangement/transition emitter still needs to publish descendant correspondence before a finish target is admitted. This is an honest compiler boundary, not a license to match the closest cylindrical edge.

Use CTC-01 as a conceptual example of better reasoning, not as a historical reconstruction of the original authoring tree.

A naive feature tree might be guessed as:

```text
one giant profile sketch
  many hole cuts
  many fillets
```

That may reproduce some visible shape, but it hides the likely construction logic. A giant sketch tends to absorb unrelated facts: outer envelope, reliefs, hole positions, local blocks, and edge finish. The result is compact but brittle.

A more resilient graph-style reading is:

```text
root at origin
  base block / web
    block cuts / reliefs
    bosses / tabs
    holes / slots
    patterns
    fillets / chamfers
```

In this reading, the root origin and principal axes stabilize the model. The base block or web carries the main dimensions. Reliefs and pockets remove material from that mass. Bosses and tabs add local structure. Holes and slots express functional details. Patterns capture repetition. Fillets and chamfers decorate stable edges late.

Do not claim this is the exact original CTC-01 feature tree unless separate evidence proves it. The point is that this graph is a better decompilation hypothesis than a chronological inventory of visible features.

## 8. Firmament implications

Firmament could eventually reflect this semantic graph more directly.

Possible future directions include source sections such as `blockout`, `features`, `holes`, and `finish`, or semantic groups that exist in tooling and documentation without enforcing new syntax yet. Such groupings may help LLMs and humans discuss dependency layers without prematurely committing to language changes.

The strategic direction is that Firmament should make stable dependency expression easier than brittle reference chains. Features should reference stable semantic names, datum planes, axes, construction frames, or named faces where possible, rather than incidental generated edge IDs.

Edge finish should be represented explicitly as late/decorator operations when the language and lowering support it. That does not mean every chamfer is cosmetic or optional. It means the dependency graph should usually treat finish as depending on core geometry, not as a parent of core geometry.

This section is strategy guidance only. It does not define new syntax as implemented, and it does not authorize parser, lowering, AIR, BRep, STEP, DisplayIR, tessellation, frontend, or product behavior changes.

## 9. Output template

When decomposing a model, emit this template before proposing source:

```text
Root anchors:
Shape spine:
Additive children:
Subtractive children:
Functional holes/slots:
Patterns/symmetry:
Edge-finish decorators:
Fragile dependencies to avoid:
Suggested serialized feature order:
Confidence / ambiguity:
```

The template forces the LLM to separate construction logic from final appearance. It also makes it easier for reviewers to see whether the proposed feature order follows stable dependencies.

## 10. Common failure modes

Avoid these failures:

- monolithic sketch first;
- referencing filleted/chamfered edges too early;
- using final faces instead of stable datums;
- treating holes as generic cylinder cuts when they are semantic clearance, thread, locating, or diameter features;
- flattening repeated features into unrelated individual cuts;
- producing a feature inventory without dependency order.

These failures usually create models that are hard to edit, hard to explain, and easy to break when upstream dimensions change.

## 11. Non-goals

This lesson has strict boundaries:

- no parser changes;
- no lowering changes;
- no new Firmament syntax;
- no AIR changes;
- no BRep changes;
- no STEP import/export changes;
- no DisplayIR changes;
- no tessellation changes;
- no frontend or product behavior changes;
- no claim that all CAD fits this model;
- no claim of exact original feature history.

The lesson is a practical decompilation heuristic. It helps LLMs choose robust dependencies before proposing a feature order, while leaving implementation decisions to explicit future milestones.
