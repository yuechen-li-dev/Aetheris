# HexBolt standards data M1

`hexbolt_template_m2.firmament` now separates `HexBoltStandardRow` (head and
shank family geometry) from `HexBoltSpec` (stable identity, length, thread
extent, designation, and property class). `HexBoltSpec.Standard` is a nested
Record. Templates access e.g. `Spec.Standard.HeadAcrossFlats`; nested static
member paths are resolved during template specialization.

The fixture's `McMasterThreadlessHexBoltGeometry` is a keyed columnar Table.
Its M8 row is the audited McMaster-Carr 91180A151 threadless STEP reference
already used by the M1 oracle. The M10 row is explicitly provisional existing
fixture/test data, not a claim of ISO 4017 certification. `M10x50` derives from
the M8 instance with `with`; the 8.25 × 37.5 mm nonstandard fixture still authors
an arbitrary `HexBoltStandardRow` and `HexBoltSpec` directly.

This is intentionally not a complete ISO dataset. The next standards milestone
should add cited rows and a broader catalog without changing compiler logic.
