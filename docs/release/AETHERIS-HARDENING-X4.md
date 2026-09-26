# AETHERIS-HARDENING-X4 — CI and regression closure

## Scope and baseline

Started from `ac627fb368643ad1375cf5e4d4f06ecb2f54e5c0` on 2026-09-25 with a clean working tree. The normal push workflow in `.github/workflows/dotnet.yml` originally ran the repository layout guard, Server and Kernel.Core Release builds, then only the Kernel.Core `Category!=SlowCorpus` lane. The tag release job ran the full solution test command. This pass adds the full solution build and test to normal push and pull-request CI while preserving the fast lane.

First reproduction of each lane, before its relevant fix, on Windows / .NET SDK 10.0.401:

| Check | Baseline result | Root cause |
|---|---:|---|
| Repository layout | Failed | Tracked helix fixture under retired `fixtures/Firmament/`; architecture note under unapproved `docs/architecture/`. GitHub run [36199361128](https://github.com/yuechen-li-dev/Aetheris/actions/runs/36199361128) stopped at the first of these. |
| Server Release build | Passed | No failure. |
| Kernel.Core.Tests Release build | Passed | No failure. |
| Kernel.Core fast lane | 1,002 passed; 20.29 s command wall time | No failure. |
| Solution Release build | Passed, 7 existing Web Runtime warnings; 21.31 s | No build error. |
| Solution full test lane | 3,980 passed, 1 failed; 227.03 s command wall time | CLI hex-bolt OBJ assertion expected exactly 905 quads; the current output has 908 quads and 1 triangle. |
| Opt-in FrictionLab | 394 passed, 5 failed of 399 | All five tests used one parity lab. A NaN profile coordinate reached topology planning and threw while constructing a finite `ParameterInterval`. |
| Opt-in Firmament | 2,306 passed, 1 failed of 2,307 | A legacy placement test expected a Y coordinate of -1.25 although the current symmetric base box has vertex and edge centroid Y = 0. |

An initial post-fix full solution run exposed one additional intermittent `PlanarPlateauTests.FreshAuthoredPhoneUsesGenericPlateauAndMeshesEveryFace` failure. Under concurrent Firmament tests, the 35-face assembly's bounded display export exceeded its production five-second budget at 5,063 ms. Five isolated repetitions passed, each in about one second of test duration (2.61–2.82 s command wall time). This is test-runner contention against a real wall-clock budget, not a changed geometry assertion. The case now has its own non-parallel xUnit collection so the real export path and production budget remain unchanged. Final full-lane runs below verify the isolation.

The default full solution lane intentionally discovers no `Aetheris.FrictionLab.Tests` cases: its project excludes legacy source unless `AETHERIS_RUN_LEGACY_TESTS=1`. The opt-in runs above make that separate coverage visible. Raw logs and TRX files are ignored under `artifacts/local/hardening-x4/` and project `TestResults/`.

## Changes and reasons

- Moved the two tracked files into approved `fixtures/Speculative/WireForm/` and `docs/development/architecture/` locations. Neither old path had code references. The repository layout guard then passed for 4,614 tracked files.
- Replaced incidental hex-bolt quad and triangle counts with assertions on all 21 imported face patches, polygon accounting, OBJ face count, over 90% quads, watertightness, connectedness, outward orientation, and zero cracks, non-manifold edges, and zero-area triangles. The CLI's real OBJ export reports these properties; changing `905` to `908` would not protect the intended behavior.
- Rejected non-finite profile line coordinates and invalid arc/circle/ellipse parameters in `ProfileExtrusionBRepPlanner.Validate` before topology construction. A default Firmament regression test now checks the NaN input returns a diagnostic rather than throwing. The five opt-in FrictionLab failures share this cause.
- Made the legacy selector placement test compute the source body's vertex and edge centroids, then check the placed bodies against those anchors and the authored offset. The previous -1.25 Y value was an obsolete coordinate, not a selector invariant.
- Reused `Step242Corpus.Body` for rational-surface export assertions and `Step242Corpus.Import` for three ordinary recovery diagnostic tests. This removes eleven redundant corpus imports without caching import-time diagnostic-capture or `PreserveRationalSurfaces` scopes. No tests were moved into `SlowCorpus` and no timeout was raised.
- Isolated only the 35-face phone display integration test from concurrent test collections. The other plateau tests remain parallel. No tessellation timeout or product behavior changed.

## CI and local commands

Normal push/PR CI now runs, in order:

```text
./scripts/Test-RepositoryLayout.ps1
dotnet restore Aetheris.Server/Aetheris.Server.csproj
dotnet restore Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj
dotnet build Aetheris.Server/Aetheris.Server.csproj --configuration Release --no-restore
dotnet build Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --configuration Release --no-restore
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --configuration Release --no-build --filter "Category!=SlowCorpus"
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx --configuration Release --no-restore -m:1
dotnet test Aetheris.slnx --configuration Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

Opt-in legacy qualification:

```text
AETHERIS_RUN_LEGACY_TESTS=1 dotnet test Aetheris.FrictionLab.Tests -c Release -m:1
AETHERIS_RUN_LEGACY_TESTS=1 dotnet test Aetheris.Kernel.Firmament.Tests -c Release -m:1
```

## Final local receipts

After restoring all test-generated STEP output to a clean fixture tree, the normal Release solution build passed with zero errors. The two remaining warnings are pre-existing WebAssembly SQLite varargs warnings. The layout guard passed; the moved WireForm fixture also passed `aetheris validate` with zero diagnostics.

| Check | Before | After |
|---|---:|---:|
| Kernel.Core fast lane | 1,002 passed; 20.29 s | 1,002 passed; 15.00 s (earlier final run: 17.35 s) |
| Kernel.Core within full solution | 1,135 passed; 32 s | 1,135 passed; 30 s |
| Firmament within full solution | 1,687 passed; 65 s | 1,688 passed; 61 s (one new regression test) |
| Full solution, 20 discovering projects | 3,980 passed, 1 failed; 227.03 s | 3,982 passed, 0 failed, 0 skipped; 212.89 s |
| CI-relevant test commands combined | 247.32 s | 227.89 s |
| Opt-in FrictionLab | 394 passed, 5 failed | 399 passed, 0 failed |
| Opt-in Firmament (active plus legacy) | 2,306 passed, 1 failed | 2,307 passed, 0 failed |

The combined CI test time is the sum of the fast-lane command and the full-solution command on this Windows host; it excludes restore/build. The two timings are separate runs and should not be mistaken for a single xUnit duration. The full-solution counts include CLI 448, Kernel.Core 1,135, Firmament 1,688, Server 59, and SheetMetal 99, all with zero failures. `Aetheris.FrictionLab.Tests` is the one non-discovering default project; its 399 cases passed in the explicit opt-in run.

The slowest remaining Core classes by **sum of overlapping xUnit test durations** were loop-role normalization (30.3 test-seconds, 25 cases), helical ribs (20.6, 13), NIST display (19.9, 18), STEP export (18.1, 21), and periodic boundary orientation (17.4, 9). The loop-role class must import within diagnostic-capture scopes; helical-rib and display tests materialize distinct geometry. The slowest Firmament classes were perforation (49.6, 12), Bézier section stacks (47.4, 10), thread (42.0, 15), knot paths (32.5, 13), and WireForm (32.1, 30). These test-seconds overlap under xUnit parallelism and are not wall-clock costs. The three-case winding-axis mate class rebuilds intentionally different valid-pitch, oversize-fit, and wrong-axis variants, so caching its compilations would lose distinct assertions.

No test was retagged, skipped, or weakened in the recent perforation, hollow cut/blend, helical rib, thread, engraving, Bézier, multi-region, NIST, pcurve, or STEP orientation areas. No timeout was increased. The optional local `AETHERIS_STEP_NURBS_WITNESS` file was not supplied; its test remains conditional by repository policy, while the checked-in NIST and STEP tests ran in full.

## Failure reproducibility and CI verdict

The layout failure reproduced locally and on GitHub's `ac627fb3` push run. The hex-bolt CLI returned 908 quads, 1 triangle, watertight topology, and zero cracks in three repeated real `mesh` commands; its original exact-count assertion failed in the initial full solution run. The five FrictionLab failures all shared the same NaN-input exception and passed after planner validation. The legacy placement assertion failed in the opt-in full run and passed with its centroid invariant. The display test passed in the baseline full run, failed once under concurrent Firmament load at 5,063 ms, then passed five isolated repetitions and both full Firmament and full solution runs after test isolation. No known failing assertion remains locally.

The GitHub verdict is determined by the Actions run on the change commit; local Windows results alone do not establish Ubuntu CI success.

## PR #815 Ubuntu CI follow-up

The first PR run (`36209127179`, Ubuntu 24.04, .NET 10.0.401) reached the full solution test command. CLI reported 444 passed and four failed; Kernel.Core (1,134), Firmament (1,688), Server (59), and SheetMetal (99) passed. Three CLI failures were optional local Cartesian 3DM qualifications: `SkipException.ForSkip` was reported as a failure by the xUnit 2.8.2 VSTest adapter when the untracked fixture was absent. A fixture-aware `FactAttribute` now marks those tests skipped at discovery, while retaining their assertions when the fixture is present. The fourth failure was the public documentation link test treating a release report's intentionally ignored `artifacts/local/` evidence as a required checked-in file. The assertion now still requires ordinary relative links to exist, but permits absent files only under that designated local artifact directory. No product geometry or importer behavior changed.

On the follow-up worktree, `dotnet build Aetheris.slnx -c Release -m:1 --nologo -v:q` passed with zero errors, and `dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1` passed with 3,978 passed, 3 intentional local-fixture skips, and zero failures across the 20 discovering projects. CLI had 445 passed and 3 skipped; Kernel.Core 1,134, Firmament 1,688, Server 59, and SheetMetal 99 all passed. The GitHub Actions rerun remains the authoritative Ubuntu verdict.
