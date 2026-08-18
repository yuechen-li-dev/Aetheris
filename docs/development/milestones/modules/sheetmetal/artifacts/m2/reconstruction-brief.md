Part: sheetmetal-import-2979c3b6e9cf464c
  Recovery status: Partial
  Constant thickness: 1.90754000001 mm ± 0.01
  Regions: 15 (8 planar, 7 bends)
  Bends: 7
  Cuts: 2
Strong nominal candidates:
  bend-c-0005-0024.BendAngle: 90deg -> 90deg (delta 1.98952E-13, Canonical bend angle within bounded angular tolerance.)
  bend-c-0005-0024.InsideRadius: 6.35000000003mm -> 6.35mm (delta -2.53806E-11, Common fractional/decimal-inch value converted to millimetres within tolerance.)
  bend-c-0006-0009.BendAngle: 90deg -> 90deg (delta 0, Canonical bend angle within bounded angular tolerance.)
  bend-c-0006-0009.InsideRadius: 6.35000000003mm -> 6.35mm (delta -2.54108E-11, Common fractional/decimal-inch value converted to millimetres within tolerance.)
  bend-c-0046-0050.BendAngle: 45deg -> 45deg (delta -2.38742E-12, Canonical bend angle within bounded angular tolerance.)
  bend-c-0046-0050.InsideRadius: 6.35000000003mm -> 6.35mm (delta -2.54001E-11, Common fractional/decimal-inch value converted to millimetres within tolerance.)
  bend-c-0057-0070.BendAngle: 90deg -> 90deg (delta 0, Canonical bend angle within bounded angular tolerance.)
  bend-c-0057-0070.InsideRadius: 6.35000000003mm -> 6.35mm (delta -2.53904E-11, Common fractional/decimal-inch value converted to millimetres within tolerance.)
  bend-c-0058-0064.BendAngle: 90deg -> 90deg (delta -7.10543E-13, Canonical bend angle within bounded angular tolerance.)
  bend-c-0058-0064.InsideRadius: 6.35000000003mm -> 6.35mm (delta -2.54001E-11, Common fractional/decimal-inch value converted to millimetres within tolerance.)
  bend-c-0059-0068.BendAngle: 90deg -> 90deg (delta 8.10019E-13, Canonical bend angle within bounded angular tolerance.)
  bend-c-0059-0068.InsideRadius: 6.35000000003mm -> 6.35mm (delta -2.53806E-11, Common fractional/decimal-inch value converted to millimetres within tolerance.)
  bend-c-0060-0067.BendAngle: 90deg -> 90deg (delta 6.96332E-13, Canonical bend angle within bounded angular tolerance.)
  bend-c-0060-0067.InsideRadius: 6.35000000003mm -> 6.35mm (delta -2.54001E-11, Common fractional/decimal-inch value converted to millimetres within tolerance.)
  part.Thickness: 1.90754000001mm -> 1.905mm (delta -0.00254, Common fractional/decimal-inch value converted to millimetres within tolerance.)
Likely structural groupings:
  RepeatedBendPolicy: [bend-c-0005-0024, bend-c-0006-0009, bend-c-0057-0070, bend-c-0058-0064, bend-c-0059-0068, bend-c-0060-0067] — 6 bends repeat angle 90 deg and inside radius 6.35 mm.
  RepeatedCut: [feature-profile-f0065-l0069, feature-profile-f0065-l0070] — 2 Slot cuts share size within 0.01 mm.
Ambiguities:
  - Recovered region boundaries and bend adjacency are geometric facts; authored flange/feature history is not recoverable from STEP alone.
  - K-factor 0.5 is a flattening policy assumption unless source manufacturing metadata says otherwise.
  - CTC-03 cut loops may be one repeated operation, but STEP contains no authoritative feature-history grouping.
  - Machine recovery status is Partial; unsupported boundary faces remain forensic evidence.
Verification targets:
  thickness; bidirectional formed boundaries; bend axes/angles/radii/adjacency; flat outline/cuts/bend lines; topology and DFM
