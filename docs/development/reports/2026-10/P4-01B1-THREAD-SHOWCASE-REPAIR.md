# P4-01B1 — Thread showcase repair

## Construction

Canonical source: `fixtures/Thread/hexbolt-showcase.firmament`. The standalone Firmament compiler creates continuous 8 mm major-diameter stock with a bounded inward helical groove. The established helical law, flank/root realization certificates, and complete-turn seams are reused. Known head, fillet, shank, and tip boundaries compose directly through shared major-radius rim edges. There is no independently attached root-stock rod, annular replacement shoulder, general Boolean, mesh mutation, or fixture-only geometry rewrite.

The shaft is deliberately 52 mm instead of the former 50 mm. Keeping the original 1.25 mm pitch, 47.5 mm span, and 38 turns on 50 mm stock left only 0.26875 mm total land after accounting for the groove footprint and transitions. The new source uses `StartOffset: 1.5mm`, leaving 0.953125 mm after the existing 0.2 mm under-head fillet and 1.315625 mm before the existing 0.9375 mm tip chamfer. These dimensions are authored data. The existing `StartOffset` and `Length` semantics control other bolts too. No tapered runout was needed.

The optional HexBolt fields `MakerMark`, `MakerMarkHeight`, and `MakerMarkDepth` invoke the existing planar text/section-stack machinery. CODEX remains 2.5 mm high and 0.2 mm deep with two counters. The old `ThreadedHexBoltMakerMark` C# generator is a deprecated compatibility adapter; the canonical build has no dependency on it. Unsupported reusable assembly engraving is rejected explicitly rather than silently omitted.

## Wire-only audit and native STEP

The historical 37 wire-only display faces were a time-budget defect, separate from the poor physical termination. Re-running the original STEP with the previous five-second whole-body preparation budget omitted 39 B-spline faces in this run (IDs 138–176). The exact count depends on how far tessellation gets before cancellation. Every missing face has a timeout diagnostic. A complete 60-second audit shades every original required face. The audit compares all BRep face IDs against shaded patches, including faces absent from partial results.

An initial 30-second product budget proved the missing faces were computable. That temporary increase was removed after correcting the evaluator cost below. Normal Cadmata now returns `Complete` within its original five-second preparation budget, shades all 293 faces, and produces zero wire-only faces. Diagnostic fallback and CIR policy are unchanged.

The normal Helios regression exposed a separate numerical cost: tensor-product B-spline evaluation computed every V row although U de Boor consumes only `DegreeU + 1` rows. The evaluator now computes the active rows and reuses the same blend arithmetic. Polynomial and rational nonuniform nets compare exactly against independent 1D tensor evaluation, including interior knots and clamped endpoints. This changes computation cost, not geometry or STEP serialization.

Independent native import also exposed invalid cubic knot multiplicities in the previous helical spline realization: interior knots repeated four times for degree three. Adjacent Hermite spans now share endpoint controls, with multiplicity three internally and four at the ends. The represented polynomials and error certificates are unchanged. This obeys the [OCCT B-spline curve contract](https://occt3d.com/dev/doc/refman/html/class_geom___b_spline_curve.html) and [surface contract](https://occt3d.com/dev/doc/refman/html/class_geom___b_spline_surface.html). It is a geometry representation correctness fix made in B1, before compactness work.

The normal CLI builds the repaired STEP and Aetheris reimports it as one shell/body: 293 faces, 689 edges, 406 vertices. Every edge has two uses; enclosure and orientation checks pass. FreeCAD 1.0.2 / OCCT independently imports one valid, closed solid with the same 293 faces, 693 edges and 408 vertices (its translator adds seam splits). Native volume is 2938.769841889224 mm³; bounds are X [-5.3, 52], Y/Z approximately [-7.505553499465, 7.505553499465] mm. The tessellation-based Aetheris mass approximation is not claimed as an exact native-volume oracle.

## Evidence

Generated outputs are ignored under `artifacts/local/p4-01b/`:

- `before/bolt.step`: original unmarked X1 thread witness, 8,798,446 bytes.
- `before/engraved-bolt.step`: preserved original engraved X2 witness, 9,028,303 bytes.
- `before/bolt.product-display.json`: old-budget missing-face identities and timeout reasons.
- `before/cadmata-full.jpg`, `before/cadmata-transitions.jpg`: old construction in the current viewer with a complete preparation budget.
- `showcase/bolt-cli.step`, `showcase/cli-build.json`, `showcase/audit.json`: normal authored build, roundtrip and topology/display evidence.
- `showcase/freecad.json`: independent native import.
- `showcase/cadmata-full.jpg`, `cadmata-head-thread.jpg`, `cadmata-tip-thread.jpg`, `cadmata-maker-mark.jpg`: actual normal Cadmata screenshots. Head close-up also records a highlighted selected thread face.

The source, compiler ownership, deprecated generator, repeated dimensions, shared-edge composition, pcurve domain mapping, and temporary debug geometry were reviewed. No authored one-off C# bolt surgery remains in the canonical path. Historical X1 and X2 release notes now point here.

## Verification

Release solution build: zero errors. Core fast lane: 1,006 passed. Full serial solution lane: 4,327 passed, zero failures. After cleanup, 18 focused Firmament/thread/mark tests and the normal Cadmata display integration test pass. Final combined milestone validation is recorded in the closeout.

**B1 verdict: Accepted.** The normal Helios local editor rebuilt the canonical source through the current Aetheris SDK, displayed one definition/occurrence, and reported zero diagnostics with source/display revision 1. Its non-AOT worker took 209.625 s (compile 192.769 s; mesh 8.512 s; transfer 24 ms). Temporary phase diagnostics isolated approximately 169 s in the established engraving path, then were removed. No text geometry redesign was attempted. This browser latency remains a practical limitation, not a failed geometry check. Source was pasted into the local editor because extension file upload was unavailable; no Helios repository source or dependency installation was changed. `showcase/helios-b1.jpg` and `helios-b1-phases.json` record this independent product path. B2 began only after this B1 acceptance.
