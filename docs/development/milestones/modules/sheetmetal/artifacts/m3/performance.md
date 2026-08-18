# Performance and determinism

Representative CTC-03 in-process cold observation from the public CLI report: parse 9.41 ms, formed lowering/preflight 60.21 ms, first authored flat lowering 29.69 ms; the immediately repeated flat traversal was 0.42 ms. Whole `dotnet run --no-build` wall time is about 0.70-0.75 s and is dominated by CLI process startup.

Generated artifact SHA-256 values:

- formed STEP: `e026084f351dcf2011f5fd675ab63573d58380c04d7b3a8ec28e9348e5ae79f1`;
- flat STEP: `73bd832b1fba07ea3a391c90300a488f491388ed50a48f9d78c6e330c1af1963`;
- flat SVG: `1c2db51a3b83a346b253af77f047025da4e472cecf68a889deaab51e9bd2ef6b`.

Stable semantic IDs, topology iteration, correspondence IDs, flat hash, SVG ordering, and STEP topology ordering are covered by repeated compilation tests.
