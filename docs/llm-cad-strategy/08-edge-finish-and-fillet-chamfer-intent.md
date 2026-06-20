# 08 — Edge finish and fillet/chamfer intent

## 1. Purpose

This lesson teaches LLMs how to reason about fillets, chamfers, rounds, and edge finish during CAD authoring and semantic decompilation. The goal is to distinguish practical edge treatment from profile construction, hole-local structure, and larger transition geometry that only happens to look like a fillet or chamfer in the final BRep.

This is strategy guidance. It is not implemented syntax. It does not authorize parser changes, lowering changes, Firmament V2 syntax changes, AIR changes, BRep changes, STEP import/export changes, DisplayIR changes, tessellation changes, frontend changes, or product behavior changes.

## 2. Edge finish is not arbitrary blend magic

Fillets and chamfers are not random geometry polish. They cost money, tool time, setup decisions, inspection complexity, model fragility, and failure risk. Engineers usually place them where they are useful: to remove sharp edges, improve manufacturability, reduce stress concentration, help assembly, make handling safer, provide tool access, satisfy an aesthetic requirement, or identify an intentional transition.

Arbitrary fillets all over a model are suspicious unless there is design or manufacturing evidence for them. A part that is fully rounded by default may be expensive to machine, hard to inspect, and fragile to edit. A decompiler that eagerly turns every cylindrical or conical transition into a source-level fillet can hide the real shape construction.

Fillets and chamfers are usually decorators over stable topology, not structural parents.

That dependency role matters. Global edge finish normally belongs late in the feature graph because it depends on edges produced by the core solid features. It should rarely be a parent of core geometry. If deleting or resizing a small edge finish changes the part's main layout, silhouette, mating function, or load path, it may not be mere edge finish.

## 3. Common edge-finish configurations

Realistic edge finishing often appears in a small number of practical target patterns. Recognizing the pattern helps an LLM avoid both under-modeling useful finish and over-modeling arbitrary blends.

### Single edge

A single-edge finish is plausible when one edge has a specific reason to be treated:

- one sharp edge needs a chamfer for clearance;
- one stress riser needs a radius;
- one external handling edge needs softening;
- one edge needs to lead a mating part into position.

The target should remain local. If the feature affects only one edge, do not inflate it into an all-body operation.

### Single planar loop, complete or incomplete

Loop-local finish is common because many manufacturing and functional edges are loops on a face:

- chamfer all edges around a hole mouth;
- round a pocket rim;
- fillet a boss base loop;
- chamfer only selected parts of a perimeter loop.

A complete loop often signals a feature-local manufacturing or assembly purpose. An incomplete loop may be equally intentional, especially when only reachable, exposed, or mating edges need treatment.

### All edges of a simple feature/body

All-edge finish can be realistic when the scope is simple and explicit:

- a small box-like boss has all vertical edges rounded;
- a simple block gets a uniform small chamfer;
- a simple machined plate gets a deburred perimeter.

“All edges” is realistic mainly for simple bodies or simple features. It is dangerous as a blanket operation on complex bodies. On a complex body, an all-edge fillet or chamfer can unintentionally alter datum edges, mating edges, internal transitions, and shape-defining boundaries.

## 4. Edge finish vs profile cornering

This lesson extends the distinction in [06 — Profiles as constructive regions](06-profiles-as-constructive-regions.md). Profile cornering happens before extrusion and defines the 2D silhouette. Edge finish happens after solid features and modifies 3D edges. The same final arc may have different intent depending on its dependency role.

Examples:

- a rounded slot end is profile or slot geometry, not edge finish;
- a rounded outside lobe is profile or blockout geometry if it defines the silhouette;
- a small radius on a plate edge is edge finish when it only softens an already-defined edge;
- a hole-mouth chamfer is a hole-local stack component, not a generic late chamfer unless evidence says it is separate global edge finish.

The key question is whether the arc or clipped corner defines the source profile before the solid exists. If so, model it with the profile or feature that owns the silhouette. Do not move shape-defining 2D intent into a late 3D edge decorator just because the final BRep contains curved or planar transition faces.

## 5. Edge finish vs ruled/transition surfaces

Sometimes a feature appears as fillet/chamfer-like geometry because traditional CAD tools made fillet/chamfer the easiest way to create a transition. But semantically it may be something larger than edge finish:

- a ruled transition;
- a draft or taper;
- a rib transition;
- a swept blend;
- a structural web;
- a manufacturing relief;
- part of the shape spine.

LLMs should ask:

```text
Is this small edge treatment?
Or is this transition geometry doing structural/design work?
```

