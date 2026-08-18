# CTC-03 M1 source audit

Useful facts: constant 1.907540000007641 mm thickness from 15 admitted support pairs; eight planar reference regions; seven cylindrical bends; source-face/edge provenance; bend adjacency; two exact planar inner-loop cuts; and deterministic flat mappings.

Incidental structure: generated identifiers such as `region-p-0065-0069`; separation of reference skins according to BRep face topology; boundary vertex segmentation; and 90 unmatched cap/cut/support faces. These are forensic evidence, not original CAD history.

Noise: repeated `...0000` coordinate tails, values near zero, 89.999999999999° angles, 6.350000000025 mm radii, and the measured thickness. They are retained in evidence and removed from reconstructed authority only through explicit nominal decisions.

Engineering interpretation was required to identify the dominant planar region as `MainDeck`, surrounding regions as front/rear/left/right walls, two return mounting flanges, one 45° service flange, and the equal openings as a vent pair. The source does not prove the original feature order, shop K-factor, pattern history, or relief family.
