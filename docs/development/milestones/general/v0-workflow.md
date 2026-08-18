# STEP242 Viewer v0 Workflow

## User path

1. Upload `.step` / `.stp` file.
2. Backend parses/imports with deterministic diagnostics.
3. Viewer renders tessellated body (read-only).
4. Inspector shows canonical hash (`SHA256`) from backend export.
5. Download canonical STEP242 text from backend.

## Diagnostics interpretation

- If import fails, treat backend diagnostics as source of truth.
- For known unsupported inputs, first diagnostic triplet is stable by contract:
  - `code`
  - `source`
  - `messagePrefix`
- `expectedFail` corpus entries in the v0 manifest lock this behavior for CI.

## v0 boundaries

- Exactly one `MANIFOLD_SOLID_BREP` root.
- No assemblies, PMI, NURBS, toroidal/exotic families.
- Planar holes supported where safe; curved-surface holes fail deterministically.
