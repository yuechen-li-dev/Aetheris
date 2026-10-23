# Virtual sculpture

Not everything worth building has to be manufactured. Virtual sculpture is the
corner of Aetheris that exists purely because exact geometry is beautiful. Nothing
here is meant for a machine shop, nothing here changes how engineering parts
build, and you will not find a DFM check in sight.

It is also a decent stress test. A sculpture with hundreds of exact tori, spheres
and cylinders exercises the same STEP writer that ships brackets, just with more
enthusiasm.

## Sol 1

The flagship piece is **Sol 1**, a disc of 233 points arranged the way a
sunflower arranges its seeds. Its source is a small JSON description,
[`sol-1.sculpture.json`](../../fixtures/Canonical/VirtualSculpture/sol-1.sculpture.json),
marked `"mode": "Virtual"` so nobody mistakes it for a part.

The recipe:

- Place each point on the golden angle, with equal-area spacing between an inner
  and outer radius:

  ```text
  theta_n = n * pi * (3 - sqrt(5))
  r_n     = sqrt(r_inner^2 + n * (r_outer^2 - r_inner^2) / (N - 1))
  ```

- Connect every point to the points 13 and 21 places later. Those are Fibonacci
  numbers, so the chords trace the two spiral families you see in a sunflower
  head.
- Lift each point with a two-frequency wave (fivefold and thirteenfold), and let
  three families of strands rise near the rim so a few of them escape the disc.
- Leave a toroidal eye in the middle, ring it with an inner corona, and frame the
  whole thing with a calm outer torus. The empty centre is designed, not a
  numerical accident.

Build it from the repository root:

```powershell
dotnet run --project Aetheris.CLI -c Release -- sculpture build fixtures/Canonical/VirtualSculpture/sol-1.sculpture.json --out artifacts/local/virtual-sculpture/sol-1.step --evidence artifacts/local/virtual-sculpture/sol-1.evidence.json --preview artifacts/local/virtual-sculpture/sol-1.preview.svg --json
```

Leave out `--out` and the files land in `artifacts/local/virtual-sculpture/`.

## What you get

Sol 1 exports as an AP242 assembly of exact, closed analytic bodies: tori for the
frame, eye and corona, spheres for the nodes, and capped cylinders for the
chords. There are no B-spline or rational surfaces anywhere in the file.

The nodes and strands overlap at their joints and are deliberately *not* fused
into one solid. Fusing them would be a manufacturing decision, and this is not
manufacturing. Because the result is an assembly, inspect it with the assembly
import tools rather than single-part `aetheris analyze`.

The evidence file records the pattern counts, an inventory of surface types, the
reimported assembly and a SHA-256 of the STEP, so you can check that a rebuild
produced exactly the same sculpture.

## More to play with

- [Mathematical knots](firmament/mathematical-knots.md): trefoils, figure-eights
  and torus knots as smooth WireForm tubes.
- The older `experimental heightfield-art` command is documented with
  [PlasticShell](firmament/plastic-shell.md). It is a retired experiment and is
  unrelated to Sol 1.
