# Deterministic PMI layout

PMI assignment is manual; placement is automatic. Each assigned semantic PMI item produces a finite ordered candidate set:

- diameter, datum, and feature-control presentations first try NE/NW/SE/SW free-leader positions;
- all annotations receive top, bottom, left, and right exterior lanes 1 through 3;
- candidate cost is leader length plus a fixed lane-index penalty, page-edge penalty, and a stable ordinal tie-break.

Annotation text is represented by a measured conservative rectangle. Projected model segments have segment bounds. Accepted candidates must be inside the view allocation, must not intersect a model segment, and must not intersect already accepted annotation bodies. Candidate ordering and first-admissible selection are deterministic. Thin leader/model or leader/leader crossings remain an explicitly allowed M0 fallback; text overlap never does.

If every bounded candidate is rejected, compilation emits `drawing-layout-impossible`; it does not silently overlap. Table overflow uses another A4 page. Layout evidence in DrawingIR includes annotation count, before/after collision counts, lane occupancy, rejected candidates, and failures.

The canonical artifact reports zero unresolved text/model and text/text collisions. JudgmentEngine is intentionally not used: M0 has one bounded deterministic heuristic, not competing interpretations that need general utility adjudication.
