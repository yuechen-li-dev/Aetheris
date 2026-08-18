# Archaeology M5: production caller migration

M5 moves callers that already know through-hole intent to `ThroughHoleConstructionRecipe`. The typed `ThroughHoleRecipeRequestBuilder` converts semantic box/Z-cylinder data into validated construction history without constructing or recognizing temporary operand bodies.

Migrated: `ThroughHoleRecoveryExecutor`, recognized CIR `subtract(box,cylinder)`, and StandardLibrary `cube_with_cylindrical_hole`. The public Boolean facade remains stable for legacy/generic callers. No Boolean family or Surgery public surface was added.

- [production-caller-map.md](production-caller-map.md)
- [firmament-migration.md](firmament-migration.md)
- [cir-migration.md](cir-migration.md)
- [standardlibrary-migration.md](standardlibrary-migration.md)
- [remaining-boolean-callers.md](remaining-boolean-callers.md)
- [kernelsdk-boundary.md](kernelsdk-boundary.md)
- [topology-parity.md](topology-parity.md)
- [validation-report.md](validation-report.md)
