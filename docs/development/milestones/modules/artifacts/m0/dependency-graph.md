# Dependency graph

Deterministic dependency-first catalog order: `Aetheris.Core`, `Aetheris.Piping`, `Aetheris.Surfacing`, `Aetheris.SheetMetal`.

- `Aetheris.Piping@0.1.0 -> Aetheris.Core >=1.0.0`
- `Aetheris.Surfacing@0.1.0 -> Aetheris.Core >=1.0.0`
- `Aetheris.SheetMetal@0.1.0 -> Aetheris.Core >=1.0.0, Aetheris.Surfacing >=0.1.0`

Duplicate identities, missing/old dependencies, and cycles are rejected before a catalog becomes observable.
