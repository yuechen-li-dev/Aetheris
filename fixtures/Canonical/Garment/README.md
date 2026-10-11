# Garment specimens

These are authoring sources for `schema Garment`, not finished textile patterns.
See `docs/public/firmament/garments.md` for commands, ownership and bounds.

- `skirt.firmament`: reusable Rect2/Profile Feature, typed Set/Pattern, two seam
  interfaces, explicit waistband support. The first lower-body coverage starter.
- `flared-skirt.firmament`: the same composition with a tapered material cut and
  interpolated waist/hem rings, for a closer waist and a wider free hem.
- `tunic.firmament`: named line spans, arm/neck openings, side/shoulder stitches,
  no supports; the garment rests on the loaded humanoid.
- `shorts.firmament`: mirrored Profile, four independently arranged panels,
  interpolated waist/leg rings and eight stitch interfaces. A tighter fit
  experiment; consult retained drape evidence before using it as a baseline.

`qualification.json` validates language/compilation. It does not certify a body
fit. The actual local humanoid is supplied to `garment drape --body ...`.
