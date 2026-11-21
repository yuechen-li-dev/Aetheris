# SectionChain to SubD handoff

The compact mouse fixture uses an unbranded exterior based on the supplied M170
reference views. Logitech lists the external envelope as 97.3 × 61.9 × 35.2 mm.
The authored profiles estimate the silhouette and parting station from the views.
The fixture covers the exterior shape and leaves wheel, button and internal
geometry for a separate pass.
[Logitech technical specifications](https://support.logi.com/hc/en-gb/articles/360023301394-M170-M171-B170-Technical-Specifications)

## Construction

- The normal SectionChain materializer builds the exact body and provides face
  correspondence to the cage converter. The converter samples exact supports
  and fits their corresponding Catmull–Clark limit samples. The rim is a sharp
  loop with a separate sampled error budget.
- The cage owns authoring handles. Station and transition regions expand to
  concrete cage vertex IDs in replayable operations. GLB and STL sample the
  limit surface independently of the authored cage density.
- The CLI selects the exact state or an edited sibling with `build --state`.
  `inspect --json` reports source identities, cage geometry and fit evidence.

## Qualification

Run the checkout's Release CLI from the repository root.

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/Sculpting/compact-mouse.firmament --state Exact --out artifacts/local/subd-mouse/Exact.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll analyze artifacts/local/subd-mouse/Exact.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/Sculpting/compact-mouse.firmament --state PalmComfort --out artifacts/local/subd-mouse/PalmComfort.glb --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/Sculpting/compact-mouse.firmament --state LowFront --out artifacts/local/subd-mouse/LowFront.glb --json
```

- STEP reimport qualifies one enclosed shell with 22 faces and 88 pcurves.
- Across 2,592 corresponding samples, fitting reduced the worst transition
  deviation from 1.028 mm to 0.448 mm. The maximum difference at the 256 rim
  samples was 0.1235 mm in both branches, under the authored 0.15 mm budget.
- Each GLB contains 45,056 triangles. CLI qualification checks watertightness,
  replays both sidecars, verifies distinct upper geometry and compares every
  sampled position at and below the parting plane exactly.
- The raised-palm GLB moves the evaluated surface by up to 1.212 mm compared
  with the seed. The lowered-nose GLB moves it down by up to 0.400 mm. GLB
  positions at and below the parting plane match the seed in both branches.
- The invalid rim budget and removal of its crease fail before file creation.
- Matched oblique and side previews show the two edits and a small residual
  crown plateau from the planar cap.

## Result

The Release solution build and all 23 focused sculpting CLI tests passed. The
full parallel suite reported seven display-related failures in Kernel Core and
CLI. All seven passed in a 20-case targeted rerun with one test process per
project. The same 20 cases also passed on the unchanged `397d7bee` baseline.
The full parallel gate remains red on this host.

The transition and rim figures are sampled positional evidence. General
retopology, global mesh self-intersection proof, and STEP export of edited SubD
remain outside this lane.
