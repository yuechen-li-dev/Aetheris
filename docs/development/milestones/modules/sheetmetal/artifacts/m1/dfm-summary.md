# M1 DFM summary

The typed `SheetMetalDfmPolicy` exposes provisional, parameterized checks only:

- thickness > 0;
- inside bend radius / thickness ratio (default minimum 1.0);
- cut-center to bend-line distance (default 2× thickness);
- flat planar-region overlap.

The authored fixture passes all applicable rules. CTC-03 has R/T = 3.3289 and no detected flat overlap; both mapped openings exceed the provisional hole-to-bend threshold. These are geometry-policy observations, not universal shop limits. Cut-to-edge distance has a policy field but awaits exact stitched blank ownership before it can be evaluated honestly.
