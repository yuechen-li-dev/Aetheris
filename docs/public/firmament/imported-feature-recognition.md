# Imported feature recognition: current limits

`Recognize` currently binds explicit face references to a named region. Authored
kind, confidence and optional evidence/proposals are not equivalent to automatic
feature recognition or a complete geometric verification. Resolving a face ID
proves which face was named; it does not prove that face is a hole wall.

A bounded `Replace ... With Hole<Shaft>` path exists for one canonical imported
body and one through-hole replacement. It rebuilds a Box plus a semantic shaft
hole using the existing materializer, then exports STEP. Its report explicitly
states `emissionStrategy: holeShaft-bounded-rebuild` and `residualSurgery: false`.
Residual face counts are migration accounting, not proof of preserved residual
geometry. This is not general hole resizing in an arbitrary imported part.

The current implementation checks for one matching-radius cylindrical support,
but does not establish complete correspondence between the recognized region,
the imported host and its declared HostSize. A planar region labeled as a hole
can currently pass this replacement path. An inconsistent HostSize can change the
rebuilt host. These are known admission gaps; do not use this prototype as evidence
that arbitrary imported geometry is preserved or that a replacement was verified.

The canonical positive witness is
`fixtures/Canonical/Integration/inline-step-recognize-replace.firmament`.
It is a constrained integration fixture, not a general modeling recipe. See the
[current audit](../../release/HOLE-RECOGNIZE-REPLACE-AUDIT-2026-10-20.md) for live
reproduction and the proposed bounded next step. No broader capability is implied
by proposal text or a replacement-ready metadata report.
