# 06 — Profiles as constructive regions

## 1. Purpose

This lesson teaches LLMs how to think about 2D profile authoring and 2D profile decompilation in Firmament V2 work.

The lesson is philosophical and strategic, not a syntax implementation. It is meant to guide future Firmament V2 authoring, review, and semantic decompilation so an LLM asks for the intended 2D region structure before writing down a boundary trace.

This note does not authorize parser changes, lowering changes, Firmament V2 syntax changes, AIR changes, BRep changes, STEP import/export changes, DisplayIR changes, tessellation changes, frontend changes, or product behavior changes.

## 2. The core problem with raw line/arc chains

Arbitrary `Line(...)`, `Arc(...)`, `Line(...)`, `Arc(...)` authoring is a poor default model for 2D profiles because it starts from a representation that is already close to the compiler's lowered boundary form.

A boundary chain can be exact, but it usually does not say why the shape exists. It records local geometric facts: this point connects to that point; this tangent arc has that radius; this loop closes with this orientation. It does not say whether the profile is a base plate with a tab, a stock rectangle with a corner relief, a pair of overlapping lobes, or a slot cut from a web.

For LLMs, this is especially fragile:

- line/arc chains are already lowered boundary form;
- they lose semantic meaning such as stock, tab, relief, slot, and lobe intent;
- they are hard to read without executing, rendering, or mentally plotting the loop;
- they encourage premature sketch-solver complexity;
- they invite self-intersection, ambiguous loops, coincident geometry, reversed orientation, and fragile constraints;
- they make edit intent local to arbitrary vertices instead of named regions;
- they are difficult for LLMs to reason about reliably from text alone.

```text
Line/Arc/Line/Arc is profile assembly language.
Useful and sometimes necessary, but not the default authoring language.
```

The problem is not that boundary chains are invalid. The problem is making them the first explanation when a constructive explanation is available.

## 3. Boundary representation vs profile intent

A 2D profile has at least two useful levels of description.

```text
Boundary representation:
  line segment
  arc segment
  spline segment
  closed loop
  orientation
  trim

Profile intent:
  base rectangle
  tab
  slot
  hole
  relief cut
  rounded corner
  clipped corner
  union
  subtract
```

Boundary representation is what the compiler may lower to after resolving the high-level region. It is appropriate for boundary extraction, downstream kernels, display, validation, import/export, and escape hatches.

Profile intent is what a human or LLM should author when possible. It preserves the modeling reason for the outline: which simple regions were combined, which regions were removed, which corners are intentionally rounded or clipped, and which references are stable enough to edit later.

Semantic decompilation should therefore try to recover profile intent, not only boundary loops. A decompiler that returns only a closed loop may be geometrically correct while still losing the construction strategy that makes the part legible.

## 4. Constructive 2D region model

The preferred high-level model is constructive:

```text
profile = union(simple regions) - subtract(simple regions) + profile-corner operations
```

Candidate admitted region primitives include:

- rectangle;
- circle;
- triangle;
- slot/capsule;
- rounded rectangle;
- polygon, only when constrained and simple;
- maybe half-space/cut-plane later;
- maybe imported boundary only as an escape hatch.

Candidate operations include:

- union;
- subtract;
- intersect, only if clearly needed later;
- mirror/pattern of 2D regions;
- profile-round;
- profile-chamfer / clipped corner;
- named anchor points and named edges.

This is conceptual guidance, not parser-backed syntax. The goal is to keep source-level thinking close to blockout intent: start with simple regions, combine or remove them, then apply profile-level cornering where it belongs.

## 5. Try several possible Firmament-like representations

The following examples are design sketches. They are not implemented syntax and should not be treated as Firmament V2 language guarantees. Their purpose is to compare authoring models and decompilation targets.

### Style A — raw boundary chain

```firmament
profile stepPlate on XY {
    boundary {
        line from [0, 0] to [0, 80]
        line from [0, 80] to [40, 80]
        line from [40, 80] to [40, 40]
        line from [40, 40] to [90, 40]
        line from [90, 40] to [90, 0]
        line from [90, 0] to [0, 0]
    }
}
```

Critique:

