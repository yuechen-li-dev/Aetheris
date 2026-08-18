# Mate / Interface table proof

| Mate | Interface | Role | Participant | Required capabilities | Lowered consequence |
|---|---|---|---|---|---|
| ShaftInBearing | ShaftBore | Shaft | BearingModule.Rotor.Shaft.Journal | AxisCapable, DimensionalCapable | AxisCoincident + diameter fit |
| ShaftInBearing | ShaftBore | Bore | BearingModule.FixedSupport.Bearing.Bore | AxisCapable, DimensionalCapable | AxisCoincident + diameter fit |
| ShaftShoulderAtHousing | ShoulderBearingSeat | Shoulder | BearingModule.Rotor.Shaft.ShoulderInterface | AxisCapable, PlaneCapable | AxisCoincident + PlaneCoincident |
| ShaftShoulderAtHousing | ShoulderBearingSeat | Seat | BearingModule.FixedSupport.Housing.SeatInterface | AxisCapable, PlaneCapable | AxisCoincident + PlaneCoincident |

All participant IDs and per-Mate constraint IDs are present in `assembly-ir.json`.
