# Experimental modules

These pages describe work that exists in the repository but is not part of the
supported product. It is published so you can see where Aetheris is heading and
reproduce the experiments, not so you can depend on it.

Experimental means:

- the syntax, C# APIs and file formats can change or disappear without notice;
- results are reported honestly, including failed gates, but no result here is a
  qualification claim;
- nothing in this folder is covered by the [support matrix](../reference/supported-features.md).

## Humanoid bodies

A research module for a canonical adult body: topology identity, a reference rig,
semantic poses, constrained kinematics and judgment-driven deformation. No adult
body is promoted to production. The Antonia body is a development base.

- [Research tooling and current status](humanoid/research.md) — start here
- [Reference rig](humanoid/reference-rig.md) and [pose normalization](humanoid/pose-normalization.md)
- [Authored poses and leg IK](humanoid/locomotion.md), [constrained kinematics](humanoid/kinematics.md)
- [Deformation judgment](humanoid/judgment.md), [source-pose comparison](humanoid/source-pose-comparison.md)
- [Antonia gameplay body](humanoid/gameplay-body.md)

## Garments and cloth

Panels, stitch interfaces and draping on a body. The cloth solver, fitting and
pattern import run end to end, but no garment passes the strain gates yet.

- [Garment authoring foundation](garments/garments.md) — start here
- [Starting from GarmentCode sewing patterns](garments/garmentcode.md)
- [Temporary fitting from Concept geometry](garments/temporary-fitting.md)
- Experiments: [quad surfaces](garments/surface-experiment.md),
  [body-fitted splines](garments/spline-experiment.md),
  [meshing and settling](garments/meshing-settling-experiment.md),
  [armholes](garments/armhole-experiment.md),
  [fit and resolution](garments/fit-resolution-experiment.md)
- [Draping research plan](garments/draping-research.md)

## Exterior reconstruction

- [iPhone exterior reconstruction](iphone-reconstruction.md): an editable
  blockout of a phone from published dimensions, as a test of reconstruction
  workflow rather than a finished model.
