# CHAMFER-FIXTURE-PRESSURE-M6 retained STEP evidence

| Artifact | SHA-256 | Independent Aetheris reimport |
|---|---|---|
| `rectangular-top-loop.step` | `1A982C1D808392653ABE310184AF6FAD4D8321CF3179C6C3F029955898074AE0` | manifold; V/E/F = 12/20/10 |
| `cylinder-top-rim.step` | `AB37BBB068C86AEDFD39A1C998A221B722049E4183F705E57DC1F57EC686DE6C` | manifold; V/E/F = 6/5/4; plane/cylinder/cone = 2/1/1 |
| `hole-entry.step` | `F3B8AFD4BD55F388475D35751683A915287EF3CD6D2B42F569DB40D658911E14` | manifold; V/E/F = 15/17/8; plane/cylinder/cone = 6/1/1 |
| `hole-entry-fixed.step` | `48376D644226875E8EE0400220994C41D67717E8E9C4DFCD893F728575E509B9` | manifold; V/E/F = 11/17/8; plane/cylinder/cone = 6/1/1 |

The cylinder artifact was also loaded successfully in CAD Assistant in shaded-with-edges mode; its continuous circular chamfer band and reduced planar cap were visible from top-oblique and rotated side views.

`hole-entry.step` is retained as the malformed regression input. Its countersink loops used duplicate seam vertices and invalid ordering, its inward walls had the wrong face sense, and its conical support did not contain its trim edges. CAD Assistant stalls at 50%; SolidWorks displays cross-hole seam/chord artifacts.

`hole-entry-fixed.step` is the corrected compatibility authority. CAD Assistant imports and renders the analytic conical entry and cylindrical shaft. See `docs/implementation/hole-entry-occt-interop-a0.md`.
