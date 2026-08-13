# M1 API architecture and domain model

`Aetheris.Geometry` owns `BoundedParametricCurve3`, `BoundedParametricPatch3`, `ParameterDomain1`, `ParametricDomain`, identity, provenance, representation classification, and shared first-jet singularity vocabulary. Kernel.Core owns native analytic supports. Surfacing and Piping adapt semantic objects to the geometry contract and remain owners of CAD intent.

`CurveJet1` and `SurfaceDifferential` both implement the deliberately small `IFirstJet3` (`Point`, `Singularity`). Dimension-specific raw derivatives remain explicit rather than forced into a generic collection. This is the future second-jet seam: M2 should add sibling `CurveJet2` and `PatchJet2` results beside these types, then implement curvature as evidence-producing queries over those results.

`ParameterDomain1` requires finite strictly increasing endpoints, inclusive membership, deterministic value equality, clamping, and normalized mapping. Reversed native trims keep an increasing public domain but map it to decreasing native parameters. Thus a curve's derivative is always aligned with authored public orientation.

Generated and imported/materialized status is carried by `GeometryProvenance.IsGenerated` and `GeometryRepresentationKind`; neither changes query evidence. No new curve-specific representation or predicate enum was introduced.
