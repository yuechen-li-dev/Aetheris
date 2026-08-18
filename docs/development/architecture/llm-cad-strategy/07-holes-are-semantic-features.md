# 07 — Holes are semantic features

## 1. Purpose

This lesson teaches LLMs to treat holes as semantic features with placement, function, manufacturing/process implications, and axial structure. A hole is usually not just evidence that cylindrical faces exist. It is often a functional feature with an entry face, axis, diameter or standard class, end condition, possible thread/counterbore/countersink/tip/chamfer structure, and a stable role in the design.

This is strategy guidance for Firmament authoring, review, and semantic decompilation. It is not implemented syntax. It does not authorize parser changes, lowering changes, Firmament V2 syntax changes, AIR changes, BRep changes, STEP import/export changes, DisplayIR changes, tessellation changes, frontend changes, or product behavior changes.

## 2. The lowered-form trap

The bad default is:

```text
hole = cylinder subtract
```

That is the same kind of lowered-form mistake as treating every 2D profile as an arbitrary `Line(...)`, `Arc(...)`, `Line(...)` boundary chain. It begins from geometry that the compiler or kernel might eventually need, but it erases the author's reason for creating the geometry.

```text
Cylinder subtract:
  remove this cylindrical volume

Hole:
  create a functional axis-based feature with placement, diameter/class, entry face,
  axis, end condition, possible thread/counterbore/countersink/tip/chamfer,
  manufacturing meaning, and stable semantic identity
```

A cylinder subtract is a possible lowering of a simple hole. It is not the authoring intent for every hole-like feature. Decompilation should try to recover the semantic hole when evidence supports it, and should fall back to a generic cylindrical cut only when the evidence does not justify hole intent.

The distinction matters because edits, validation, manufacturing interpretation, and pattern recognition attach to the semantic feature. A through clearance hole in a bolt pattern is easier to inspect, resize, standardize, and relocate than four unrelated cylinder booleans whose only shared fact is radius.

## 3. Holes are not pockets, slots, or generic cuts

### Hole

A hole is an axis-based cylindrical or near-cylindrical feature, usually for:

- fasteners;
- clearance;
- threads/tapping;
- pins/dowels;
- locating;
- fluid/air path;
- tooling/access.

The key evidence is not merely round geometry. The key evidence is axis-based functional intent.

### Slot

A slot is an elongated opening, often two rounded ends plus straight sides, usually for adjustment, travel, or clearance. A slot's rounded end contains cylindrical or arc geometry, but that does not make it a hole. The semantic feature is the slot unless there is separate evidence that the rounded end is itself a functional hole.

### Pocket

A pocket is an area or volume removal, often not primarily axis/fastener based. It may have circular corners, cutter-radius corners, islands, floors, and walls. Those cylindrical side faces are normally pocket evidence, not hole evidence.

### Generic cylindrical cut

A generic cylindrical cut is a low-level geometry operation or fallback when semantic hole intent is absent. It can be the right representation for an arbitrary round recess, a test cut, imported geometry with insufficient context, or a cylindrical negative shape that does not behave like a functional hole.

State the negative tests clearly:

- a slot's rounded end contains cylindrical/arc geometry, but that does not make it a hole;
- a cylindrical boss contains cylindrical faces, but that does not make it a hole;
- a fillet can contain cylindrical faces, but that does not make it a hole;
- a decorative circular recess is not a hole unless there is evidence for hole intent.

## 4. Hole variants and axial stacks

Many holes are axial feature stacks. The source-level concept is one semantic feature, while the lowered geometry may contain several coaxial pieces.

### Through hole

- cylindrical shaft passes through the body;
- end condition reaches another face or is explicitly through all;
- often used for clearance, pins, fasteners, or fluid/air passage.

### Blind drilled hole

- cylindrical shaft stops inside the body;
- may include a conical drill tip;
- author may specify depth and process, not manually draw the cone unless the cone itself is design-critical.

A blind hole is not merely a shallow cylinder Boolean. If it is drilled, the conical tip can be a process implication. The source should preserve that the feature is a blind drilled hole, not an arbitrary cylinder plus cone subtraction unless lowered geometry is the only defensible evidence.

