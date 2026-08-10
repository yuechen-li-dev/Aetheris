# Multi-part stackup proof

Automatically derived unique chain:

1. Housing seat depth: 30.00 -0.04/+0.05 mm (`HousingTable.H6204.SeatDepth`)
2. Housing/Bearing Mate transition: 0 mm
3. Bearing width: 10.00 ±0.02 mm (`BearingTable.6204.Width`)
4. Bearing/Spacer Mate transition: 0 mm
5. Spacer width: 5.00 -0.02/+0.03 mm (`SpacerTemplate.Width`)
6. Spacer/Shaft transition: 0 mm

Nominal 45.00 mm; worst case 44.92–45.10 mm. `AxialReach >= 44.90 mm` passes. The failing fixture requires 44.95 mm, returns `assembly-tolerance-assertion-failure`, and retains its complete five-edge chain in `failing-assembly-ir.json`.
