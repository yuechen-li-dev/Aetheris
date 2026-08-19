# Chamfer fixture corpus

This corpus is the executable boundary for `CHAMFER-FIXTURE-PRESSURE-M6`.

- `valid/` contains exact history-known construction families that build real changed geometry.
- `invalid/` contains authored inputs rejected before topology emission.
- `deferred/` records semantic selections, attempted witnesses, typed error codes, and the missing topology policy. Matching executable assertions live in `ChamferM6LoweringTests`.
- `evidence/` retains representative AP242 artifacts. See its README for hashes and reimport evidence.

The word *chamfer* in a fixture name never implies arbitrary imported-edge surgery. The admitted domain is recorded in each fixture header.