### Counterbore

- larger entry cylinder or shallow pocket;
- smaller main hole below;
- flat shoulder between the entry cylinder and shaft;
- often used for socket head screws, washers, or recessed fastener heads.

### Countersink

- conical entry;
- main hole below;
- angle matters;
- often used for flat head screws or deburring-like entry geometry when the functional intent is part of the hole.

### Tapped/threaded hole

- tap drill or core diameter;
- thread standard, size, pitch, and depth;
- thread may be represented as metadata, cosmetic/thread annotation, or explicit geometry depending on the target.

The important semantic fact is not only the cylinder diameter. It is that the hole receives or represents a thread.

### Clearance hole

- standard/fit-based diameter, such as an M30 clearance hole;
- manufacturing/assembly intent matters more than raw cylinder diameter;
- exact diameter may derive from a standard, fit class, or local design convention.

A hole feature may lower to a sequence of cylinders, cones, chamfers, and thread annotations. The source should preserve hole semantics so later tools can know that these pieces belong together.

## 5. Placement semantics: where does a hole live?

A hole needs stable placement. LLMs should ask how the feature is located before emitting a cylinder cut. Placement may involve a face, a face-local coordinate frame, datums, axes, patterns, or a group table. Good placement avoids fragile generated edges, especially filleted edges that may disappear or change when edge finish changes.

The following examples are design sketches. They are not implemented Firmament syntax and should not be treated as language guarantees.

### Style A — face-local placement

```firmament
hole<clearance> mountA {
    on: top
    center: [120, 40]
    diameter: 10mm
}
```

Critique:

- readability: high for plate-like and prismatic parts;
- semantic preservation: good when `top` is a stable named face and the hole axis is normal to that face;
- edit locality: good if moving the hole only changes the local center;
- lowering difficulty: moderate, because the compiler must define the face-local frame and normal direction;
- decompilation friendliness: good when a planar entry face and circular intersection are obvious;
- failure modes: ambiguous face frames, renamed faces, face splits, or accidental references to generated faces.

### Style B — datum/axis placement

```firmament
hole<tapped> clampScrew {
    axis: datumAxis("clampScrewAxis")
    entry: top
    thread: M6x1
    depth: 12mm
}
```

Critique:

- readability: clear for important holes once datums are named well;
- semantic preservation: strong, because the hole's location/orientation is separated from face identity;
- edit locality: strong for assemblies and functional interfaces where datums are stable;
- lowering difficulty: higher, because datum/axis syntax and validation must exist;
- decompilation friendliness: good if feature trees, PMI, names, or repeated axes expose datum intent;
- failure modes: over-modeling trivial holes, unclear datum ownership, or missing entry-face/material intersection rules.

### Style C — pattern placement

```firmament
pattern linear boltRow {
    seed: hole<clearance> {
        on: top
        center: [-60, 0]
        fit: M8
    }
    count: 4
    spacing: [40, 0]
}
```

Critique:

- readability: high when repetition is regular;
- semantic preservation: strong, because the repeated-feature intent survives;
- edit locality: strong, because count and spacing can change without editing each hole;
- lowering difficulty: moderate to high, because pattern semantics must replicate and validate features;
- decompilation friendliness: strong when centers are collinear, circular, rectangular, or otherwise regular;
- failure modes: forcing an imperfect real layout into a false pattern, or losing per-instance overrides.

### Style D — hole table / hole group

```firmament
holeGroup<clearance> mountingHoles {
    on: top
    fit: M10
    centers: [
        [-100, 50],
        [100, 50],
        [-100, -50],
        [100, -50]
    ]
}
```

Critique:

- readability: compact for repeated but not perfectly patterned holes;
- semantic preservation: good because common role, fit, face, and end condition can be shared;
- edit locality: good for adding/removing centers, weaker for parametric relationships than a pattern;
- lowering difficulty: moderate, mostly repeated validation of one feature template;
- decompilation friendliness: good when holes share diameter, face, and role but do not form a clean pattern;
- failure modes: hiding a true pattern in a table, or grouping unrelated holes only because their diameters match.

