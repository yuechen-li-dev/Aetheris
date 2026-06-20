# 02 — Prismatic CAD First Principles

Core lesson: for many CNC and prismatic mechanical objects, a useful first approximation is:

```text
Blocks + cuts + holes + edge finish.
```

This is not the only CAD method. It is a strong default hypothesis for many machined, fixture-like, bracket-like, and plate-like parts.

## Start from simple masses

Prefer simple, stable construction primitives early:

- Boxes.
- Bosses.
- Pockets.
- Slots.
- Through-holes and blind holes.
- Simple additive and subtractive volumes.

Build the gross form before details. A clear blockout often gives later features better references and makes intent easier to inspect.

## Prefer prismatic tools before arbitrary sketches

When a block, cut, slot, or hole expresses the same intent as a complex profile, prefer the semantic feature. A simple 3D boolean over prismatic tools can often be treated like a constrained 2D boolean internally, without exposing arbitrary sketch complexity in source.

This helps an LLM produce source that is closer to how a CAD user would maintain the model.

## Why arbitrary sketch-first modeling is dangerous

Sketch-first modeling can be powerful, but using it as the default escape hatch creates risk:

- Self-intersection.
- Ambiguous regions.
- Loop closure problems.
- Coincident geometry.
- Constraint-solving complexity.
- Fragile feature references.
- Difficult LLM decompilation.

A single heroic sketch may match the silhouette while destroying the recoverable authoring structure.

## Admit sketches deliberately

This does not ban sketches or profiles. It means they should be admitted deliberately when the part actually needs profile-level expressiveness.

Good reasons to use sketches include:

- A non-prismatic outline is genuinely central to the design.
- A profile captures intent better than a set of boxes and cuts.
- The language and kernel can represent the profile robustly.
- The LLM can explain the references, dimensions, and ambiguity.

## Edge finish comes late

Fillets, chamfers, rounds, and other edge finishes usually belong near the end of the feature tree. They decorate or condition already-defined functional edges. Applying them too early can obscure the main shape spine and create fragile downstream references.
