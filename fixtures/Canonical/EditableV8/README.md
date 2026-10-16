# Editable V8 geometry reference

`geometry-reference.expected.json` retains the original implementation's 171
binding names, 21 mesh fingerprints and solved world matrices. Its source hash
and implementation commit identify the pre-refactor baseline.

`geometry-reference.meshes.zip` recovers the numeric data behind those existing
fingerprints. Each entry is named `<original SHA-256>.bin` and contains
little-endian position and normal arrays (Int32 length, then Float64 elements),
followed by indices (Int32 length and elements). Signed zero is normalized.
The test verifies each entry against the unchanged original fingerprint.

The numeric golden is necessary because a byte hash cannot distinguish geometry
changes from platform math roundoff. Windows/Linux comparison on 2026-10-16 found
identical array lengths and indices, maximum position delta `1.013e-13 mm`, and
maximum normal-component delta `2.267e-10` (spring normals). The live test allows
`1e-9 mm` per position component and `1e-9` per normal component, requires finite
values, and compares indices exactly. World matrices retain their `1e-9` bound.
No production geometry or original fingerprint was changed.

To recover the archive, run the demo at the original implementation commit with
default `V8Spec` (see the JSON's `reproduction` field), then from the current
repository root:

```powershell
python scripts/freeze-editable-v8-reference.py path/to/baseline.mesh.json
```

The command writes under ignored `artifacts/local/v8-ci/`. It refuses any input
whose meshes do not exactly match all original fingerprints; it cannot rebaseline
changed geometry. The checked-in archive was recovered from the Windows baseline
with all 21 original hashes verified. It is a compressed durable regression golden,
not a per-run mesh export. Its full numeric arrays permit tolerance comparisons
without the boundary failures introduced by rounding and hashing floats.
