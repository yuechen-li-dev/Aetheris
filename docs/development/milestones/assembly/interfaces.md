# Interfaces and Mates

M0 source keeps engineering meaning above geometric lowering:

```firmament
Interface ShaftBore {
    Role Shaft requires AxisCapable, DimensionalCapable;
    Role Bore requires AxisCapable, DimensionalCapable;
    Lower AxisCoincident Shaft.Axis Bore.Axis;
    Fit Shaft.Diameter inside Bore.Diameter;
    Allow translation:along-axis;
    Allow rotation:about-axis;
}

Mate ShaftInBearing: ShaftBore {
    Shaft: BearingModule.Rotor.Shaft.Journal;
    Bore: BearingModule.FixedSupport.Bearing.Bore;
}
```

Roles are named and cardinality-ready (`Minimum`/`Maximum` in IR); the parser admits N-role Interfaces even though M0 dogfood is binary. Capability validation is exact and structural through `SemanticCapabilitySet`. M0 adds `AxisCapable`, `PlaneCapable`, `PointCapable`, and `DimensionalCapable`, backed respectively by exact analytic or toleranced bindings—never reflection, mesh IDs, or raw topology IDs.

Relational requirements lower to the bounded `PlacementConstraintKind` set: `AxisCoincident`, `AxisAligned`, `PlaneCoincident`, `PointCoincident`, and `OffsetAlongAxis`. Only AxisCoincident and the axial-seating combination are solved in M0. The Interface identity and Mate provenance survive lowering, so the source statement remains “shaft mates with bearing,” while the exact constraint is inspectable as a consequence.

The first dogfood, `ShaftBore`, checks coaxial placement and symbolic diameter fit. The second, `ShoulderBearingSeat`, lowers to axis plus plane coincidence and proves one Interface can create multiple constraints.
