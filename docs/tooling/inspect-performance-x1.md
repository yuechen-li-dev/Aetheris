# Inspect performance X1

`inspect-profile` and default `inspect-compose` are compiler-introspection commands. They parse, expand static data, resolve Profiles, normalize arrangements and transitions, and report analytic evidence. They do not emit BRep, export/reimport STEP, run M8 mass properties, CIR sampling, or CAD-assistant work.

> Inspection reads compiler intent before it is erased. It does not independently simulate or verify the finished artifact.

`inspect-compose --json --materialize` is the explicit opt-in diagnostic path for BRep plan/body and mass-properties evidence. `build`, `analyze`, and `verify` remain the artifact-producing and independently expensive routes.

The original CTC slowdown was accidental: `inspect-compose` always called `PrismaticSectionStackEmitter.Emit` and `BrepMassProperties.Evaluate` after normalization. The default now stops at the immutable normalized stack. Report timings distinguish parse, normalize, optional materialize, and total work; arrangement timings remain per slab. Semantic SHA-256 signatures cover Profiles, operations, slab regions, and the composition, using resolved line/arc values and deterministic order.
