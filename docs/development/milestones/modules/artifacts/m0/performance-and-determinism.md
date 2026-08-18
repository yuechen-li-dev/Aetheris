# Performance and determinism

The showcase command records one-process timing in `validation-report.json`. On the final validation run, cold lazy catalog initialization (including first-use/JIT costs) was approximately 14 ms, saddle lowering/materialization approximately 48 ms, and routed-pipe lowering/materialization approximately 13 ms. The report also records the mean cached catalog inspection cost over 10,000 accesses; cached lookup is the representative compiler/Host plumbing overhead after process initialization.

Two independent showcase invocations emitted identical hashes:

- ruled saddle: `D0C7DB3488FB7D9C2294F6641FC49F387EDE323E1E31FFD512686514F086E451`
- pipe route: `2F52517C5FFAF4C6F02814ABE870158C607346F3EC084B76ED097C6D29539A7D`

Focused tests independently compare repeated catalog ordering, STEP hashes, and semantic stable identities. Timings are diagnostic smoke measurements, not a throughput benchmark or performance guarantee.
