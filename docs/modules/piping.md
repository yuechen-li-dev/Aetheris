# Piping Module

`Aetheris.Piping` 0.1.0 owns `Piping.PathPipe` and `Piping.PipeRoute`. A pipe is authored by engineering intent—centerline, section, bend radius, and route policy—not by exposing generic Sweep and twist knobs.

M0 admits a solid circular section, straight PathPipe, and one planar positive 90-degree route with inlet straight, circular bend, and outlet straight. `PipeRouteIr` preserves line/arc/line centerline elements and their stable identities. Lowering uses shared exact cylinder/torus/cylinder surfaces and one closed BRep shell; Kernel.Core is not taught what a pipe is.

For a circular section, rotation about the tangent does not change geometry. The deterministic frame rule transports section phase with the route-plane normal and fixes the seam at the outward radial direction through the bend. Users do not receive generic sweep-twist controls.

The resulting `SemanticValue` exposes inlet/outlet points and axes, bend start/end, exact centerline construction identity, and diameter. `StandardPipeElbow` is the first module-owned Template. The showcase emits deterministic STEP through the ordinary exporter.

Current limitations are explicit: M0 supports one +90-degree XY bend, solid circular sections, and no branch/junction policy. Wall thickness is represented in the domain section model but diagnosed as unsupported by exact route materialization; hollow pipe, arbitrary 3D routes, reducers, fittings, and route solving remain future work.

Evidence: [M0 validation report](artifacts/m0/validation-report.json), [CLI inspection](artifacts/m0/inspection-evidence.md), and [pipe route STEP](artifacts/m0/pipe-route.step).
