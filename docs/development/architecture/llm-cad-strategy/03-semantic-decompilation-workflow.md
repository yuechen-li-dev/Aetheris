# 03 — Semantic Decompilation Workflow

Semantic decompilation tries to recover a plausible authoring strategy from final geometry and inspection evidence. The output should separate facts, interpretation, confidence, and missing capabilities.

## Practical workflow

1. Establish the coordinate frame and bounding box.
2. Identify the main mass or blockout.
3. Identify major subtractive cuts.
4. Identify secondary additive masses.
5. Identify holes, slots, pockets, and repeated features.
6. Identify symmetry, mirror, and pattern structure.
7. Identify edge finishing.
8. Produce a candidate feature tree in resilient order.
9. Separate high-confidence, medium-confidence, and speculative features.
10. Produce a missing capability matrix.

## Shape spine vs. detail features

Use a strong distinction between the shape spine and detail features.

### Shape spine

The shape spine is the minimum stable sequence needed to create the main body. It usually includes the base mass, major additions, and major removals that define the part’s silhouette and functional envelope.

### Detail features

Detail features refine the spine. They include holes, slots, pockets, repeated features, fillets, chamfers, rounds, and edge finishing.

Do not let detail features obscure the main construction order. A model with many holes may still have a simple spine.

## Expected report format

Future decompilation reports should use this structure unless a task asks for a narrower format:

```text
- backend facts
- visual/design interpretation
- shape spine
- candidate feature tree
- feature decomposition table
- missing capability matrix
- confidence/ambiguity notes
- recommended implementation milestones
```

## Confidence discipline

Do not present every recovered feature as certain. Use confidence bands:

- High confidence: directly supported by backend facts and visible design structure.
- Medium confidence: plausible and useful, but not uniquely determined.
- Speculative: a candidate strategy that needs more evidence or capability work.

When two strategies create equivalent geometry, prefer the one that better preserves stable references and design intent, but record the ambiguity.
