# CIR-RECOVERY-V13.2: explicit stepped-hole tier placement semantics

## Why this milestone exists
A4.1 proved the production stepped executor could not reconstruct medium/large tier placement from the prior `HoleRecoveryPlan` contract. The blocker was `missing-stepped-entry-side-polarity`. V13.2 hardens plan semantics so stepped tiers carry explicit anchor/depth/z-span metadata.

## Placement contract
`HoleProfileSegment` now carries explicit tier placement fields:
- `AnchorSide` (`Top`, `Bottom`, `Through`, `Unknown`)
- `DepthFromAnchor`
- `ZMin`
- `ZMax`
- `IsThrough`
- `PlacementDiagnostics`

### Coordinate convention
Host/world Z follows existing Aetheris BRep conventions:
- host min Z = `HostTranslation.Z - HostSizeZ/2`
- host max Z = `HostTranslation.Z + HostSizeZ/2`
- top entry means contact at host max Z
- bottom entry means contact at host min Z

`ZMin/ZMax` are stored in host/world coordinates and are the intended subtraction span for that segment.

## Canonical top-entry stepped example
For host z-range `[-10,+10]`:
- small through tier: `AnchorSide=Through`, `IsThrough=true`, span includes `[-10,+10]`
- medium tier depth 8 from top: `AnchorSide=Top`, `ZMin=+2`, `ZMax=+10`
- large tier depth 4 from top: `AnchorSide=Top`, `ZMin=+6`, `ZMax=+10`

## Bottom-entry mirroring policy
If stepped relief tiers touch host min Z, both blind tiers use `AnchorSide=Bottom` and their Z spans mirror top-entry behavior about the host midpoint. Mixed medium/large anchor sides are rejected as unsupported.

## Relation to FrictionLab route
FrictionLab succeeded because medium/large z spans were explicit. V13.2 aligns production plan metadata with that convention and adds tests that compare radii/order/z spans against that successful route model.

## Why execution remains deferred
V13.2 is contract hardening only. Production stepped execution remains intentionally deferred with:
- `stepped-execution-route-disabled-until-v13.3`

No stepped Boolean execution is attempted in this milestone.

## V13.3 next step
Re-enable stepped production execution using plan-provided placement semantics (`AnchorSide`, `DepthFromAnchor`, `ZMin/ZMax`, `IsThrough`) and keep route-equivalence tests as an execution gate.

## V13.3 execution consumption update
V13.3 re-enables bounded stepped execution and now treats these placement fields as executable source-of-truth:
- executor validates explicit `AnchorSide`, `IsThrough`, and concrete `ZMin/ZMax` for every tier,
- through tier must explicitly declare `AnchorSide=Through` and `IsThrough=true`,
- medium/large tiers must be blind and share one concrete entry anchor side,
- tool construction uses `height = ZMax - ZMin` and `centerZ = (ZMin + ZMax)/2` with no hidden span inference.

Route is explicitly `repeated-subtract-small-medium-large`; any placement inconsistency rejects before Boolean.
