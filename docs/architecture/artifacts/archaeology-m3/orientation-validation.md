# Orientation and validation

Strict extracted rules:

- `BrepEdgeUse` resolves traversal exactly once from topology endpoints and explicit reversal.
- a new known loop must close use-to-use and final-to-first;
- an identical directed edge use cannot repeat; a periodic seam may appear once in each sense;
- face loop zero is outer and subsequent loops are caller-oriented inner trims;
- a strict closed shell requires exactly two boundary uses per edge;
- every realized vertex requires a finite point before a body passes Surgery validation.

Tests cover rectangular loop links, open/repeated rejection without mutation, a reversed circular inner loop, a closed cube shell, open-shell rejection, and non-finite point rejection. Existing through-hole, blind-hole, keyway, polygonal cavity, STEP, and Boolean regressions retain cavity/wall/bottom orientation coverage.

Compatibility findings: historical keyway/prismatic/orthogonal coedge senses are retained behind named recipe-local control seams; orthogonal merged rectangles retain a T-junction assembly seam. These are not generalized into Surgery. Box-cylinder analytic orientation remains family-specific and is the M3 control implementation.