### Style E — low-level cylinder cut

```firmament
cut Cylinder {
    on: top
    center: [120, 40]
    radius: 5mm
    depth: 12mm
}
```

Critique:

- readability: acceptable for simple geometry, but weak for design intent;
- semantic preservation: low, because fastener, thread, fit, counterbore, and pattern meaning are absent;
- edit locality: weak when the feature later becomes a standard or patterned hole;
- lowering difficulty: easiest, because it is already close to kernel operations;
- decompilation friendliness: poor as a target, because it does not explain why the cylindrical volume exists;
- failure modes: normalizing all holes into anonymous booleans and losing manufacturing meaning.

### Style F — reference-grid or sketch-derived placement

```firmament
hole<dowel> locatePin {
    on: top
    center: intersection(gridLine("A"), gridLine("3"))
    diameter: 6mm
    end: depth 10mm
}
```

Critique:

- readability: good when the part has a real layout grid or named construction references;
- semantic preservation: good for locating and fixture holes tied to design datums;
- edit locality: good if the grid moves and dependent holes follow;
- lowering difficulty: higher, because construction references must resolve before the hole;
- decompilation friendliness: limited unless the original feature tree or PMI exposes the construction references;
- failure modes: inventing a grid after the fact without evidence, or anchoring to references less stable than a face-local coordinate.

## 6. Through vs blind should usually be derived

It often does not make sense to manually declare a magic Boolean pair:

```text
blind: true
through: false
```

Those flags describe a result. Source should usually describe the cause:

- entry face;
- axis/direction;
- depth or termination;
- target face, through all, up to next, or up to face.

Then the compiler can determine whether the feature:

- is through;
- is blind;
- intersects body material;
- exits multiple bodies;
- is invalid because it removes no material;
- implies a drill tip because a blind drilled process applies.

Candidate end-condition styles:

```firmament
end: throughAll
end: depth 12mm
end: upToFace bottom
end: upToNext
```

Critique:

- `throughAll` is readable and robust for plates, but needs clear behavior for multi-body or assembly contexts;
- `depth 12mm` is direct and manufacturing-friendly, but the blind/through result depends on body thickness along the axis;
- `upToFace bottom` preserves design intent when a target face is stable, but can become fragile if the target face is generated or split;
- `upToNext` adapts to thickness changes, but requires careful definition when multiple candidate faces exist.

If the hole is blind because of depth, the blindness is a consequence. If the author wants manufacturing drill-tip behavior, that should be process or variant information, not an accidental cone Boolean.

## 7. Counterbore/countersink declaration shape

Several declaration shapes may be useful at different abstraction levels. These are strategy sketches, not implemented syntax.

### Generic parameterized hole type

```firmament
hole<counterbore> mount {
    on: top
    center: [0, 0]
    shaftDiameter: 8mm
    counterboreDiameter: 14mm
    counterboreDepth: 5mm
    end: throughAll
}
```

Critique:

- concise and readable for common variants;
- preserves the fact that the larger entry cylinder and smaller shaft are one feature;
- easy for LLMs to select when the role is obvious;
- can become crowded as variants accumulate many parameters;
- may require variant-specific validation rules.

### Hole with stack components

```firmament
hole mount {
    on: top
    center: [0, 0]
    stack {
        counterbore diameter: 14mm depth: 5mm
        shaft diameter: 8mm end: throughAll
    }
}
```

Critique:

- expressive and close to lowering without losing the parent hole identity;
- handles unusual axial combinations better than one parameter list;
- makes stack order explicit;
- more verbose for ordinary holes;
- risks becoming primitive soup if the parent hole role and standards are omitted.

### Standard/library hole

```firmament
hole<clearance> mount {
    standard: ISO
    fit: M8
    on: top
    center: [0, 0]
    end: throughAll
}
```

Critique:

