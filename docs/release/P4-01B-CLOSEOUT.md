# P4-01B closeout

**Verdict: Accepted — B1 and B2.**

1. **The bolt is a credible Preview 4 mechanical showcase.** Normal Firmament authors continuous major-diameter stock minus a finite helical groove, with 0.953125 mm head-side and 1.315625 mm tip-side cylindrical lands. The under-head fillet and tip chamfer meet ordinary shaft stock. The 38 turns, 1.25 mm pitch and CODEX engraving with two counters remain. Stock length is deliberately 52 mm to preserve the thread span while providing these lands.
2. **STEP is free of the obvious avoidable bloat found in this audit.** Export-local exact immutable geometry sharing saves 35.064% against the repaired artifact. Topology, pcurve associations, numeric literals and AP242 structures remain equivalent. Remaining size is predominantly unique certified spline/control geometry.

| Stage | STEP bytes |
| --- | ---: |
| Original engraved witness | 9,028,303 |
| Repaired geometry, before optimization | 6,991,354 |
| Final optimized artifact | 4,539,892 |

B1 was accepted before B2 profiling/optimization. [B1 report](P4-01B1-THREAD-SHOWCASE-REPAIR.md) documents the physical repair, wire-only timeout audit, legal spline knots, numerical evaluator fix and obsolete-generator deprecation. [B2 report](P4-01B2-STEP-COMPACTNESS-X0.md) documents entity/byte attribution, exact duplication, interning scope, precision and timing evidence.

## Validation

The final normal CLI and opt-in topology/display audit produce identical STEP bytes: SHA256 `2376F5E53F715716F4F0823C3787059B0E69337B22EFA39F93357F4951BE89E8`. Aetheris reimports one enclosed, orientation-consistent shell/body with 293 faces, 689 edges, 406 vertices and two uses per edge. All 293 faces shade; zero wire-only faces remain. Native FreeCAD independently imports one valid closed solid with unchanged repaired bounds and volume. An ordinary unthreaded fixture also passes native roundtrip and unchanged graph comparison.

Normal Cadmata Auto preparation reports `Complete` under its original five-second budget; actual face selection and close zoom work. Helios rebuilds the same authored source through the final SDK and shows the complete bolt with zero diagnostics. Its non-AOT worker took 251.251 s under concurrent test load (compile 232.585 s, mesh 9.192 s). The earlier isolated B1 build took 209.625 s; engraving dominates this remaining latency. No text feature redesign was added.

Release solution build: **zero errors** (five existing Web runtime warnings). Core fast lane: **1,017 passed**. Full serial solution lane: **4,340 passed across 20 projects, zero failures/skips**, including 1,150 Core, 2,012 Firmament and 62 Server tests. The final authored topology/display audit and 13 documentation qualification checks pass. Logs: `artifacts/local/p4-01b-final-build.log`, `p4-01b-final-fast.log`, `p4-01b-final-full.log`, `p4-01b-final-audit.log`, and `p4-01b-final-doc-tests.log`. The test-regenerated tracked ellipse STEP is restored; its current-export copy is retained only in ignored evidence. The intended NIST snapshot change contains 16 serialization hashes and no status, diagnostic or topology changes.

## Deliverables

- [Canonical Firmament source](../../fixtures/Thread/hexbolt-showcase.firmament)
- [Final STEP](../../artifacts/local/p4-01b/showcase/bolt-cli.step)
- [Cadmata full bolt](../../artifacts/local/p4-01b/showcase/cadmata-full.jpg)
- [Head/thread and under-head fillet](../../artifacts/local/p4-01b/showcase/cadmata-head-thread.jpg)
- [Tip/thread transition](../../artifacts/local/p4-01b/showcase/cadmata-tip-thread.jpg)
- [Maker mark](../../artifacts/local/p4-01b/showcase/cadmata-maker-mark.jpg)
- [Final face selection](../../artifacts/local/p4-01b/showcase/cadmata-selection.jpg)
- [Final Helios witness](../../artifacts/local/p4-01b/showcase/helios-final.jpg)
- [Before entity/size profile](../../artifacts/local/p4-01b/compactness/before-profile.json), [after profile](../../artifacts/local/p4-01b/compactness/after-profile.json), [compactness comparison](../../artifacts/local/p4-01b/compactness/comparison.json)

Generated evidence remains ignored under `artifacts/local/`. The dedicated cleanup removes phase probes, the old root-rod/annular-shoulder stitch, and canonical dependency on the deprecated C# showcase generator. No manufacturing runout, broad Boolean/kernel redesign, cosmetic thread substitution, lossy precision, or Preview 4 distribution work was introduced.
