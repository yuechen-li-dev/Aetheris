# Oriented through-shaft holes in the Helios graph

Helios Simple Bracket retained its XZ Profile and 20 mm extrusion. At the current
master baseline, adding `Hole<Shaft>` through the graph reached the compiler and
was refused with `compose-oriented-feature-not-admitted`. The orientation guard
also rejected a simple axial shaft cut that the local section-stack emitter can
transport with its host.

The parser now admits only a Shaft on local +Z (including its existing Top
aliases), terminated with ThroughAll, through that guard. It still rejects the
other oriented feature adapters. The section arrangement, materialization,
manifold checks, and source correspondence remain the existing owners.

The regression fixture is
`fixtures/Regression/OrientedComposeHoles/simple-bracket-through-hole.firmament`.
At local center (1.5, 20) mm and diameter 2 mm it drills through the 20 mm
extrusion. Native tests cover XY, XZ, and YZ, exact volume
`4020 - 20*pi` mm³, closed orientation-consistent material, the cylinder's world
axis, semantic entry/exit/wall ownership, and STEP reimport. Seven focused cases
pass; a 6 mm cut at that center rejects because it separates the narrow leg.

The Helios graph regression authors the feature only through form controls,
Preview and Apply, downloads its source and STEP, checks the original bracket
bounds and closed shell, and validates the downloaded source through the CLI.
The standalone CLI `analyze volume` command currently defers this oriented
curved shell; exact-volume evidence comes from BRepMassProperties in the native
regression instead. Helios also explains why Apply is disabled before Preview.

Reproduce with the source-built browser SDK and
`playwright test --config playwright.graph.config.ts --grep "Simple Bracket accepts"`.
Native focused checks use
`dotnet test Aetheris.Kernel.Firmament.Tests -c Release --filter FullyQualifiedName~OrientedComposeShaftHoleTests`.
Raw baseline, build and full-suite logs remain under `artifacts/local/oriented-shaft-*`.

The complete affected Firmament suite passes 3,481 tests with
`DOTNET_PROCESSOR_COUNT=2`; the seven new focused cases are included. Helios's
244 unit tests pass as well. All 13 Helios graph authoring tests pass against
the production AOT frontend, including the new bracket case with
`HELIOS_GRAPH_REQUIRE_AOT=true` asserting an actual AOT native WASM request.

The full parallel solution suite was attempted with and without a competing AOT
build. It reports additional tessellation execution-budget failures and NIST
audit snapshot mismatches in unchanged code. These remain qualification limits;
this change does not adjust their budgets, baselines, or geometry. The idle
parallel attempt reports 15 failures: 11 Core, two Firmament, one CLI and one
EditableV8. The affected Firmament failures disappear in the complete bounded
process run above. This is not a claim that the unrestricted solution gate passes.
The unmodified Core NIST audit class separately reproduces two hash mismatches
among 17 cases. That test project has no dependency on the modified Firmament
project.

This capability drills along the extrusion axis. A mounting hole through a broad
bracket leg face, or another oriented support/termination/variant, requires its
own support adapter and is not established by these results.