- captures manufacturing and assembly intent;
- lets size derive from standards rather than magic numbers;
- improves review because readers see `M8 clearance`, not just a diameter;
- requires a trustworthy standard/library model;
- must expose enough overrides for local practice without hiding critical dimensions.

Conceptual recommendation: Firmament may need all three levels over time. Generic hole variants are concise, stack components are expressive, and standard/library holes preserve manufacturing intent. None of these examples is a syntax commitment.

## 8. Decompilation rules for holes

When seeing cylindrical or conical faces, an LLM should ask:

1. Is it a functional hole or a round/fillet/slot/boss?
2. Does it pass through?
3. Does it terminate inside with a drill-tip-like cone?
4. Is there an entry cone or counterbore cylinder?
5. Is there a thread/standard indication in names, PMI, feature tree, drawings, or adjacent metadata if available?
6. Is it repeated or patterned?
7. Is it placed relative to a stable datum or face?
8. Should it be grouped with other holes?

Suggested output shape:

```text
Hole candidate:
  role:
  entry face:
  axis:
  center/reference:
  diameter/standard:
  end condition:
  stack components:
  group/pattern:
  confidence:
  not-a-hole alternatives:
```

The `not-a-hole alternatives` line is important. It prevents the LLM from treating every cylindrical face as a hole and gives reviewers a place to see why slot end, fillet, boss, pocket corner, or decorative recess interpretations were rejected.

## 9. Firmament recommendation

Firmament should conceptually treat holes as first-class semantic features. `cut Cylinder` should remain possible as a fallback or escape hatch, but it should not be the default representation for holes when hole intent is available.

Recommended direction:

- preserve hole variants such as clearance, counterbore, countersink, tapped/threaded, dowel, reamed, and drilled holes as design/manufacturing intent;
- derive through/blind classification from entry, axis, body intersection, and end condition where possible;
- prefer stable faces, datums, axes, and patterns over generated edges for placement;
- make hole groups and patterns first-class enough for decompilation targets;
- keep lowered cylinder/cone/chamfer/thread geometry attached to the parent hole when possible;
- use generic cylindrical cuts only when semantic evidence is insufficient.

This note does not claim that these capabilities already exist.

## 10. Relationship to previous lessons

This lesson follows the same progression as the earlier strategy notes.

- In `04-spatial-decomposition-first-pass.md`, the LLM first identifies major blocks, volumes, and spatial roles. Holes usually belong after blockout and before late edge finish because they modify functional volumes and interfaces.
- In `05-cad-as-semantic-dependency-graph.md`, holes are functional children in the dependency graph. A mounting hole may depend on a plate face, datum layout, or bolt pattern, and downstream chamfers or thread annotations may depend on the hole.
- In `06-profiles-as-constructive-regions.md`, raw boundary curves are treated as lowered forms of profile intent. Similarly, raw cylinder/cone Boolean stacks are lowered forms of hole intent.

Hole chamfers, countersinks, counterbores, drill tips, and thread extents are not the same as unrelated late edge finish. When they are part of the axial structure of the hole, they should remain attached to the hole feature rather than being decompiled as independent decorative rounds or arbitrary cuts.

## 11. Common failure modes

- treating every cylindrical face as a hole;
- treating every hole as a cylinder subtract;
- hiding hole patterns as unrelated individual cuts;
- manually tagging through/blind instead of deriving from end condition;
- ignoring counterbore/countersink/thread semantics;
- confusing slot ends with holes;
- confusing fillet cylinders with holes;
- confusing cylindrical bosses with holes;
- treating decorative circular recesses as holes without evidence;
- locating holes from fragile filleted edges;
- losing manufacturing standard/fit semantics;
- splitting one axial hole stack into unrelated primitive booleans;
- inventing hole standards or pattern intent when the evidence is too weak.

## 12. Non-goals

This note explicitly does not include:

- parser changes;
- syntax guarantees;
- lowering implementation;
- hole wizard implementation;
- manufacturing standard library implementation;
- AIR changes;
- BRep changes;
- STEP import/export changes;
- DisplayIR changes;
- tessellation changes;
- frontend behavior changes;
- product behavior changes;
- implementation tests.