If it changes the main silhouette, connects two masses, defines a functional ramp or web, controls load flow, provides clearance over a broad area, or spans a broad face region, it may not be edge finish even if it looks like a fillet or chamfer. A broad ruled surface between a boss and a base can be a rib or web. A sloped face that controls insertion may be a ramp. A large blend that defines the product outline may be part of the shape spine.

The decompilation risk is flattening intent. Calling every transition a fillet can make the graph look simple while destroying the distinction between decorative edge treatment and trunk-level construction.

## 6. Decompilation rules

When seeing cylindrical, conical, planar bevel, or ruled faces near edges, ask:

1. Is this a small local edge treatment?
2. Is it on a single edge, loop, or simple body-wide set?
3. Does it appear late relative to functional features?
4. Does it belong to a hole stack, such as countersink or hole-mouth chamfer?
5. Does it define a 2D profile corner before extrusion?
6. Does it form a structural transition, rib, or web?
7. Does it have repeated radius or distance values across similar edges?
8. Would an engineer pay to machine this edge finish for a reason?
9. Does treating it as edge finish make the dependency graph more resilient?
10. Does treating it as edge finish hide actual shape construction intent?

Use an explicit candidate record when the evidence is ambiguous:

```text
Edge finish candidate:
  kind: fillet / chamfer / round / unknown
  target pattern: single edge / loop / partial loop / simple-body all-edges / other
  radius/distance:
  likely reason:
  dependency role:
  belongs to:
    global edge finish / hole stack / profile cornering / ruled transition / unknown
  confidence:
  not-edge-finish alternatives:
```

Prefer explanations that preserve alternatives over premature certainty. A decompiler can say “likely loop chamfer on the hole mouth” or “possibly ruled transition, not edge finish” when the evidence is incomplete.

## 7. Placement/target semantics

Future Firmament target styles may need to represent edge finish without collapsing everything to unstable generated edge IDs. The examples below are design sketches, not implemented syntax.

### Style A — single edge finish

```firmament
finish Fillet {
    target: edge("boss.base.front")
    radius: 2mm
}
```

Evaluation:

- readability: high when the edge name is semantic and meaningful;
- semantic preservation: good for genuinely single-edge intent;
- edit locality: good if the edge reference tracks the intended feature role;
- lowering difficulty: moderate because the compiler must resolve a stable edge target;
- decompilation friendliness: useful when evidence isolates one treated edge;
- failure modes: dangerous if edge IDs are generated, order-dependent, or unstable.

Critique:

- precise;
- dangerous if edge IDs are unstable;
- should use semantic edge refs when possible.

### Style B — loop finish

```firmament
finish Chamfer {
    target: loop("mountHole.entry")
    distance: 1mm
}
```

Evaluation:

- readability: high for hole mouths, pocket rims, boss bases, and perimeter loops;
- semantic preservation: strong because the loop remains a single target concept;
- edit locality: good when the parent feature can move or resize while preserving the loop role;
- lowering difficulty: moderate to high because loop identity must survive feature edits;
- decompilation friendliness: strong for repeated complete-loop or partial-loop evidence;
- failure modes: ambiguous if multiple loops share similar roles or if the loop is broken by later features.

Critique:

- good for hole mouths, pocket rims, boss bases;
- matches common manufacturing intent;
- requires stable loop references.

### Style C — semantic feature-local finish

```firmament
hole<clearance> mount {
    on: top
    center: [0, 0]
    fit: M8
    mouthChamfer: 0.5mm
}
```

Evaluation:

- readability: high because the finish is declared where the functional feature is declared;
- semantic preservation: very strong for countersinks, thread entries, and hole-mouth chamfers;
- edit locality: strong because the finish follows the hole when the hole changes;
- lowering difficulty: localized to the feature stack rather than global edge selection;
- decompilation friendliness: strong when coaxial or entry-face evidence supports a hole stack;
- failure modes: wrong if the chamfer is actually a separate global operation shared with non-hole edges.

Critique:

- keeps hole-local chamfer attached to the hole stack;
- avoids treating it as unrelated late global edge finish;
- good for decompilation.

### Style D — feature perimeter finish

```firmament
finish Round {
    target: feature("centerBoss").verticalEdges
    radius: 2mm
}
```

Evaluation:

- readability: high when the edge role is obvious;
- semantic preservation: strong because the target is a feature role, not a raw edge set;
- edit locality: good if the feature keeps the same conceptual vertical edges after resize;
- lowering difficulty: high because role selectors must be defined and validated;
- decompilation friendliness: strong for patterns like boss vertical edges or plate perimeter edges;
- failure modes: role names can become vague, overbroad, or inconsistent across feature types.

