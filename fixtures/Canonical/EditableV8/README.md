# Editable V8 geometry reference

`geometry-reference.expected.json` retains the original implementation's 171
binding names and solved world matrices, together with 21 qualified display-mesh
fingerprints. Its source hash and implementation commit identify the design's
pre-refactor provenance; `meshReferenceRevision` identifies its display projection.

`geometry-reference.meshes.zip` contains the numeric data behind those qualified
fingerprints. Each entry is named `<SHA-256>.bin` and contains
little-endian position and normal arrays (Int32 length, then Float64 elements),
followed by indices (Int32 length and elements). Signed zero is normalized.
The test verifies each entry against its declared fingerprint.

The numeric golden is necessary because a byte hash cannot distinguish geometry
changes from platform math roundoff. Windows/Linux comparison on 2026-10-16 found
identical array lengths and indices, maximum position delta `1.013e-13 mm`, and
maximum normal-component delta `2.267e-10` (spring normals). The live test allows
`1e-9 mm` per position component and `1e-9` per normal component, requires finite
values, and compares indices exactly. World matrices retain their `1e-9` bound.
The analytic-edge export correction on 2026-10-24 changed display sampling on four
definitions. Symmetric quad diagonals also gained a rounding-scale deterministic
tie-break. Before refreshing these display fingerprints, all 21 definitions'
surfaces, curves, trim bindings, vertices and topology were compared with the prior
qualified kernel within `1e-9`; all 171 world matrices were preserved. The prior
kernel's meshes first passed the original frozen reference. The new Windows and
Linux meshes have identical indices and array lengths, with maximum position
and normal differences within the existing component bounds. No design dimensions
or exact construction geometry changed.

To recover the archive, run the demo at the qualified current revision with
default `V8Spec` (see the JSON's `reproduction` field), then from the current
repository root:

```powershell
python scripts/freeze-editable-v8-reference.py path/to/baseline.mesh.json
```

The command writes under ignored `artifacts/local/v8-ci/`. It refuses any input
whose meshes do not exactly match all declared fingerprints; it cannot rebaseline
changed geometry. The checked-in archive uses the qualified Windows projection
with all 21 hashes verified. It is a compressed durable regression golden,
not a per-run mesh export. Its full numeric arrays permit tolerance comparisons
without the boundary failures introduced by rounding and hashing floats.
