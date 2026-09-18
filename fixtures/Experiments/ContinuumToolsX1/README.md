# CONTINUUM-TOOLS-X1 fixture source

The committed fixture is this deterministic recipe rather than generated bulk JSON/OBJ. Run:

```powershell
dotnet run --project Aetheris.CLI -- continuum make-fixtures --out-dir artifacts/local/continuum-tools-x1
```

It produces:

- an exact 10 mm sphere represented by six degenerate local tori (`R=0`, `r=10`) plus four queries;
- a 25 x 25 x 25 scalar SDF grid of an exact 16 x 12 x 10 mm sharp box, deliberately offset from grid nodes.

The resulting files are deterministic, regenerable, and ignored under `artifacts/local/`. They test interchange and analytic/contouring execution, not learned PAT inference.
