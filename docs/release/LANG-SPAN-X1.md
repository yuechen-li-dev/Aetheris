# LANG-SPAN-X1 — geometric span views

> Subsequent planar-support qualification: [LANG-SPAN-SURFACE-X1](LANG-SPAN-SURFACE-X1.md). This document retains the original curve-view milestone verdict.

## Verdict

**Meaningful progression.** Firmament can now name a bounded curve view over an existing planar line or circle guide and compose that view into the established Profile/Path pipeline without creating a copied guide. It also has a semantic `Span<Plane>` declaration that retains a named boundary Profile, but planar support validation and feature/FEA consumption are intentionally not claimed yet.

## Existing seam audit

`Segment { Trace; From; To }` already created an owner-local bounded curve for a Profile; Concept Path and `|>` already consume named guides; `ResolvedProfileSegment2D` preserves source provenance. Those mechanisms are retained. BRep trim loops, `SurfacePatch`, Regions, imported topology selectors, PMI, and FEA selections stay distinct: they are not a second Span implementation and are not rebound by nearest geometry.

## Admitted types

| Span type | Domain representation | Orientation | Consumers | Status |
| --- | --- | --- | --- | --- |
| `Span<Line>` | named endpoints on a Line2/Rect edge | Forward/Reverse | Profile pipeline, Concept Path pipeline | qualified |
| `Span<Arc>` | named endpoints on a Circle2, inspected as native radians | Forward/Reverse | guide binding; pipeline-compatible | qualified |
| `Span<Curve>` | the same currently admitted planar line/arc guides | Forward/Reverse | guide binding | qualified |
| `Span<Plane>` | named Profile boundary | Forward | semantic inspection only | deferred consumer |

The CLI command `aetheris inspect-spans <file> --json` reports stable span identity, parent identity/type, domain, endpoints/boundary, orientation, length when available, and provenance.

## Evidence and limits

`fixtures/Canonical/Span/line-pipeline.firmament` gives the middle 40 mm of `Stock.Bottom` the identity `MountingEdge`; the Profile pipeline consumes that name without a helper line. The focused tests prove parent provenance, arc domain/length and reverse traversal, diagnostics for invalid type/off-parent/zero length, and the planar semantic view.

An upstream parent edit is re-evaluated because the span resolves from its parent and named endpoints on every bind. It is rejected rather than clamped when an endpoint no longer lies on the parent. No STEP parity or packaged-CLI claim is made for curve or planar spans in this X1 increment; neither arbitrary trim nor a feature-support materializer was added.

Future work: explicit normalized range syntax, finite arc parent carriers, `Span<Plane>` boundary/inside validation for Hole and FEA support, then cylindrical/freeform parameter spans and composite Regions.
