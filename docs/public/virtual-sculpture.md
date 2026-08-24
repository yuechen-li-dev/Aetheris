# Virtual Sculpture

Virtual Sculpture is Aetheris's deliberately non-manufacturing geometry lane. It exists for bounded computational artworks whose semantic purpose is visual and mathematical rather than fabrication. It does not extend or weaken Firmament `Model`, PlasticShell, Sheet Metal, Structural, Piping, Surfacing, or FEA behavior.

ART-X0 contains one flagship piece: **Sol 1**. Its canonical source is [`fixtures/Canonical/VirtualSculpture/sol-1.sculpture.json`](../../fixtures/Canonical/VirtualSculpture/sol-1.sculpture.json), marked explicitly with `"mode": "Virtual"`. The lane is intentionally narrow; it is not a general art platform or an alias for the retired PlasticShell height-field experiment.

## Composition law

Sol 1 places 233 nodes in an annular phyllotaxis field:

```text
theta_n = n * pi * (3 - sqrt(5))
r_n     = sqrt(r_inner^2 + n * (r_outer^2 - r_inner^2) / (N - 1))
```

Each node connects to nodes `n + 13` and `n + 21`. These Fibonacci offsets expose the two dominant parastichy families of the golden-angle field. Node height is a deterministic two-frequency radial/angular wave. Three residue-selected `+21` families rise through a smooth outer ramp, producing the controlled escaping prominences.

The composition reserves an analytic toroidal eye, adds an analytic inner-corona delimiter, and surrounds the field with a calm exact toroidal frame. The center is a designed void, not a collapsed numerical pole.

## Representation contract

Sol 1 exports as an AP242 product-structure assembly of exact closed analytic bodies:

- toruses for the frame, eye, and corona delimiter;
- spheres for phyllotaxis nodes;
- rigidly placed cylinders for Fibonacci-family chords;
- planar analytic caps on the bounded cylinders;
- no B-spline surfaces and no rational product surfaces.

The nodes and strands overlap visually at their joints. They are intentionally retained as separate exact closed bodies rather than boolean-unified into a manufacturing solid. That preserves deterministic analytic structure and honestly describes the artifact as virtual sculpture. A generic single-part `aetheris analyze` route is therefore not the correct inspection route; use assembly import/inspection for the STEP product structure.

## Build Sol 1

From the repository root:

```powershell
dotnet run --project Aetheris.CLI -c Release -- sculpture build fixtures/Canonical/VirtualSculpture/sol-1.sculpture.json --out artifacts/local/virtual-sculpture/sol-1.step --evidence artifacts/local/virtual-sculpture/sol-1.evidence.json --preview artifacts/local/virtual-sculpture/sol-1.preview.svg --json
```

When `--out` is omitted, generated files default under `artifacts/local/virtual-sculpture/` in accordance with the generated-artifact policy. The evidence report records pattern counts, representation inventory, STEP assembly reimport, deterministic SHA-256, and the explicit non-manufacturing marker.

The older `experimental heightfield-art` command remains documented with [PlasticShell](firmament/plastic-shell.md). It is a quarantined historical experiment and is not Sol 1.
