# STEP section normalization X2

Normalize section topology before comparing geometry.

`aetheris sections part.step --axis Z --levels -100,-60,-50,0,5,50 --json`
reports Below, At, and Above samples. Below/Above are `Z ± epsilon` (default
`0.001 mm`); At is the requested plane and is diagnostic only.

The active STEP route converts bounded line and circular-arc intersections to
`ArrangementSourceCurve2D`, transfers endpoint and intersection parameters,
splits them through `ProfileArrangementBuilder.NormalizeBoundary`, collapses
identical atomic supports while retaining source IDs, then validates a
deterministic incidence graph. It does not use the former first-neighbour
chain walker.

The current CTC evidence deliberately rejects rather than fabricates loops at
the lower levels. The trace isolates four conical faces whose horizontal cuts
are non-circular conics because their axes are not normal to Z. That family is
outside X2 and is now reported as `UnsupportedSectionCurve` rather than
silently lost. Cylinder trims are derived from the actual trim-edge/plane
intersections, not from projected face vertices. At Z=5 and Z=50 the six-sided
raised region normalizes as one exact loop. This is an evidence-backed
normalization checkpoint, not yet a completed material-region implementation:
material-side classification, conflict ownership, containment roles, and
richer fragment provenance remain required before section correspondence can
begin.