- readability: readable for very small examples, but the reader must plot the points to see the shape;
- semantic preservation: low, because the text does not say that this is an L-shaped plate or which dimensions belong to which conceptual region;
- edit locality: weak, because changing the upright or base requires editing shared vertices and maintaining closure;
- lowering difficulty: easiest if the boundary is valid, because it is already close to the lowered form;
- LLM decompilation friendliness: poor as a default, because the LLM may output precise-looking coordinates without explaining the construction;
- failure modes: self-intersection, missing closure, reversed orientation, coincident segments, duplicate edges, fragile point constraints, and loss of intent.

This style is acceptable as an escape hatch or lowered representation, not as the default authoring language.

### Style B — constructive union of rectangles

```firmament
profile stepPlate on XY {
    union {
        rect base {
            origin: [0, 0]
            size: [90, 40]
        }

        rect upright {
            origin: [0, 40]
            size: [40, 40]
        }
    }
}
```

Critique:

- readability: high, because the L shape is visibly two rectangles;
- semantic preservation: high for blockout intent, because `base` and `upright` name the conceptual regions;
- edit locality: strong, because the base and upright dimensions can change independently while the boolean region remains the source of truth;
- lowering difficulty: moderate, because the compiler must perform a 2D region union and extract boundary loops;
- LLM decompilation friendliness: high, because the LLM can explain the shape as combined simple regions before lowering it;
- failure modes: ambiguous overlaps, unintended slivers, or unclear ownership of shared edges if names and anchors are weak.

This style reveals L-shape intent, avoids a self-intersecting loop to solve, and is easier for humans and LLMs to edit.

### Style C — base rectangle minus relief rectangle

```firmament
profile stepPlate on XY {
    region stock: rect {
        origin: [0, 0]
        size: [90, 80]
    }

    subtract {
        rect upperRightRelief {
            origin: [40, 40]
            size: [50, 40]
        }
    }
}
```

Critique:

- readability: high when the mental model is stock plus removed corner;
- semantic preservation: high for subtractive blockout, because the removed region is named as a relief;
- edit locality: strong, because the removed region can move or resize without rewriting an outer loop;
- lowering difficulty: moderate, because subtraction must produce valid loops and orientations;
- LLM decompilation friendliness: high when evidence suggests a full stock envelope with a notch, relief, or removed quadrant;
- failure modes: subtractive regions that do not overlap stock, ambiguous partial overlaps, accidental deletion of the whole profile, or unclear whether the relief is functional or merely a convenient decomposition.

This style often matches how a designer thinks: start with stock, remove this corner.

### Style D — named regions plus cornering

```firmament
profile bracketEnd on XY {
    union {
        rect stem { center: [0, 0], size: [40, 120] }
        circle lobe { center: [0, 60], radius: 20 }
    }

    profileRound {
        corner stem.bottomLeft radius: 8
        corner stem.bottomRight radius: 8
    }
}
```

Critique:

- readability: high for lobes, rounded ends, and explicit silhouette cornering;
- semantic preservation: high, because it distinguishes the stem, the lobe, and the profile-level rounded corners;
- edit locality: strong if region and corner references are stable;
- lowering difficulty: higher than simple unions, because corner references must resolve against the constructed 2D region;
- LLM decompilation friendliness: high when the arc is part of the 2D silhouette rather than a late 3D edge finish;
- failure modes: unstable generated corner names, corner references that disappear after boolean operations, and confusion between 2D profile cornering and 3D fillet/chamfer decorators.

This style is useful precisely because it distinguishes silhouette construction from later 3D edge finish.

### Style E — declarative profile feature graph

```firmament
profile mountingPlate on XY {
    shape spine {
        rect main { center: [0, 0], size: [800, 120] }
        rect leftTower { center: [-350, 0], size: [100, 450] }
        rect rightTower { center: [350, 0], size: [100, 450] }
    }

    reliefs {
        cut rect upperMiddle { center: [0, 160], size: [420, 80] }
        cut rect lowerMiddle { center: [0, -160], size: [420, 80] }
    }

    corners {
        round leftTower.top radius: 50
        round rightTower.top radius: 50
    }
}
```

