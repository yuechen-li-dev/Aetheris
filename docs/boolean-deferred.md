# Boolean deferred pile (safe-family snapshot)

This note records the current boolean scope so deferred items can be revisited later without losing context.

## Current supported subset (safe family)

The current supported boolean subset is intentionally narrow and family-specific:

- box − cylinder through-hole
- box − cone through-hole
- box − cylinder blind hole
- box − cone blind hole
- box − box blind pocket and opposite-face through-slot (bounded orthogonal pocket family)
- box − sphere cavity (single-sphere only): fully enclosed cavity and one-sided top/bottom exterior-opening spherical pocket
- cylinder-root − coaxial center bore (through)
- cylinder-root − coaxial blind bore from top/bottom (single-bore bounded family)
- rotated cylinder through-hole
- safe-family composition for supported analytic-hole families, including independent world-Z multi-hole continuation on simple box roots
- recognized safe box-root subtract with bounded prismatic through-cut tools (triangular/hexagonal/slot profiles) when the root has no prior subtract history
- face-contact orthogonal unions even when no axis has full-span equality (bounded connected-cell family)

## Current deferred boolean pile

The following items remain deferred:

- rotated cone through-hole
- rotated blind cylinder hole
- rotated blind cone hole
- arbitrary-axis blind-hole composition chaining
- torus booleans
- broad add/intersect expansion outside the current narrow box-box subset
- general BRep booleans
- bounded mixed analytic-hole + prismatic continuation rebuild path on safe box roots (recognition now exists; bounded reconstruction remains deferred)
- safe-composition prismatic blind-pocket continuation (recognition-classification gap + bounded reconstruction path still deferred)
- multi-sphere and mixed sphere+prismatic continuation in one subtract chain

## Known blocker: rotated cone through-hole

Rotated cone through-hole is deferred due to a **section-curve representation mismatch in the builder/export path**.

## Architectural framing

- The current boolean system is a **family-specific safe boolean framework**.
- It is **not** a general boolean engine.
- Deferred items should be revisited only if product scope or real usage justifies them.

## Return criteria

Revisit this deferred pile when at least one of these is true:

- Real product usage hits a blocked case.
- A milestone explicitly targets widening the safe family.
- There is an explicit decision to invest in more general boolean foundations.
