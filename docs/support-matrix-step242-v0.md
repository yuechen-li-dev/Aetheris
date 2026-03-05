# STEP242 v0 Support Matrix

Aetheris STEP242 v0 is intentionally narrow and deterministic.

## Hard contracts

- Single solid only: exactly one `MANIFOLD_SOLID_BREP` root is required.
- Backend-authoritative flow: import -> render -> canonical export (+ SHA256).
- Deterministic diagnostics: stable `(code, source, messagePrefix)` for expected fail cases.
- Exporter line-edge consistency: for exported `EDGE_CURVE` instances backed by `LINE`, both referenced vertex points are guaranteed to lie on the exported line geometry within export tolerance.
- STEP Part 21 complex entity-instance assignments are accepted for parser/decoder context use (for example `#5=(GEOMETRIC_REPRESENTATION_CONTEXT(...)...REPRESENTATION_CONTEXT(...));`).
- STEP Part 21 typed parameter values are supported in value position with deterministic normalization (`IDENT(...)` parsed as uppercase identifier + argument list), including common nested wrappers such as `LENGTH_MEASURE(...)` and `LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE(...),#...)`.
- `ADVANCED_FACE.surface` accepts either a `#entity` reference or a limited inline surface constructor (`PLANE`, `CYLINDRICAL_SURFACE`, `CONICAL_SURFACE`, `SPHERICAL_SURFACE`) when geometry arguments are otherwise in subset.

## Supported entity families (Tier1)

| Category | Supported in v0 |
|---|---|
| Curves | `LINE`, `CIRCLE` |
| Surfaces | `PLANE`, `CYLINDRICAL_SURFACE`, `CONICAL_SURFACE`, `SPHERICAL_SURFACE` |
| Topology | `MANIFOLD_SOLID_BREP`, `CLOSED_SHELL`, `ADVANCED_FACE`, loops/coedges/edges/vertices subset required by kernel importer |

## Hole policy

- Planar multi-loop hole semantics are supported when loop-role classification is unambiguous and safe.
- Curved-surface holes are deterministic fail in v0 (`Importer.LoopRole.*`).

## Explicit non-goals for v0

- Assemblies / occurrences import semantics.
- PMI / MBD.
- NURBS / B-spline / healing workflows.
- Toroidal and other exotic analytic surface support.
- Any modeling/editor feature expansion in the Viewer tab.
- Typed wrapper decoding is intentionally limited in v0: primitive readers currently unwrap single-argument typed values; wrappers with unexpected arity fail deterministically via `Importer.StepSyntax.TypedValue`.

## Corpus and CI gate

- Manifest file: `testdata/step242/manifests/v0.corpus.json`.
- Groups:
  - `passRequired`: must pass Parse -> Import -> Validate -> Tessellate -> Pick -> Export -> SHA256.
  - `expectedFail`: must match first diagnostic triplet exactly.
  - `deferred`: reported, non-gating.
- Determinism:
  - lexical path ordering,
  - LF-normalized report output,
  - in-process double-run byte stability.
