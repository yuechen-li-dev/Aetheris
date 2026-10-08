# HUMANOID-GAMEPLAY-X1 — Antonia development body

2026-10-08. Success for a bounded editable gameplay body and typed runtime
deformation path. This is not canonical adult promotion or an unrestricted
production-character certification.

## Delivered

- Retained admitted original Antonia geometry, original quad topology and UVs.
- Bilateral weights, central pelvis support, smooth hinge transitions and
  shoulder support; no four-influence truncation.
- Shared retained dual-quaternion skin binding, source-generated gameplay JSON,
  two sparse authored hip flexion correctives, explicit anatomical activation.
- Editable Blender rig, subdivision preview, own simple materials and 55-pose
  timeline library. Fresh-process reopening works with script execution disabled.
- Reproducible recipe and fail-closed qualification in
  `scripts/qualify-antonia-gameplay.ps1`; local before/after/reference review.
- Fixed negative-flexion boundary measurement selecting an incorrect Euler
  branch due to float roundoff. Joint-domain constraints are unchanged.

## Evidence

The original LBS baseline was replayed in Blender. The final body was serialized,
reloaded and evaluated in C#, then independently replayed from solved local
quaternions in Blender. Both use the existing edge-distortion and reversal-proxy
screen; its thresholds were not weakened.

| Pose | Baseline maximum edge ratio / flags | Final maximum edge ratio / flags |
| --- | ---: | ---: |
| Hip 90 | 9.059 / 71 | 3.408 / 0 |
| Knee 90 | 5.624 / 28 | 2.133 / 0 |
| Elbow 120 | 7.018 / 22 | 2.334 / 0 |
| Seated | 31.079 / 288 | 3.435 / 0 |
| Squat | 32.422 / 221 | 3.014 / 0 |

All 53 intended cases pass the runtime screen with no collapsed triangles or
reversal flags. Maximum C#/Blender vertex difference is 0.006363 mm; maximum
semantic residual is 0.000053 degrees. Eight saved-file poses reopen within
0.000069 mm. Topology: 27,193 vertices, 27,112 editable quads, 54,224 binding
triangles, 108,448 UV loops, 55 joints.

Local evidence is under `artifacts/local/humanoid-production/`; raw trials,
reference renders and generated character files remain ignored. The Blender
file contains no Genesis payload. The local Genesis comparison uses fixed rest
scale; its hip90 pose is actually 87.519 degrees and is not numeric pose parity.

Validation: full Release build (zero errors, two existing WASM warnings), 86
focused humanoid tests, one source-generated gameplay roundtrip with JSON
reflection disabled, and 4,408 serial solution tests across 21 assemblies
(zero failures/skips). Actual Blender replay and fresh-process saved-file
reopening also pass. The full solution command was:

```powershell
dotnet test Aetheris.slnx -c Release --no-build -m:1 --logger trx --results-directory artifacts/local/humanoid-production/full-tests-final -v quiet -- RunConfiguration.MaxCpuCount=1
```

The complete asset wrapper exits zero with `ANTONIA_GAMEPLAY_QUALIFIED`.
See the generated TRX and logs for exact command results.

## Remaining limits

Left/right hip100 stress poses retain five/six reversal flags. Screening does
not prove absence of self-intersections, qualify every interpolated animation,
or admit arbitrary twist combinations. No automatic canonical measurement,
morph or attachment admission is added for Antonia.

The CPU skinning reference and GPU-ready palette are implemented; Aurelian
Vulkan dispatch is not. Ordinary glTF skinning cannot silently replace the DQS
contract. Hair, clothing, facial animation, LODs and production texture maps are
outside this body's qualification. Genesis geometry, weights and morph deltas
were not transferred; no tools or assets were downloaded for this work.

Usage and reproduction: [Antonia gameplay body](../public/humanoid-gameplay-body.md).
