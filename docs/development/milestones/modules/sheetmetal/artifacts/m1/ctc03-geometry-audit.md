# CTC-03 ordinary-BRep geometry audit

Source: `testdata/step242/nist/CTC/nist_ctc_03_asme1_ap242-e2.stp`, imported through the ordinary current AP242 exact-BRep path.

| Fact | Value |
|---|---:|
| Bodies / shells | 1 / 1 |
| Vertices / edges / faces | 236 / 354 / 120 |
| Planes / cylinders | 62 / 58 |
| Cones / spheres / tori / splines | 0 / 0 / 0 / 0 |
| Bounds, mm | min (-249.55754, 0, 0), max (53.402246, 485.4448, 162.689737) |
| Manifoldness | enclosed manifold from directed-coedge traversal/incidence |
| PMI/semantic indicators in source | 416 entity-name occurrences; retained in the unchanged source file |

Visually, CTC-03 reads as a large bent enclosure/bracket blank with a dominant central panel, surrounding flanges, one oblique 45° transition, multiple 90° bends, and two long rectangular openings. Machine-recoverable evidence is narrower: eight overlapping parallel plane pairs, seven coaxial cylinder pairs, exact source-face adjacency, and two paired planar inner-loop profiles. All supports are analytically developable families; the other 90 faces are thickness caps, cut walls, end faces, or unmatched feature boundaries, not evidence for 90 additional reference-sheet regions.
