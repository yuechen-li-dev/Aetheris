# Expected intent

- Canonical orthogonal sheet-metal-style L-bracket made from exactly two axis-aligned rectangular prisms by additive union only.
- `base` (horizontal arm): box size `[60, 20, 10]` at origin span `X 0→60, Y 0→20, Z 0→10`.
- `upright` (vertical arm): box size `[10, 20, 60]` at origin span `X 0→10, Y 0→20, Z 0→60`.
- Required overlap volume between arms: `X 0→10, Y 0→20, Z 0→10`.
- Resulting union bounding box: `X 0→60, Y 0→20, Z 0→60`.
- No subtracts, pockets, holes, chamfers, fillets, triangular faces, diagonal faces, or extra geometry.
