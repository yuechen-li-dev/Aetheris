# Authored lowering audit

Before M3, the M1 rectangular two-opposite-90°-flange family independently generated a base, planar flanges, cylindrical bend skins, circular base holes, thickness walls, a closed formed BRep, and a region-based flat pattern. M2 reconstruction renamed and nominalized recovered regions but retained recovered planes, polygons, transforms, source face/edge bindings, and the imported formed body.

M3 removes those dependencies from the normal authored path. `AuthoredSheetMetalCompiler` parses a rectangular named base, an acyclic flange graph, cuts, directions, corner/relief policies, and one K-factor policy. `AuthoredSheetMetalLowering` derives all region frames and analytic bend geometry. `AuthoredSheetBrepEmitter` shares known tangent topology and emits planes/cylinders/cut walls without generic Boolean union. `SheetMetalFlattener` traverses the same graph and cancels shared contour segments to produce one outer boundary.

Forensic `RecoveredRegion`/`RecoveredBend` compilation remains evidence-linked by design; it is not the canonical authored route.
