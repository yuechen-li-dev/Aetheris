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
- `sundress.firmament`: one garment, two panels, four seams, open armholes,
  Rect2-derived named edits and figure-bound Concept fitting. Supply `--body`
  through the garment commands; standalone validation reports the missing input.
  The current static fit closes seams and passes sampled contact, but its 18.0%
  peak neckline stretch exceeds the 15% limit. It is an authoring specimen,
  not an accepted fit; retained evidence and limits are in the garment guide.
- `tshirt-dress.firmament`: the same authoring workflow with integrated sleeves
  and eight seams. An arrangement/contact experiment, not an accepted fit.
- `sundress-posed.firmament`: exactly the sundress cut and Rest-pose measurements,
  with declarative T-pose fitting and a half-second transition to A-pose. This
  isolates pose/motion from pattern changes; inspect all retained checkpoints
  before treating it as an accepted fit.

`qualification.json` validates language/compilation. It does not certify a body
fit. The actual local humanoid is supplied to `garment drape --body ...`.

## GarmentCode reference starters

`GarmentCode/` contains pinned MIT-credited upstream pattern JSON, design inputs,
independent curve samples and provenance. `garmentcode-dress.firmament` is its
editable translation; `garmentcode-dress-template.firmament` adds pure hem
customization. These qualify source geometry/interchange, not production drape.
See [workflow documentation](../../../docs/public/firmament/garmentcode.md).

`garmentcode-dress-guided.firmament` preserves the imported cut and combines
an erased Concept collar guide with T-pose sewing and a half-second transition
to A-pose. The guide releases before the transition, with 40 unsupported fitting
frames in the 60-frame experiment. It requires the actual figure artifact.
See [armhole investigation](../../../docs/public/firmament/garment-armhole-experiment.md)
for contact probes, checkpoint metrics and the unresolved strain limit.

`temporary-fitting.firmament` is an analytic authoring/solver witness: an erased
Concept plane supports named panel boundaries for five settling frames. The
shared drape path then releases the guide and requires 15 unsupported frames.
It introduces no permanent pins or additional material panel. See
[temporary fitting](../../../docs/public/firmament/garment-temporary-fitting.md).
