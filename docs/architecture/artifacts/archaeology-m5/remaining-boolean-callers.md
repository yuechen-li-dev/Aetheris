# Remaining Boolean callers

- `FirmamentPrimitiveExecutor`: compatibility execution for operation-shaped V1 input and a generic primitive bridge. Retained deliberately; known recovered through holes take the Recipe path elsewhere.
- `HoleRecoveryExecutor`: five direct subtract sites implement legacy blind/counterbore/countersink/chamfered/stepped fallback families. Their multi-stage history and topology require family-specific Recipes outside M5.
- `CirBrepMaterializer` box/box: recognized operands but no named reusable construction Recipe.
- server `operations/boolean`: genuine external compatibility surface over two stored bodies. API description now states bounded-family support and typed rejection.

The facade remains public and behavior-compatible. It is not the normal implementation path for the three migrated semantic constructions.
