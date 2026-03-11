# Mx119d helper classification (diagnostics-first)

## 1) Shared neutral infrastructure (extracted)
- Guardrail wrapper that converts `ArgumentException` / `InvalidOperationException` into deterministic import diagnostics (`Importer.Guardrail`).
- Generic single-root STEP entity lookup helper for deterministic root selection error handling.
- Generic diagnostic constructors for `NotImplemented` and `ValidationFailed` result types.

## 2) Exact-lane-owned policy/helpers (intentionally kept lane-owned)
- Exact AP242 root/entity policy (`MANIFOLD_SOLID_BREP` traversal semantics and all exact topology/surface decode behavior).
- Loop-role normalization, containment, spherical/conical special handling, and orientation/topology policy.

## 3) Tessellated-lane-owned policy/helpers (intentionally kept lane-owned)
- `TESSELLATED_SOLID` shell/face family policy.
- `COMPLEX_TRIANGULATED_FACE` triangulation and triangle-to-brep topology mapping.
- Tessellated subset constraints and diagnostics specific to tg path behavior.

## 4) Not-worth-extracting residue (for now)
- Narrow helpers used only once in a lane and encoding nearby policy/context.
- Data-shape readers that look similar but carry lane-specific failure context strings and constraints.

This milestone intentionally extracts only neutral shared infrastructure and leaves lane policy where it belongs.