Critique:

- readability: very high when the profile has a clear spine, reliefs, repeated structures, and named corner operations;
- semantic preservation: very high, because it records the modeling strategy rather than only the resulting outline;
- edit locality: strong if each named feature owns a coherent dimension or reference;
- lowering difficulty: higher, because this requires a richer profile feature graph and reference model;
- LLM decompilation friendliness: excellent as a candidate notation for explaining recovered intent;
- failure modes: may be too high-level for a first parser, may overfit ambiguous shapes, and may imply feature categories that are not actually supported.

This style is expressive as a decompilation candidate, even if it is too high-level for early parser work.

### Style F — low-level boundary escape hatch

```firmament
profile customEscape on XY {
    boundary unsafe {
        // explicit line/arc loop
    }
}
```

Critique:

- readability: depends entirely on the boundary contents;
- semantic preservation: intentionally low unless comments or names add intent;
- edit locality: weak for ordinary blockout geometry, acceptable for imported or highly specialized curves;
- lowering difficulty: low if valid, difficult if validation must repair malformed loops;
- LLM decompilation friendliness: appropriate only after constructive explanations fail;
- failure modes: normalized garbage, hidden self-intersections, accidental reliance on point order, and false confidence from precise coordinates.

This style is needed for advanced or imported cases, but it should be marked unsafe, low-level, or explicit. It should not be the default output of LLM decompilation unless no constructive explanation is available.

### Style G — constructive regions with repeated 2D features

```firmament
profile ventedPanel on XY {
    region stock: rect { center: [0, 0], size: [220, 120] }

    subtract pattern slotRow {
        count: 5
        spacing: [35, 0]
        seed: slot vent { center: [-70, 0], size: [18, 80], endRadius: 9 }
    }
}
```

Critique:

- readability: high when repeated slots, holes, or reliefs are part of the profile;
- semantic preservation: high, because it records the pattern rather than five unrelated loops;
- edit locality: strong, because count, spacing, and seed geometry are separate knobs;
- lowering difficulty: moderate to high, because pattern expansion must feed a 2D region subtraction;
- LLM decompilation friendliness: high when repetition is visible and functionally meaningful;
- failure modes: wrong seed inference, missing symmetry assumptions, overlapping pattern members, and ambiguity about whether the pattern belongs in the 2D profile or a later 3D cut feature.

This style is useful when repeated 2D voids are genuinely part of the profile silhouette or through-profile cut pattern, but it should not be used to hide unrelated details inside one overloaded profile.

## 6. Profile cornering vs 3D edge finish

Do not conflate profile-level cornering with 3D edge finish.

Profile cornering modifies a 2D region before extrusion. It defines the silhouette or trunk shape. It belongs in the same conceptual layer as the profile blockout because it changes what gets extruded.

Examples of profile cornering include:

- rounded end of a slot;
- clipped outside corner of a plate;
- lobe radius;
- profile relief corner;
- rounded rectangle corner in a 2D plate outline.

3D edge finish modifies final or near-final 3D edges after extrusion, cuts, bosses, holes, and other functional geometry exist. It is usually a decorator layer, not the source of the part's massing strategy.

Examples of 3D edge finish include:

- small chamfer on a hole rim;
- fillet on an external edge;
- deburr radius;
- stress-relief edge round;
- cosmetic round added after the functional shape is complete.

Traditional CAD may expose both through similar sketch, fillet, chamfer, or round tools. That user-interface similarity should not drive decompilation. The same geometric-looking arc may be trunk-level profile intent or late edge finish depending on its dependency role.

If the arc controls the 2D silhouette before extrusion, treat it as profile-level cornering. If it decorates an edge created after functional geometry exists, treat it as 3D edge finish. When uncertain, state the ambiguity rather than flattening both into one generic fillet concept.

## 7. Recommended default for Firmament V2

Firmament V2 strategy should prefer constructive 2D region profiles as the high-level model.

Recommended default:

- express 2D profiles as unions and subtractions of simple named regions when possible;
- use raw line/arc boundary chains as an escape hatch or lowered form, not the default authoring model;
- make profile cornering explicit and separate from 3D edge finish;
- have decompilation candidates first try to explain 2D profiles as simple regions that were combined, removed, mirrored, patterned, rounded, or clipped.

