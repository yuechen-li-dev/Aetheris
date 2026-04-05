# Firmament Overview and Compile Pipeline

## What Firmament is

Firmament is an ordered CAD-feature DSL that compiles to executable primitive/boolean operations over Aetheris BRep bodies.

- It is **feature-id based** and **document-order dependent**.
- It supports primitive creation, bounded boolean composition, validation ops, and a narrow pattern expansion subset.
- It supports TOON-style canonical syntax and JSON-compat parse mode.

## What Firmament is not

Firmament is **not** currently:

- a general parametric constraint solver,
- a full frame/assembly language,
- a generalized selector graph traversal language,
- a general robust boolean engine for arbitrary solids.

## Execution model (current)

Compile flow today:

1. Parse source (`TOON` or JSON object root).
2. Validate schema block (if present).
3. Validate primitive required fields.
4. Validate boolean required fields.
5. Validate pattern required fields.
6. Validate validation-op required fields + target shape classification.
7. Enforce document coherence (references, selector roots/ports, pattern expansion).
8. Map schema into compiled schema model.
9. Run CNC DFM checks (narrow current rule).
10. Lower primitives/booleans.
11. Execute primitives then booleans.
12. Enclosed-void schema guard (hard failure when disallowed by process policy).
13. Run validation ops against executed geometry.

The returned artifact includes parsed doc, compiled schema, lowered plan, primitive/boolean execution result, and validation execution result.

## Identity and ordering semantics

- Feature IDs must be unique across feature-producing ops.
- Boolean primary reference fields (`to`, `from`, `left`) must reference earlier produced IDs.
- Pattern ops expand into synthesized IDs (`__linN`, `__cirN`) and become concrete feature-producing boolean entries.
- Validation ops do not create geometry.

## Export-selection semantics

Export chooses the **last successfully executed geometric body by op index** (primitive or boolean).
Validation ops are never export bodies.

Validation-op failures are diagnostic-only: they can mark validations unsuccessful and add warning diagnostics, but they do not by themselves block compile success or STEP export.

## Reference scope note (authoring vs integration API)

This corpus is primarily for `.firmament` file authoring semantics.  
If you are integrating at compiler API level, inspect implementation contracts directly (`FirmamentCompileResult` + `FirmamentCompilationArtifact`) and the integration tests in `Aetheris.Kernel.Firmament.Tests`.
