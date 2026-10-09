# Authored humanoid poses and leg IK

`HumanoidKinematicSolver.SolveAuthored` admits explicit rest-relative quaternion
channels and a canonical millimetre visual root offset. The caller names the
skeleton and rest revision. The API checks finite unit quaternions, known unique
joints, a root offset of at most 250 mm, named engineering rotation envelopes,
measured hinge flexion and the existing rigid-link residual checks. It issues
the same `SolvedHumanoidPose` consumed by skinning; there is no bypass token or
alternate skeleton authority.

These coordinates differ from `AnatomicalJointRequest`, whose flexion/abduction/
twist coordinates are absolute anatomical requests. Imported local quaternions
must be converted against the actual rest skeleton. Engineering envelopes are
not medical limits and do not certify an arbitrary mesh deformation. Call
`HumanoidGameplayBody.Evaluate` and inspect its surface evidence independently.

`SolveLeg` accepts a matching solved pose, anatomical side, canonical ankle
target and ground normal. It solves hip-knee-ankle analytically using the source
knee pole, keeps both rigid link lengths, and transports the sole normal onto
the ground while preserving animated heading. Unreachable targets clamp to the
rigid chain and report `ReachClamped` plus ankle residual in millimetres. The
result is re-admitted through `SolveAuthored`; admission failure is explicit.

The application owns collision queries, stance detection, world-space locks,
pelvis adjustment, fading and release policy. Aetheris owns pose admission and
the solve. The small joint calculation does not deform the surface; Aurelian's
existing GPU skinning consumes the resulting palette.

Tests cover authored admission failures, both reachable legs, rigid-link
residuals, palette compatibility and an unreachable target without stretching.
The Copeland Mixamo bank exercises this API with a separately prepared Antonia
body. Its 1,737-pose qualification now passes the existing surface screen on CPU
and Vulkan ground queries. Kinematic test success alone does not confer animation
or production-mesh qualification.

`scripts/repair-antonia-locomotion-weights.py` writes an explicit body variant,
leaving its source intact. It changes only normalized skin support: a continuous
neck-to-skull transition from 60 mm below the neck pivot over 100 mm, and a knee
transition half-width of 200 mm with the existing radial fade. Geometry, topology,
rest skeleton and authored hip correctives are retained and checked. Source hash
and recipe are recorded in provenance; the result remains a development candidate
pending its own pose qualification. The original body is not promoted or replaced.

Run `scripts/test-antonia-locomotion-weights.py` with Python for regressions on
region-boundary continuity, bilateral support, retained geometry and distant
support. New clips still require independent surface qualification; the successful
bank does not certify arbitrary poses, fingers, facial animation or intersections.
