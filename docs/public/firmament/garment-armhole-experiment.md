# Garment armhole experiment

Date: 2026-10-11. This follows [temporary Concept fitting](garment-temporary-fitting.md).
**The final A-pose drape improves from 87.12% to 9.23% peak strain. The complete
dressing sequence remains unqualified because fitting and midpoint checkpoints
still exceed the unchanged 15% strain limit.**

## Controlled inputs

All trials retain the imported GarmentCode cut, raised flat arrangement, 611
vertices, 992 triangles, six seams and zero permanent pins. The control uses
Rest-pose sewing and a collar guide at measured shoulder height plus 45mm,
compliance 1e-8, released after 20 settling frames. Each run has 30 gravity-free
sewing frames, 60 fitting frames, eight substeps and 24 structural iterations.
The transition adds 30 motion frames and 60 wearing frames.

Body SHA-256:
`0a0ad03e86447fc56448940c7aa56b54ea6683e8a874f833e01fd56a65a2d0e5`.
Cloth content key, including arrangement and fabric:
`42C204F0BE495216B6CB3C2C3087EC8C786135D3EE71FE2D465BCFB4D468BA8B`.
Warp/weft compliance remains 1e-8 and diagonal compliance 4e-8. These are
authoring values, not calibrated textile measurements. No pattern dimension,
clearance, acceptance gate or body skin weight was adjusted.

## Contact isolation

`scripts/inspect-garment-strain.cs` recompiles the actual source, checks its hash
against the retained `garment.json`, verifies FigureFit body artifact hashes
and validates snapshot topology/material identity. It reconstructs the authored
final body pose through the existing humanoid solver. It reports semantic edge
membership, material coordinates and body samples for the eight worst links.
It runs five replay frames through `ClothSolver3D` with zero initial velocity
and zero gravity, varying contact and substeps with 24 iterations.

The original final snapshot has 87.119% strain and 0.00872mm sampled penetration.
Its worst link is right-back armhole 336/337, rest length 14.275mm. Its endpoints
are 6.002mm and 7.792mm from the body. Interior triangle samples and connected
constraints also act on them; endpoint distance alone cannot justify disabling
contact.

| Contact | Substeps | Peak strain after five frames | Penetration against original body | Seam gap |
| --- | ---: | ---: | ---: | ---: |
| Enabled | 8 | 80.663% | 0.748mm | 1.968mm |
| Enabled | 32 | 85.598% | 0.766mm | 1.734mm |
| Disabled | 8 | 12.490% | 61.136mm | 0.548mm |
| Disabled | 32 | 0.925% | 95.169mm | 0.064mm |

The sewn material can relax when contact is removed, but then enters the body.
More substeps with contact enabled do not repair this control. This implicates
the fitted arrangement and contact/structural conflict; it does not establish
pattern sizing or the discrete contact algorithm as the sole root cause.
Contact-disabled snapshots are diagnostic failures, never accepted fits.

## Declarative fitting and motion

Changing only the fitting pose to TPose reduces final strain to 22.309%, seam
gap to 0.820mm and sampled penetration to 0.000128mm. The guide is released,
with 40 unsupported fitting frames. The worst remaining edge is right-back
armhole 331/332, rest length 10.770mm; the next reported outlier is 9.614%.

Then the same fitted garment follows the existing half-second SmoothStep
transition from TPose to APose, followed by 60 wearing frames:

| Checkpoint | Peak strain | Seam gap | Sampled penetration | Body surface admissible |
| --- | ---: | ---: | ---: | --- |
| Fitting | 22.309% | 0.820mm | 0.000128mm | Yes |
| Midpoint | 22.372% | 1.111mm | 0.000118mm | Yes |
| Wearing | 9.228% | 1.132mm | 0.0000247mm | Yes |

