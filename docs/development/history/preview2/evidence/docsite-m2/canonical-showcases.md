# Canonical public showcase fixtures

| Showcase | Canonical source | Real validation path | Result |
|---|---|---|---|
| First exact part | `fixtures/Canonical/Basics/box.firmament` | `aetheris build` | passed; authoritative volume assertion passed |
| Table/Template/Profile | `fixtures/Canonical/Templates/table-driven-mounting-plate.firmament` | `aetheris build` | passed |
| HexBolt standards family | `fixtures/Compatibility/LegacyV1/Examples/hexbolt_template_m2.firmament` | `aetheris build` + `aetheris mesh --format obj --debug-ir` | passed; watertight SurfaceMeshIR output |
| Profile bracket | `fixtures/Canonical/PMI/counterbore-shaft-diameter.firmament` | `aetheris build` | passed; datum and diameter PMI evidence found |
| Imported STEP | `fixtures/Regression/CanonicalGeometry/inline-step-recognize-replace.firmament` | `aetheris build` | passed; one region recognized/replaced and hybrid STEP verified |
| Bearing module | `fixtures/Canonical/Assembly/bearing-module.firmament` | `aetheris asm inspect` | passed; 44.92–45.10 mm path against 44.90 mm minimum |
| Failing stackup | `fixtures/Invalid/Assembly/bearing-module-tolerance-failure.firmament` | `aetheris asm inspect` | expected exit 1; `assembly-tolerance-assertion-failure` |
| Template block pair | `fixtures/Canonical/Assembly/template-block-pair.firmament` | `aetheris asm inspect` | passed; definitions/occurrences/placements and source provenance emitted |
| Plate with hole | `docs/development/milestones/fea/artifacts/m5/plate-with-hole.firmament` | `aetheris fea` | passed; native results and Abaqus deck emitted |
| Forge host/extension | `tools/Aetheris.Forge.M1Evidence/Program.cs` + `Aetheris.Forge.SampleExtension/SecretGeometryExtension.cs` | Forge SDK test suite | 8/8 passed |

The public site synchronization script reads these sources directly; it does not maintain independent copied snippets.
