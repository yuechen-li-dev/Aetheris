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
