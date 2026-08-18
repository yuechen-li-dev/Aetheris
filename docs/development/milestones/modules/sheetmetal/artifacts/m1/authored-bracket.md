# Authored U-channel

Source: `fixtures/SheetMetal/simple-u-channel.firmament`.

- Thickness 1.5 mm; 5052-H32 Aluminum provenance string; K = 0.42.
- Base 100 × 60 mm; left/right flange heights 25/20 mm; two 90° bends at R2 mm.
- Two Ø8 mm base holes at (25,30) and (75,30).
- Formed exact BRep: one enclosed manifold, 28 vertices, 40 edges, 16 faces, 10 planes, 6 cylinders.
- Bounds: (-3.5, 0, 0) to (103.5, 60, 25) mm.
- STEP export/reimport succeeds through the ordinary AP242 path.
- Each bend neutral radius is 2 + 0.42×1.5 = 2.63 mm; allowance is π/2×2.63 = 4.13119433947 mm.
- Valid flat envelope: 146.262388679 × 60 mm. Two cut loops and two bend lines map successfully.
- Reference-surface flat→re-fold check: point and bend-angle residuals within 1e-8 mm/rad.
- Deterministic flat hash: `a5ce4b0368a79014b3505f3613f3449cf5d884569e3694780b4d55354b55e30d`.

The formed body is explicit analytic topology produced by the Sheet Metal domain builder; it does not invoke generic `BrepBoolean`.
