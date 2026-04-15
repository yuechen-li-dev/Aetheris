# .firmasm examples (ASM-A0)

`.firmasm` manifests are JSON objects with four top-level sections:

- `manifest` with `version`.
- `assembly` with `name` and `units`.
- `parts` dictionary where each entry has:
  - `kind`: `firmament` or `step`
  - `source`: relative path to the part source file
- `instances` array where each entry has:
  - `id`: unique instance id
  - `part`: part key from `parts`
  - `transform`:
    - `translate`: `[x, y, z]` (required)
    - `rotate_deg_xyz`: `[rx, ry, rz]` (optional)

STEP parts are loaded as foreign opaque rigid geometry and are validated through the bounded STEP importer seam.

## ASM-A1 OCCT nut-bolt extracted fixtures

See `testdata/firmasm/examples/occt-nut-bolt/README.md` for the bounded assembly extraction fixture set and current nut import blocker evidence.

## ASM-A2.5b OCCT as1 flattened extracted fixture

See `testdata/firmasm/examples/occt-as1/README.md` for the full flattened extraction fixture set (`5` deduplicated STEP part families, `18` rigid instances).
