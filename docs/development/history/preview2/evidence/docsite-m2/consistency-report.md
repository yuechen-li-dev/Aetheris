# Cross-repository consistency report

| Area | Public statement | Verified source | Result |
|---|---|---|---|
| Firmament status | 33 feature rows with Supported/Experimental/Legacy/Future boundaries | `language-features.json` + manifest test | synchronized by hash |
| FEA | bounded linear isotropic elasticity; native/imported admitted domains; fixed/traction/force/pressure; Abaqus deck only | FEA docs, 12 tests, current CLI solve | consistent |
| Assembly | Interface Roles, Lower/Fit/Allow, product tree, Mate graph, transforms, dimensional graph, worst-case assertion | Assembly source/tests and both `asm inspect` fixtures | consistent |
| Forge | typed Template host; explicitly registered bounded extensions; exact/CIR/semantic output | Forge docs, sample extension, 8 SDK tests | consistent |
| SurfaceMeshIR | structured polygons/quads before target triangulation; OBJ/STL; bounded support families | feature manifest and current HexBolt CLI mesh | consistent |
| Continuum | BRep boundary/topology authority and CIR occupancy dual-lowering; SDF is a backend | Preview 2 feature manifest + Continuum docs | consistent |
| Imported STEP | exact bounded import/recognition; no arbitrary history recovery | language reference + InlineStep build evidence | consistent |
| CLI | `build`, `inspect`, `mesh`, `fea`, and `asm inspect` flags | real command help + executed commands | consistent |

Two drift hazards were removed: the public site no longer consumes `preview1-capabilities.json`, and generated public prose/examples now reference a checked-in Preview 2 snapshot with source hashes. Current docsite validation measurements live in `measured-results.json`; they intentionally supersede older milestone result numbers for the public showcase.
