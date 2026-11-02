# Blend clean-up E0: classification probe

**Goal.** Before any surgery, find out whether imported blends (fillets, chamfers and the
patches between them) can be told apart from the faces they connect, reliably enough to
plan `Recognize` / `Remove`. Read-only; no geometry changes.

**Probe.** `Aetheris.Kernel.Firmament.Tests/Experiments/BlendRegionProbe.cs`, run by
`BlendRegionProbeRun` with `AETHERIS_EXPERIMENTS=1`. Output (JSON with display meshes) goes to
`artifacts/local/blend-probe/`.

## Method

Faces are **primary** (large, analytic, extendable) or **secondary** (blend-shaped). The
primaries are what surgery will extend; secondaries are deleted whatever their type.

- Tangency per edge from exact analytic normals (plane, cylinder, cone, sphere, torus),
  oriented outward by the display mesh, at five interior samples; 3° threshold.
- Round secondary: cylinder, torus, sphere or B-spline, radius ≤ 5 mm, tangent to two or
  more distinct neighbours, and wrapping its axis (or tube) by less than 178°.
- Chamfer secondary: planar or conical strip no wider than 5·√2 mm, leaning 20–70° against
  at least two neighbours.
- Per secondary face, the number of distinct primaries it is tangent to: **2 = strip**
  (one fillet run between two faces), **0–1 = joiner** (corner patch, setback, or a blend
  meeting only blends).
- Per-face blockers: a concave hole wall meeting the blend at a sharp edge and **not
  coaxial** with it (a hole cut across a fillet); an inner loop in the blend; a B-spline
  primary neighbour that cannot be extended.

## Results

Authored ground truth (finish removed from the source; secondaries added vs faces added):

| Fixture | Finish faces | Found |
|---|---|---|
| chimera fillet | 18 | 18 |
| chimera chamfer | 17 | 16 |
| D-profile fillet / chamfer | 2 / 2 | 2 / 2 |
| polygon rim fillet | 4 | 4 |

NIST (17 files) plus SO-101: **752 strips, 146 chamfer faces, 276 joiners, 27 blocked
faces** (20 by B-spline neighbours, 7 by holes crossing blends). FTC-07, FTC-08, STC-07 and
STC-08 read as textbook strip networks with spherical corner joiners; CTC-04's hole entry
breaks classify as chamfers, its pocket floor fillets as strips.

SO-101 (Fusion AP214, 295 faces): 48 strips, 15 chamfer faces, 24 joiners, 22 blocked.
The teardrop holes cut into the top fillet are caught as hole-crosses-blend blockers; the
long bottom edge chain is blocked by Fusion's B-spline neighbours.

## Findings that shape E1

1. **Regions are networks, not chains.** Tangent blends connect into large components (up
   to 76 faces on FTC-07). The unit of surgery is the strip (two primaries) plus the joiners
   at its ends, not the connected component.
2. **Tangency alone confuses blends with holes.** Teardrop walls, slot ends and hole walls
   are cylinders tangent to two flats. How far the face wraps its axis separates them: a
   blend is under 180°, a slot end 180°, a teardrop about 270°.
3. **Blocking must be per face and direction-aware.** A fillet running around a hole
   (coaxial, or tangent to the hole wall) is removable; a hole cutting across a fillet is
   not. Region-level blocking falsely blocked 102 of SO-101's faces.
4. **Joiners are a quarter of all secondaries.** Corners are not an edge case; E3 matters.

## Known misses and limits

- One chimera chamfer face is missed (16 of 17); not yet diagnosed.
- A torus whose major radius is under its minor radius (a fillet around a tight inside arc)
  has no well-defined tube angle; the wrap test is skipped for it.
- Thresholds (5 mm radius, 3° tangency) are fixed for the probe; production takes them
  from the request.
- `rounded-plate-through-cavities` has no finish to strip, so its authored count is not a
  ground-truth comparison; its secondaries are the plate's own rounded corners.

## Next

E1: remove a single strip between two planes ending on planar caps, verified against the
authored part without the finish (author, export, import, remove, compare).
