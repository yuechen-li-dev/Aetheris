# STEP Tools "Boxy" import: two outer bounds on cylindrical bands

Reported on Hacker News: `boxy_with_surfacetex.stp` (STEP Tools' ST-Developer v14, 2010, AUTOMOTIVE_DESIGN) did not
import.

## Cause

`Importer.LoopRole.DuplicateDeclaredOuter.Face:202`: the importer requires exactly one `FACE_OUTER_BOUND` per face.
30 cylindrical faces in the file each declare both of their bounds outer. Each bound is a single-edge full circle,
so the face is a band. The schema allows at most one outer bound, so the file breaks that rule. But on a band
neither circle bounds the other, and nothing tells a writer which one to call outer. The two declarations carry no
role and do not contradict each other.

## Change

When a face declares more than one outer bound and every bound is a full revolution of a cylinder or cone at one
axial station (`FullRevolutionConstantAxial`), the bounds are kept as written: same order, same traversal. The first
declared bound stays outer and the rest become inner. The body therefore has exactly one outer bound, and so does
its export. Each such face reports an Info diagnostic, `Importer.LoopRole.BandRingsBothDeclaredOuter`. Every other
case is still refused, including a planar face with two outer bounds.

## Evidence

- The file imports as Qualified: an enclosed manifold, 80 faces, 124 edges, 112 vertices, no missing pcurves. It is
  bit-identical in analysis to a hand-edited copy where the second outer bound of each band is demoted to
  `FACE_BOUND`.
- An independent kernel (springrep) reads both the source and Aetheris's export with 0 issues. Both have 80 faces,
  area 48826.636696 mm² and volume 174839.178053 mm³, so the export preserves the geometry exactly. Every exported
  face has exactly one `FACE_OUTER_BOUND`.
- Tests in `Step242BandRingOuterBoundTests`:
  - import, then one outer bound per exported face, then a reread with the same topology and surfaces;
  - the planar double-outer case is still refused.
  - The file is in `testdata/step242/steptools/` with provenance.

## Found while there

`BrepMassProperties` integrates this body at 175037.29 mm³, which is 0.11% above the exact 174839.18. That is inside
its reported error bound, but the bound is loose enough that a volume check on curved bodies cannot catch an error
this size. The reread integrates to 175037.17. Exact checks on curved imports should use the independent reader or
topology and geometry equality, not this volume.

## Verdict

Success. The reported file imports, and it round-trips exactly as measured by an independent reader.
