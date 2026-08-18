# Geometry parity

M2 changes reader/facade ownership only. The retained V1 compiler, lowering, executor, and STEP exporter are unchanged. A real CLI build of `box_basic.firmament` completed through the compatibility route and produced the recorded deterministic STEP hash in [model-equivalence.md](model-equivalence.md).

Legacy JSON `.firmasm` migration retains all OCCT AS1 instance transforms as `LegacyExplicit`; the migration test proves it emits no `Mate` records. No geometry or placement semantics were reinterpreted.