This is a recommendation for strategy and future design discussion. It is not a claim that the syntax or lowering described here is implemented.

## 8. Implications for AIR/lowering

High-level constructive regions can lower internally to 2D boolean regions. The compiler can then produce boundary loops, line segments, arc segments, orientations, trims, and any other lower-level form required by AIR, kernels, display, or export.

The source language does not need to expose all low-level loops first. A high-level source profile can preserve intent while still compiling to exact boundary data.

This also localizes failures:

- invalid region operation;
- overlapping ambiguous regions;
- unsupported corner reference;
- unsupported primitive;
- pattern expansion conflict;
- boundary extraction failure.

Those errors are easier to diagnose than a generic malformed sketch loop because they point back to named regions and operations.

This avoids turning the source language into a sketch constraint solver too early. Constraints may still matter, but blockout geometry should not require a general-purpose sketch solver when constructive region composition would express the intent more directly.

## 9. LLM decompilation rule

```text
When decompiling a 2D profile, first ask:
  What simple regions were likely combined or removed?
Only use raw line/arc loops after constructive explanations fail.
```

Use this output template when proposing a 2D profile interpretation:

```text
Profile intent:
  base regions:
  subtractive regions:
  repeated/mirrored regions:
  profile-level cornering:
  low-level boundary escape hatches:
  ambiguity:
  recommended Firmament representation:
```

The template is meant to force the LLM to explain intent before emitting boundary mechanics. It also makes uncertainty visible.

## 10. Common failure modes

Common LLM and authoring failures include:

- outputting line/arc chains because they look precise;
- hiding slots inside arbitrary sketches;
- treating profile rounds as 3D fillets;
- treating late fillets as profile arcs;
- overusing sketch constraints for blockout geometry;
- failing to name regions;
- losing edit locality;
- combining unrelated cuts into one overloaded profile;
- mistaking an imported boundary for the original modeling strategy;
- presenting design sketches as implemented syntax.

Precision is not the same as intent. A coordinate-perfect loop can still be a poor source model if it destroys the shape's editable structure.

## 11. Relationship to earlier lessons

This lesson extends the spatial decomposition approach from [04 — Spatial decomposition first pass](04-spatial-decomposition-first-pass.md). Constructive profiles are part of the shape spine and blockout layer: they help an LLM decide what the gross 2D mass is before adding smaller children and finish details.

## Section-stack composition

When several named profiles describe one stepped prismatic body, author one shared Concept scaffold, inspect every Profile, then declare explicit `Base`, `Add`, and `Remove` axial intervals in `Compose`. Inspect the normalized section stack before building. The compiler must resolve material per open Z interval and emit one final topology plan; do not construct temporary solids and ask generic 3D Booleans to recover the intended topology afterward. See [profile composition X1](../firmament/profile-composition-x1.md) for the admitted contact policy and current verification boundary.

It also follows [05 — CAD as semantic dependency graph](05-cad-as-semantic-dependency-graph.md). Raw boundaries are lowered forms. Constructive profile regions belong before extrusion when they define the trunk shape. Profile cornering belongs before extrusion when it changes the silhouette. 3D edge finish remains a late decorator in the dependency graph.

The dependency question is therefore: does this 2D curve define the region that becomes mass, or does it decorate an edge after mass exists? Answer that before choosing profile syntax.

## 12. Non-goals

This note explicitly does not require or authorize:

- parser changes;
- syntax guarantees;
- lowering implementation;
- sketch solver work;
- AIR changes;
- BRep changes;
- STEP import/export changes;
- DisplayIR changes;
- tessellation changes;
- frontend behavior changes;
- product behavior changes;
- implementation tests;
- a claim that raw boundary loops are never useful;
- a claim that constructive regions are universal for every profile.

Raw boundary loops remain necessary for some advanced, imported, or currently unsupported cases. The recommendation is only that they should be explicit low-level escape hatches rather than the first tool an LLM reaches for when a constructive profile explanation is available.
