# Ground-truth CLI inspection

Generated artifacts were re-imported with `aetheris analyze ... --json` through the normal CLI path.

| Artifact | Bodies / shells | Faces / edges / vertices | Structural assessment | Exact surface families | Bounds (mm) |
|---|---:|---:|---|---|---|
| `pipe-route.step` | 1 / 1 | 5 / 7 / 4 | enclosed-manifold | 2 plane, 2 cylinder, 1 torus | `[0,-10,-10]` to `[90,80,10]` |
| `ruled-saddle.step` | 1 / 1 | 6 / 12 / 8 | enclosed-manifold | 4 plane, 2 B-spline (degree-(1,1) ruled supports) | `[-40,-25,-12]` to `[40,25,14]` |

Both imports had contiguous face, edge, and vertex ID ranges and no unsupported curve family. The pipe uses five circle curves plus two lines; the saddle uses twelve exact lines.