Critique:

- preserves design intent better than raw edge list;
- requires stable feature identity and edge-role selectors.

### Style E — all simple-body edges

```firmament
finish Chamfer {
    target: body("smallBlock").allEdges
    distance: 0.5mm
}
```

Evaluation:

- readability: high for simple bodies and small imported/simple features;
- semantic preservation: acceptable only when “all edges” is truly the design scope;
- edit locality: risky because adding a new edge may unexpectedly include it in the finish;
- lowering difficulty: lower than semantic role selectors but still requires explicit body scope;
- decompilation friendliness: good for simple blocks with uniform small finish;
- failure modes: unsafe on complex bodies, datum edges, mating faces, and internal transitions.

Critique:

- acceptable for simple bodies/features;
- dangerous on complex bodies;
- should require explicit scope.

### Style F — low-level edge list escape hatch

```firmament
finish Fillet {
    edges: [edgeId(...), edgeId(...)]
    radius: 2mm
}
```

Evaluation:

- readability: low unless the edge IDs are supplemented by comments or diagnostics;
- semantic preservation: weak because the target is an implementation artifact;
- edit locality: poor because topology changes can invalidate the list;
- lowering difficulty: relatively direct once the IDs exist;
- decompilation friendliness: acceptable as an imported fallback, not as the preferred recovered source;
- failure modes: brittle references, noisy diffs, hidden loops, and lost feature intent.

Critique:

- necessary fallback/imported case;
- brittle;
- should not be default decompilation target.

## 8. Manufacturing and utility constraints

Edge finish is constrained by utility and cost. Fillets and chamfers may be used for:

- deburr and safety;
- assembly insertion;
- stress relief;
- tool access;
- fit and clearance;
- aesthetic requirement;
- avoiding sharp edges;
- reducing crack initiation;
- making edges machinable.

They may also be omitted for valid reasons:

- machining cost;
- inspection burden;
- radius would interfere with a mating part;
- a sharp datum edge is needed;
- the operation is unnecessary.

Therefore LLMs should not add edge finish casually. Edge finish should be justified by utility, manufacturing process, safety, assembly, inspection, or stated product requirement. Small finish can be appropriate and important, but it is not free decoration.

## 9. Relationship to holes and profiles

### Holes

Countersinks, counterbores, drill tips, thread entries, and hole-mouth chamfers may belong to the hole feature stack. They are not necessarily independent global edge finish. When the bevel or radius is coaxial with a hole, attached to the entry or exit, and sized like a standard preparation, prefer hole-local interpretation unless evidence suggests a separate finishing pass.

### Profiles

Rounded 2D corners and clipped 2D corners may define the profile silhouette before extrusion. They are profile-level cornering, not late 3D edge finish. If a rounded slot end, lobe, tab, or clipped plate corner is part of the sketch/blockout identity, it belongs in the constructive profile.

### Dependency graph

Global edge finish belongs late and should usually be leaf-level. Hole-local or profile-local finishing belongs inside its parent feature if it is part of that feature’s structure. This keeps the dependency graph resilient: core geometry defines the part, feature-local details travel with their owning features, and global finish decorates stable topology at the end.

## 10. Firmament recommendation

For future Firmament design and decompilation strategy:

- treat global edge finish as a distinct late feature category;
- treat hole-local finish as part of the hole stack when appropriate;
- treat profile cornering as part of the 2D profile or blockout;
- use semantic targets such as feature edges, loops, and role-based selectors instead of raw generated edge IDs where possible;
- keep low-level edge-list finish as an escape hatch only;
- require or encourage explicit target scope:
  - single edge;
  - loop;
  - partial loop;
  - feature edge role;
  - simple-body all edges.

This is a recommendation for reasoning and future design discussion. It is not a claim that any syntax, selector, parser behavior, lowering behavior, kernel behavior, display behavior, or product behavior is currently implemented.

## 11. Common failure modes

- treating every cylindrical or conical transition as fillet/chamfer;
- missing that a fillet-like face is a ruled or structural transition;
- using global edge finish for hole countersinks;
- using late edge finish for profile cornering;
- referencing unstable generated edges;
- adding decorative fillets too early;
- applying all-edge fillet/chamfer to a complex body;
- flattening loops into raw edge lists;
- ignoring cost and utility of machining edge finish.

## 12. Non-goals

This lesson does not provide or authorize:

- parser changes;
- syntax guarantees;
- fillet/chamfer implementation;
- edge selector implementation;
- manufacturing cost model;
- product behavior changes.
