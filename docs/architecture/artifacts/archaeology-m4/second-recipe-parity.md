# Polygonal through-cut parity

The contrasting recipe consumes ordered outer/inner footprints and root bounds.
Unlike a circular hole, it creates one planar cavity wall per polygon edge and
multi-edge inner support loops. The existing M3 implementation remains callable
as `BuildLegacy`; the facade now explicitly creates a typed recipe request.

The rounded-box/slot fixture remains 128 vertices, 192 edges, and 66 faces with
192 curves and 66 planar surfaces. Legacy, direct recipe, and facade STEP are
identical. The established canonical SHA-256 remains
`8554faf173a41abeb15facbeb2bd3cceb4f2ea486d6aa1e1b11c0740b922fe7d` and
STEP reimport remains valid.
