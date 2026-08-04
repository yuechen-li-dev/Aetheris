# M8-HOLE-LOOP-X1

## Doctrine

**Edge sense is interpreted once. Every later layer consumes the same directed topology contract.**

`Edge.StartVertexId -> Edge.EndVertexId` is the canonical topology direction. A coedge reverses that pair exactly once through `DirectedEdgeUse.Resolve`. `EDGE_CURVE.same_sense` is curve-parameter sense only; it does not alter the topology edge or an `ORIENTED_EDGE` traversal. `ADVANCED_FACE.same_sense` is material-facing surface orientation only and never changes connectivity.

A loop is connected when the end of each resolved use matches the next resolved use, including closing back to the first. Binding-aware gates also admit coincident, distinct vertices at a periodic seam. The currently supported circle forms are a one-edge full circle with coincident endpoints and the existing seam-plus-full-circle wall representation. Split arcs must be explicitly chained; unordered shared incidence is insufficient.

Planar loop role and winding are diagnostic/orientation concerns. The importer preserves STEP `EDGE_LOOP` order rather than rewriting coedges to normalize projected winding. Producers own cap outer/inner loop order; exporters serialize it verbatim.

## CTC-01 evidence

The original artifact (`202d059...3937`) was reported `enclosed-manifold` by the analyzer because it counted edge-to-face uses only. M8 correctly found broken ordered wall loops. The producer emitted each hole wall as `seam, top-circle, reversed-seam, bottom-circle`: after the seam ended at the bottom, the top circle began at the top. The corrected producer emits `seam, bottom-circle, reversed-seam, top-circle`.

The regenerated CTC-01 artifact is `2a96344595d2d7d9edcb4fcd9d052dfa7481bcc7ad7f18e2f4235e7d00f8608e`. Its reimport is loop-connected and M8 mass-property topology is enclosed and orientation-consistent. The analyzer now bases `enclosed-manifold` on ordered traversal plus edge-use incidence.
