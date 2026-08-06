# Composed-host Counterbore and PMI (X6)

Profile/Compose used to take an early geometry-only export route. It admitted
through `Hole<Shaft>` cuts, but bypassed the normalized V2 PMI records and had
no stepped Counterbore plan.

X6 admits one bounded host class: a single enclosed, constant-XY-section
Profile/Compose stock body with an implicit or explicit world-XY/+Z placement.
`Hole<Counterbore>` is currently a top-entry (`On: +Z`) `ThroughAll` feature.
Its shaft and counterbore circles must each lie strictly inside the base profile;
counterbore depth must not exceed host depth; and the largest counterbore circle
must be disjoint from every other circular cavity. Other axes, blind endings,
touching cavities, and multi-body/non-prismatic hosts are rejected with typed
`ComposeCounterbore*` diagnostics.

The authoritative plan is a prismatic section stack, not a Boolean operation:
the shaft circle removes material over the full extrusion interval, while the
larger counterbore circle removes material over the entry-depth interval. The
stack emits exact cylindrical walls, the planar shoulder transition, entry and
exit loops, and stable semantic descendants for Mouth, CounterboreWall,
Shoulder, ShaftWall, and Exit.

All V2 PMI now passes from the normalized document through the common AP242
export boundary, including for Profile/Compose. `HoleDiameter` targets the
shaft diameter of a Counterbore feature; it does not silently dimension the
larger counterbore relief. Datum targets continue to resolve through the
canonical face selector binder.

The acceptance source is
`fixtures/FirmamentV2/Canonical/valid/profile-compose-l-bracket-counterbore-pmi.firmament`.
It has two Pattern-generated shaft holes, one Counterbore, Datum A, and a
toleranced HoleDiameter callout. Its analytic volume is
`28800 - 540π = 27103.539816... mm³`.

Polygon-boundary EdgeFinish remains deliberately unsupported on Profile/Compose.
This work neither adds arbitrary boundary finishing nor generic Boolean support.
