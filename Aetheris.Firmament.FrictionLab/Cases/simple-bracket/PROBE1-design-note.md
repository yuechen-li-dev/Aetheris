# PROBE1 design note — canonical simple bracket

## 1) Exact spans (world space)

- Base prism (`size = [60,20,10]`):
  - X: 0 → 60
  - Y: 0 → 20
  - Z: 0 → 10
- Vertical prism (`size = [10,20,60]`):
  - X: 0 → 10
  - Y: 0 → 20
  - Z: 0 → 60
- Overlap region:
  - X: 0 → 10
  - Y: 0 → 20
  - Z: 0 → 10
- Final union (`bracket`):
  - X: 0 → 60
  - Y: 0 → 20
  - Z: 0 → 60

## 2) Placement interpretation (Firmament documented semantics)

For `box`, the documented default local frame is:
- X span `[-sizeX/2, +sizeX/2]`
- Y span `[-sizeY/2, +sizeY/2]`
- Z span `[0, sizeZ]`

With `place.on: origin`, `offset[3]` is world-XYZ translation added after anchor resolution.

Therefore:

- Base box `size[3]=[60,20,10]` with `offset[3]=[30,10,0]` gives:
  - X = `[-30,+30] + 30 = [0,60]`
  - Y = `[-10,+10] + 10 = [0,20]`
  - Z = `[0,10] + 0 = [0,10]`

- Vertical tool box `size[3]=[10,20,60]` inside `add.with` with top-level boolean `place.offset[3]=[5,10,0]` gives tool span:
  - X = `[-5,+5] + 5 = [0,10]`
  - Y = `[-10,+10] + 10 = [0,20]`
  - Z = `[0,60] + 0 = [0,60]`

Then additive union (`op: add`) of `base` and placed tool yields the target union bounds.

## 3) Why this is not a cross/T-shape

- The model contains exactly two rectangular prisms only.
- They are orthogonal in extent (one long in +X, one long in +Z) and share one corner overlap block.
- There is no third arm and no mirrored extension in −X/−Z directions.
- There are no subtracts, holes, pockets, fillets, chamfers, diagonals, or extra support geometry.
