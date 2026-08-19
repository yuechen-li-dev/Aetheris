# Performance evidence

M0 records six independently timed phases in `AssemblyPerformanceIr`: parse, semantic/instance bind, Mate validation, placement solve, dimensional graph build, and tolerance analysis. Use:

```text
aetheris asm inspect fixtures/Canonical/Assembly/bearing-module.firmament --json --profile
```

The final profiled CLI observation on the seven-instance/two-Mate/six-relation dogfood was 22.22 ms parse, 9.84 ms bind, 2.53 ms Mate validation, 10.20 ms placement, 0.82 ms dimensional graph, and 4.16 ms tolerance analysis. Focused in-process test runs complete all nine M0 compiler tests in roughly 0.15 seconds on this development machine. These are diagnostic observations, not benchmarks or budgets. The raw report is `performance.json`.
