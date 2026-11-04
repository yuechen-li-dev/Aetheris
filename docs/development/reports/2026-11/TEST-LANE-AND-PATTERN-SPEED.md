# Test lane and pattern build speed

## What changed

- **STEP import memo keyed by (text, policy).** It used to skip any import with an `ImportPolicy`, which every
  inline-STEP and edit path passes. FTC-09 edit tests: 91 s to 21 s; CLI build: 10.0 s to 8.0 s.
- **Congruent perforation openings share one realization.** `BrepHollowRadialCutPattern` realizes one opening
  per radius (exact curves, host and cutter pcurves, certificates). It places every other opening by rigid
  motion, checked against that instance's exact authority, and falls back to full realization if the check
  fails. Identical holes now get identical representations; before, adaptive span selection varied with
  position.
- **Perforation STEP verification by congruence.** One- and two-opening specimens are exported and
  round-tripped with the full analytic checks. The full file must then hold exactly
  `count(1) + (N − 1) × (count(2) − count(1))` entities of every type except the writer's shared leaf geometry
  (`Step242EntityCensus`). Patterns of two or fewer openings still reimport in full. The report records
  `VerificationBasis`. The full body is still validated in memory first (bindings, every pcurve at 129 samples,
  preflight, manifold).
- **Test trims.** FTC-09 invalid-intent cases compile once (validation runs the build), with one build
  agreement test. The FTC-09 placement cases share one compile. The CLI plan/lock test drops a redundant
  `validate --locked`. FEA determinism repeats the cheap single-bend solve instead of the two-bend channel.
- **`AGENTS.md`:** the full lane is parallel by default; the serial lane is an optional determinism check.
  The CI workflows still run serially.

## Evidence

133-opening perforated sleeve (`FirmamentPerforationTests` source):

| | cold | warm, fresh geometry |
| --- | --- | --- |
| before | 8.4 s | 3.76 s (export 1.28, reimport 2.32) |
| after | 4.6 s | 2.19 s (export 1.44, verification 0.55) |

The exported STEP is 18.6 MB: each intersection curve is a cubic B-spline accurate to the kernel's linear
tolerance (about 25 spans). Export is now writer-bound. Faster float formatting was tried and dropped: the
current format caps output at 15 significant digits, and the alternative was both different and slower.

Full lane, this 2-core container: serial 855 s; parallel 603 s, all 20 assemblies green, peak about 3.7 GB.

## Verdict

Success for the import memo and the perforation route. The remaining perforation cost is STEP serialization of
dense exact curves, which is a representation decision (tolerance or span merging), not redundant work.
