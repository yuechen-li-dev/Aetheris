# Semantic piping and local autorouting

Aetheris X3/X3a separates connection intent from route geometry and hardens the equipment endpoint seam:

```text
PipingSystem → equipment-owned Port + Nozzle → Connection
→ explicit Route or RouteRequest → RouteProposal → accepted Route
→ endpoint Mate + PipeSegments + fittings → Assembly → BOM/Cut List → AP242
```

The router is a design assistant. `RouteProposal` is inspectable; acceptance copies its ordered anchors into an ordinary `RouteIr`. The accepted route retains provenance but no solver ownership. Rebuilding an explicit or previously materialized route does not invoke pathfinding.

## Ports and logical connections

`Port` is the piping specialization of an Assembly Interface. It has stable identity, a position/DatumFrame, an outward axis direction, a `PipePolicy`, and a bounded connection style.

```firmament
PipePolicy Pipe25 {
    OuterDiameter: 25mm;
    WallThickness: 2mm;
    Material: "Standard.Materials.StainlessSteel.304_Annealed";
}
Port PumpDischarge {
    Position: [100mm,100mm,250mm];
    Direction: [1,0,0];
    PipePolicy: Pipe25;
    ConnectionType: Flange;
    Equipment: Pump;
    NozzleLength: 50mm;
}
Connection Supply {
    From: PumpDischarge;
    To: CoolerInlet;
    PipePolicy: Pipe25;
    Service: "CoolingSupply";
}
```

Connections exist before routes. Different port policies require `Reducer: true`; different connection styles require `Adapter: true`. X3 records those requirements but does not claim a standards database.

Direct `PipePolicy` declarations require all three fields: `OuterDiameter`, `WallThickness`, and `Material`; there are no implicit dimensional or material defaults. Both dimensions must be positive and twice the wall thickness must be less than the OD.

## Equipment-owned nozzles and endpoint mates

X3a associates a port with one equipment proxy and materializes the gap between the equipment face and route endpoint as a hollow semantic nozzle occurrence:

```firmament
KeepOut PumpBody {
    Min: [0mm,0mm,0mm];
    Max: [50mm,300mm,350mm];
    Concept: EquipmentProxy;
}
Equipment Pump { KeepOut: PumpBody; }
Port PumpDischarge {
    Position: [100mm,100mm,250mm];
    Direction: [1,0,0];
    PipePolicy: Pipe25;
    ConnectionType: Flange;
    Equipment: Pump;
    NozzleLength: 50mm;
}
```

`Position` is the pipe-side mating plane. The nozzle root is `Position - Direction × NozzleLength` and must lie exactly on the outward face of the equipment's KeepOut. The nozzle exposes `EquipmentMate` at its root and the stable port Interface as `PipeMate` at its tip. The first or last pipe segment exposes its own endpoint Interface at the same position with the opposite outward direction; `PipingMateIr` records that coincidence and opposition explicitly.

The only clearance exemption is an inspectable `PipingKeepOutExemptionIr` scoped as `NozzleEnvelopeOnly` for that port's nozzle against that port's owning equipment KeepOut. It does not apply to the route, another port, another nozzle, or any foreign KeepOut. Thus a nozzle may touch its own equipment face while route and nozzle clearance remain fully enforced everywhere else.

The generic shipped product identities are `Standard.Piping.Pipe`, `Standard.Piping.Elbow90`, and `Standard.Piping.Tee`. Their dimensional policies are available from `Use Standard.Piping`. They are generic products, not ASME, DIN, or ISO parts.

## Explicit routes

An explicit route is an ordered axis-aligned 3D path. The first run must follow the source port's outward direction and the final run must approach opposite the destination's outward direction.

```firmament
Route SupplyRoute {
    Connection: Supply;
    Through: [[0mm,0mm,0mm],[300mm,0mm,0mm],[300mm,500mm,0mm]];
    BendRadius: 50mm;
    LockedSegments: [1];
    Provenance: Explicit;
}
```

Interior anchors become semantic 90-degree turns. Straight intervals lower to hollow `PipeSegment` components using the declared OD and wall. Each turn lowers to a hollow analytic-torus `Standard.Piping.Elbow90` occurrence with named `Inlet` and `Outlet` Interfaces. Pipe segments stop at elbow tangent points; cut length is therefore distinct from corner-to-corner route length.

For the admitted 90-degree route, a segment's cut length is its corner-to-corner interval minus one `BendRadius` for each adjacent interior turn: one radius at an end interval and two on an interval between two elbows. Validation rejects routes whose anchor spacing cannot accommodate those tangent setbacks.

## KeepOut, clearance, and autorouting

`KeepOut` is semantic obstacle geometry; names have no routing meaning. X3 uses conservative axis-aligned bounds for structural members, equipment proxies, and explicit service volumes.
An existing structural body is not implicitly routable space: author or generate a `KeepOut` from its conservative bounds. This makes avoidance semantic and inspectable instead of depending on object names.

```firmament
KeepOut Frame {
    Min: [400mm,-100mm,-100mm];
    Max: [600mm,100mm,100mm];
    Concept: StructureMember;
}
RouteSpace SkidRouting { Clearance: 30mm; }
RouteRequest SupplyRequest {
    Connection: Supply;
    Clearance: 30mm;
    BendRadius: 50mm;
    HardWaypoints: [];
    AcceptAs: SupplyRoute;
}
```