The final garment has no permanent pins or active guides. Final and midpoint
replay checks are exact. Maximum measured body travel per substep is 4.088mm;
this is a diagnostic, not CCD certification. The final wearing snapshot passes
the existing strain/seam/contact gates. **The CLI correctly reports the whole
sequence as failed** because its fitting and midpoint strain still exceeds 15%.
No checkpoint is exempted and no limit is weakened.
The final three wearing samples also satisfy the existing stationary/gate
predicate, with final maximum speed 0.000799m/s. This is bounded settling
evidence, not proof for arbitrary motion.

This isolates the remaining blocker to startup fitting near the right-back
armhole, rather than final A-pose equilibrium or collar retention. A further
startup-arrangement/contact investigation is required before qualifying the
complete sequence. Adding a strain cap that merely transfers the error into
penetration or seam gaps would not resolve it.

The actual front/side/back renders show less armhole bunching than the Rest-pose
control. They still show a coarse, somewhat stiff silhouette and a projecting
neckline; this is a foundation specimen, not a finished textile model. Blender
only presents exported solver meshes, with no fitting, subdivision or cloth sim.

## Reusable source and evidence

`fixtures/Canonical/Garment/garmentcode-dress-guided.firmament` composes existing
Concept measurements, an erased collar plane, named boundary guides, release,
and declarative poses. The original bind skeleton and cut are preserved. A
source regression checks this composition. CLI compilation with the actual
body also confirms the same cloth key, guide command and motion program.

The checked-in specimen SHA-256 is
`6c672fced4ad4a1c210466ed9c5bc8402068ccfbdc13ec812579343bd5f55d9a`.
Experimental source files have different comments; each retained artifact
records its own source hash. Outputs live in
`artifacts/local/garment/armhole-round/`: `strain-inspection.json`,
`wearing-inspection.json`, `tpose-60/`, `transition-60/`, and `specimen/`.
Renders are under `artifacts/local/garment/renders/armhole-round-wearing/wearing/`.

```powershell
dotnet run -c Release scripts/inspect-garment-strain.cs -- `
  artifacts/local/garment/fitting-round/anatolia-guided-high.firmament `
  artifacts/local/humanoid-production/antonia.gameplay-body.json `
  artifacts/local/garment/fitting-round/high-60/garment.json `
  artifacts/local/garment/armhole-round/strain-inspection.json

dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment drape `
  fixtures/Canonical/Garment/garmentcode-dress-guided.firmament `
  --body artifacts/local/humanoid-production/antonia.gameplay-body.json `
  --ticks 60 --substeps 8 --iterations 24 `
  --out-dir artifacts/local/garment/armhole-round/reproduced --json
```

Use Release for replay probes; Debug is substantially slower. The helper accepts
a retained final snapshot with a FigureFit binding, not an intermediate OBJ.
Guides/body contact remain CPU capabilities. This does not qualify GPU draping,
friction, garment self-contact or continuous collision safety. No new dependency
or third-party solver was adopted. GarmentCode remains MIT-credited to Maria
Korosteleva; see pinned licenses and [import provenance](garmentcode.md).

## Validation

The idle serial Release Aetheris solution build passed with zero errors and
five existing WebAssembly SQLite interop warnings. The complete solution test
lane passed 4,518 tests, including 2,109 Firmament tests and 1,195 kernel tests.
The new source regression composes the known cut, temporary guides and pose
motion without changing cloth identity or the original bind skeleton. The
generated STEP test output was restored after the lane.

The inspection helper ran against both the original Rest snapshot and the
authored final A-pose snapshot; its recomputed metrics match the retained drape.
An equivalent UTF-8 BOM source produced identical compiler identity and replay
results. A mismatched source and attempted input overwrite were rejected, with
no diagnostic report written. Logs and probe reports are retained under
`artifacts/local/garment/armhole-round/`.

The Release Aurelian consumer solution built with zero warnings/errors and its
complete serial test lane passed 1,218 tests, with the installed Naga selected
through `AURELIAN_NAGA`. Copeland logs are
`artifacts/aurelian-beacon3d/garment-armhole-{build,tests}.log`.
This proves consumer regression compatibility, not GPU body-contact support or
native game presentation of this garment. No Copeland source was changed in
this round.
