# Corner and relief summary

Adjacent base flanges create stable `SheetMetalCornerIr` records. Supported policies are bounded `Open`, open-seam `Mitered`, and `Relief`. Automatic relief derives width >= thickness and depth = inside radius + thickness and records the values/provenance in `SheetMetalReliefIr`. The formed body realizes corner clearance by deterministic bend/flange end setbacks and explicit exposed thickness walls; the same trim extents participate in flat region placement.

The electronics tray and CTC-03 each produce four relief corners without flange overlap. Closed/overlap/welded seams, arbitrary corner intersection solving, and full shop-specific round/obround notch libraries remain deferred.