Forbidden centerline space is the obstacle bound inflated by pipe radius plus required clearance. Candidate search conservatively adds one bend radius to that planning inflation so an elbow torus cannot cut inside an otherwise valid sharp corner; the reported final clearance is then recomputed against the actual trimmed straights and torus centerlines without that extra planning margin. The deterministic router builds coordinates from endpoints, hard waypoints, and inflated obstacle faces, then searches the adjacent 3D coordinate graph with A*. Cost is route length plus a 1000 mm penalty per elbow, so small length savings do not add fittings. Neighbor order and priority tie-breaking are stable. Collinear search points are removed before the proposal is exposed.

`RouteSpace.Clearance` is the system default. `RouteRequest.Clearance`, when present, is the request-local value used for proposal and final verification; current fixtures repeat it deliberately so generated state remains self-evident. `AcceptAs` performs the explicit acceptance step and names the materialized ordinary route. Omit `AcceptAs` to inspect a proposal without adding route geometry. Search neighbor order is `+X, -X, +Y, -Y, +Z, -Z`; exact anchors also depend on port positions/directions, policy radius, bend radius, hard waypoints, and obstacle bounds.

`aetheris inspect source.firmament --json` reports proposals separately from accepted routes. A successful proposal includes anchors, length, elbow count, minimum clearance, cost, and diagnostics. Failures distinguish endpoint direction, hard-waypoint reachability, excessive clearance, disconnection, and lock conflicts.

## Local rerouting

`LocalReroute` authorizes one anchor interval:

```firmament
LocalReroute AroundMotor {
    Route: SupplyRoute;
    FromAnchor: 0;
    ToAnchor: 2;
    Avoid: [Motor];
    Clearance: 25mm;
}
```

The prefix before `FromAnchor` and suffix after `ToAnchor` are copied unchanged. Locks outside the interval are shifted only by the number of replacement anchors; a lock inside the interval fails with `piping-local-reroute-crosses-lock`. The resulting ordinary route has `LocallyRerouted` provenance.

Anchor and segment ordinals are zero-based. The authorized interval includes both boundary anchors but only segments with ordinals `FromAnchor .. ToAnchor-1`; the `ToAnchor` and the segment leaving it begin the preserved suffix. `LocalReroute` updates the named route in place and retains its identity—there is no `AcceptAs` because the operation is already an explicit authored edit. `LocalReroute.Clearance` overrides the RouteSpace default for the replacement interval. All system KeepOuts remain active; `Avoid` adds/names edit-specific obstacles and never disables unlisted KeepOuts.

## Build and fabrication output

```powershell
aetheris inspect fixtures/Canonical/Piping/autorouted-connection.firmament --json
aetheris build fixtures/Canonical/Piping/pump-skid.firmament --output artifacts/local/x3a/x3a-pump-skid.step --json
```

Build emits an AP242 occurrence assembly plus sibling `.routing.json`, `.cutlist.json`, and `.bom.json`. The routing report contains the system, equipment, ports, nozzles, endpoint mates, scoped exemptions, connections, proposals, accepted routes and their anchors/segments/turns/provenance, KeepOuts, realized pipe segments/fittings, minimum clearance, and timing evidence. The Cut List contains deterministic fabrication groups keyed by policy/material/cut length; the BOM separately groups semantic pipe, fitting, and nozzle-stub products. Routing proxies are separate named assembly components; they are not equipment-detail models. Grouping uses semantic product/policy/material and cut length, never geometry hash alone. Final qualification independently checks exact axis-aligned cylinder/nozzle centerline intervals and each realized elbow's torus centerline against the applicable original KeepOut bounds, then subtracts the pipe radius. Elbow arcs use deterministic dense sampling with the maximum chord sagitta subtracted, so the reported clearance is a conservative lower bound rather than an optimistic sample.

## Bounded support

Supported:

- logical Connections independent of geometry;
- stable port Interfaces and endpoint directions;
- equipment-owned hollow nozzle stubs, scoped owner exemptions, and coincident endpoint mates;
- explicit orthogonal 3D routes and 90-degree elbows;
- deterministic local autorouting around axis-aligned KeepOut bounds;
- hard waypoints, locked segments, and bounded local rerouting;
- hollow analytic pipe and elbow bodies;
- AP242 assembly, BOM, pipe Cut List, and routing evidence.

Not supported:

- freeform or spline routing, arbitrary bends, or 45-degree elbows;
- tees in automatic branch realization (the generic Tee product policy is shipped for future branch lowering);
- reducers, flanges, and adapters as automatic geometry;
- penetrations, pipe supports, spools, pressure class, or standards lookup;
- stress, flow, gravity-slope, or plant-scale optimization.

Copy the [autorouted connection fixture](../../../fixtures/Canonical/Piping/autorouted-connection.firmament), [local reroute fixture](../../../fixtures/Canonical/Piping/local-reroute.firmament), or [pump skid fixture](../../../fixtures/Canonical/Piping/pump-skid.firmament) as current examples.
