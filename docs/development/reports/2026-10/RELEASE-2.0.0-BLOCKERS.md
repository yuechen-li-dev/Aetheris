# Aetheris 2.0.0 release-blocker pass

2026-10-23 · master @ `25795d16` plus the version-props fix in this commit.
Scope: Aetheris only (HeliosCAD is released separately).

## Verdict

The language core is in good shape for a stable tag. Across 373 canonical files,
5,000 random mutants and 2,424 unknown-field probes, we saw zero crashes, zero
hangs and zero nondeterministic STEP output. Packaging is what blocks 2.0.0: the
library set as it stands cannot be installed from nuget.org (B1), and the release
lane can only ship preview.3 (B4). Both are a few hours of work once the decisions
below are made.

The second-largest item is a product-promise gap. The new public docs say an
unknown or misplaced field "always rejects", but about 400 of 2,424 probes are
still accepted, mostly in assembly, Semantic and SectionChain bodies. Either
close those gaps or soften the sentence before 2.0.0 (S1).

## Already fixed in this pass

| Commit | Fix |
|---|---|
| `5e4c90b3` | Parse cache: guitar warm build 11.2 s → 5.8 s |
| `25795d16` | Reject non-finite numbers (`1e309mm`, `NaN`, `Infinity`) at parse time with `firmament-number-not-finite` |
| `25795d16` | Field closure for `Pmi` blocks, `Profile` bodies (`From` still legal), `Modify` on a composed body, `Hole<Shaft>` in materialized Templates, and concept-Path `Line`/`Span`/`Arc` segments (which also covers circular-sweep paths) |
| `25795d16` | CLI tool package: native PDBs stripped, 276 MB → 210 MB (nuget.org limit is 250 MB) |
| this commit | `InformationalVersion` and library release notes follow `$(Version)`. A `-p:Version=2.0.0` pack used to report `aetheris 2.0.0-preview.4` |

Regression tests: `Aetheris.CLI.Tests/SourceIntentClosureRegressionTests.cs` has
8 cases. Two assembly tests were updated because they now see the earlier,
better diagnostic. The canonical corpus is unchanged: same exit codes and
byte-identical STEP hashes for all 373 files. The unknown-field probe went from
603 to 401 accepted.

## Blockers: must resolve before tagging 2.0.0

### B1 · The public library graph depends on unpublished packages
This is verified by an actual restore, with nuget.org enabled, of a consumer of
`Aetheris.Kernel.Firmament 2.0.0`:

```
error NU1101: Unable to find package Aetheris.Cloth3D
error NU1101: Unable to find package Aetheris.Humanoid
error NU1101: Unable to find package Aetheris.Fields
```

| Package (published) | Depends on (not in `package-public-libraries.ps1`) |
|---|---|
| Aetheris.Kernel.Firmament | Cloth3D, Humanoid |
| Aetheris.Continuum (and so FEA, Forge.Host) | Fields |
| Aetheris.Modules.BuiltIn | Humanoid, PlasticShell |

Preview.3 on nuget.org did not have this problem; its Firmament nuspec lists
only Collaboration, Continuum, Forge, Kernel.Core, StandardLibrary, Semantics
and Surfacing. The Cloth3D/Humanoid edge arrived with garment authoring
(`1cd817f0`). **Decision D1.**

### B2 · CLI package size
It is 210 MB after the PDB strip, which is legal but 6.3× preview.3 (33 MB).
Installed, it takes 874 MB. The native assets cover 36 RIDs:
libSkiaSharp 82 MB, pdfium 53 MB, rhino3dm 30 MB, e_sqlite3 27 MB. One more
native dependency would push it over nuget.org's limit again. **Decision D2.**

### B3 · Stable package depends on a prerelease
`Aetheris.Kernel.Firmament 2.0.0` depends on
`Copeland.TS.Backend.Wgsl 0.1.0-preview.1-a3.16c0ce99…` (NU5104). NuGet allows
it, but a stable 2.0.0 that pulls a prerelease pins every consumer to it.
Publish a Copeland 0.1.0, or accept the dependency and say so in the release
notes. **Decision D3.**

### B4 · No 2.0.0 release lane
- `.github/workflows/dotnet.yml` has a "frozen" release lane that hardcodes
  `2.0.0-preview.3` in package paths, the tool-install smoke test, the tag
  filter `refs/tags/v2.0.0-preview.3` and `--notes-file docs/release/2.0.0-preview.3-notes.md`.
- `scripts/package-public-libraries.ps1` defaults to `2.0.0-preview.3`, while
  `Directory.Build.props` says `2.0.0-preview.4`.
- There are no 2.0.0 notes, and the CLI `PackageReleaseNotes` still reads
  "Preview 3: … Windows x64 is the qualified release target".
- The Web runtime SDK (`Aetheris.Web.Runtime/sdk/package.json`, `Program.cs:102`)
  and `demos/Aetherion.Helios` are pinned to preview.3.

Work: parameterize the lane on the tag, write the 2.0.0 notes, and bump the
props and SDK version. I can do all of this once D1–D4 are settled.

## Should fix before 2.0.0

### S1 · Silent acceptance that remains (docs promise otherwise)
The probe inserts `zzUnknownField: 3mm;` after every `{` in 246 seeds, giving
2,424 probes. 401 are still accepted. They cluster by binder:

| Area | Accepted | Binder |
|---|---|---|
| Concept Struct members | 59 | Mostly legitimate member publications, so probably not bugs |
| `Semantic` bodies (assembly parts) | 48 | Assembly part parser |
| WireForm: `Straight`, `StartFrame`, `Bend`, `Knot`, `Helix`, `WireForm`, Model | ~110 | `WireForm.cs`, regex-based with no field closure |
| `Assembly` / `Subassembly` roots, `expose` | 40 | Assembly parser |
| SectionChain `Path`, `Loop`, `Point2`, `Line2` | ~45 | `SectionChainAuthoringParser` |
| Object values in Structs (`Bounds`, `A:`/`B:` Points, `Plane{}` …) | ~40 | Value-object readers |
| Template-struct `Extrude`, `Profile`, `EdgeFinish`, `RemoveOffset`, `return`, `Axis2` | ~15 | Assorted |

Repros that are user-visible today, all in Learn/06, the assembly lesson:

```
Extrude body { profile: outline; from: 0mm; to: height; zz: 1mm }    → builds
Semantic Foot { zz: 1mm; DatumFrame Frame = … }                       → builds
anchor: PostOnPlate.Plate; zz: 1mm;                                   → builds
SectionChain  Point2 B { position: [2mm, -2deg] }                     → builds (wrong unit!)
SectionChain  Point2 B { position: [2mm, -2mm]; zz: 1mm }             → builds
```

The SectionChain unit case is the worst one, because a wrong unit changes
geometry silently. A cheap minimum for 2.0.0 is to close Semantic, Assembly
roots and SectionChain `Point2`/`Line2`/`Path` with the same syntax-based check
used today, and add a "known gaps" paragraph to
`reference/language/compatibility.md` for WireForm and the value objects.
Closing WireForm properly means moving `WireForm.cs` onto the syntax tree, which
belongs in 2.0.x.

### S2 · Validate and build route differently
`validate` dispatches by domain (Drawing, Analysis, Garment, Assembly, Scene,
PlasticShell, Sculpting, Plateau, Piping, Structural, SectionChain, WireForm,
then generic V2). `build` has its own shorter list, then
`FirmamentBuildAndExport`, which falls back to the legacy expansion pipeline
whenever V2 does not own unified admission. One consequence:

```
model RoundPlate { … }      (EquationCurves/circle.firmament, lowercase `model`)
validate → exit 1  (firmament-v2-phase3-edge-finish-syntax-invalid)
build    → exit 0, writes STEP, reports model "Outline"
```

I tried the obvious guard, "fail build when V2 says RecognizedInvalid". It broke
29 corpus files (all WireForm, sweep, knot and open-path sources), because those
unmigrated domains are always RecognizedInvalid in V2 and are owned by the legacy
pipeline. So I reverted it. The real fix is one shared domain router used by
both commands. Among the 5,000 mutants this was the only kind of validate/build
disagreement (2 cases, both lowercase `model`). Suggested: fix it if time allows; otherwise treat it as a known
issue.

### S3 · Misleading diagnostics on common mistakes
| Input | Diagnostic shown |
|---|---|
| Lowercase `model`, or any WireForm field error | `firmament-v2-phase3-edge-finish-syntax-invalid` |
| `999999999999999999999mm` box size | `ValidationFailed: Unable to choose a containment sample point.` |
| Plain-Model Extrude `to: 8deg` | `ProfileExtrusionPlanInvalid: invalid height` |
| `Point2` with `deg` in a mechanical profile | `profile-layout-unresolved-line` |
| Unknown field in a mechanical `Path` `Line` | `firmament-pipeline-unknown-guide:Stadium.Bottom` |
| `build`/`measure`/`verify`/`format`/`lock`/`mesh` on a missing file | Raw .NET text: `Could not find file '/abs/path'` |

None of these is unsafe, since each one rejects. But the first row is what a
WireForm user sees for every typo.

### S4 · Process-cold validate on swept and B-spline models
Each figure is a single cold process:

| Fixture | validate | build |
|---|---|---|
| EditableV8/scene | 29.8 s | n/a |
| WireForm/axis-coil | 15.1 s | 15.6 s |
| WireForm/helix | 14.8 s | 14.1 s |
| Assembly/coil-on-stem | 11.8 s | 13.4 s |

Stack samples put the time in validate's in-memory STEP preflight. The importer
re-reads the generated STEP, and `StepFaceOrientationResolver` (line 160) calls
`BrepDisplayTessellator.Tessellate` with default options just to get a
signed-volume sign. `BrepPcurveRecovery.Populate` then evaluates the B-spline
grid. Options are coarse sign-only tessellation, or skipping orientation
re-resolution for STEP the kernel itself just wrote. Both touch the kernel, so I
left them alone. The guitar cold build is also still 17.0 s against 10.9 s on
the Oct 13 baseline; the gap is in step.import-body, sectionchain authoring and
m0.ports.

## Defer to 2.0.x (track, do not block)

- WireForm binder moved to the syntax tree with full field closure (finishes S1).
- Object-value field closure (`Bounds`, Points, `Plane{}` and similar inside Structs).
- A model-size bound with a real diagnostic (see S3, row 2).
- Shared validate/build router (S2), if not done for 2.0.0.
- RID-split CLI packages (D2 option b).
- CLI help is uneven: 9 commands (`format`, `mesh`, `lock`, `measure`, `inspect-3dm`, `recover-3dm`, `fea`, `drawing`, `scene`) print a one-line usage only, and `view --help`
  says "Open a STEP model" while top-level help says it builds and opens Firmament.
- Assembly builds write to `artifacts/local/build/<name>.step`, while part builds
  write next to the source. Pick one convention and document it.
- Cookbook entries that only `asm inspect` (Assembly/bearing-module, which is
  the README's "author an assembly" example, plus machine and two-link-arm) or
  are inspect-only (7 GearInterfaces pairs) fail plain `validate`. Either label
  them in the docs or point the README at Learn/06.
- Slow validate pathology on mutants (up to 18.8 s) traces to the same S4 preflight.

## Decisions needed from you

- **D1 · Experimental packages in the graph.** Options:
  - (a) Publish Cloth3D, Humanoid, Fields and PlasticShell as `-preview`
    packages. This makes B3 worse, because a stable package would then depend on
    prereleases.
  - (b) Publish them as 2.0.0 with "experimental" in the description.
  - (c) Cut the edges: move garment authoring behind a module so
    Kernel.Firmament does not reference Cloth3D or Humanoid, have Continuum stop
    referencing Fields, and drop Humanoid and PlasticShell from Modules.BuiltIn.

  I'd go with (c) for Humanoid and Cloth3D, since you've said they are "part of
  the product LATER". Fields and PlasticShell depend on how public you want
  them.
- **D2 · Native footprint.** Options:
  - (a) Keep all 36 RIDs.
  - (b) Ship RID-specific tool packages.
  - (c) Restrict natives to the qualified platforms, for example win-x64,
    linux-x64 and osx-arm64. Estimated from per-RID asset sizes, that comes to roughly 55 MB.

  Also decide whether pdfium, SkiaSharp and rhino3dm belong in the core tool at
  all, or in an optional add-on.
- **D3 · Copeland prerelease dependency** (B3).
- **D4 · Release framing.** Is this "2.0.0" stable, or should it be another
  preview with 2.0.0 for YC? Also, which platforms do the notes call
  "qualified"? The current text says Windows x64 only, but the tool installs
  and builds all six Learn lessons on Linux x64.
- **D5 · Bare numbers without units** (Piping, Structural, WireForm,
  SectionChain, Set read them as mm). Keep as legacy mm, warn, or reject. Note
  that WireForm `Length: 20` already rejects.
- **D6 · Lowercase keywords** (`hole`, `base`, `profile`, `loop` are accepted).
  Make them official aliases (`format` already rewrites to mixed case) or reject
  them. Either way, lowercase `model` should not behave differently from the
  others.
- **D7 · Docs promise.** Close the S1 gaps before 2.0.0, or caveat "always
  rejects" with a known-gaps list.

## What was checked, and how to repeat it

- **Full test lane:** `dotnet test Aetheris.slnx -c Release --no-build -m:1`.
  All green after the fixes.
- **Canonical sweep:** 373 files under `fixtures/Canonical`, validate plus a
  double build. We compared exit codes and STEP hashes before and after, plus
  build-twice determinism.
- **Mutation fuzz:** 5,000 cases with 14 mutators (delete-char, dup-line,
  lowercase-keyword, unit swap, number extremes, field rename and others).
  Zero crashes, hangs or stack traces in output.
- **Unknown-field probe:** 2,424 insertions over 246 seeds. Results by construct
  are in S1.
- **Packaging:**
  - Packed all 16 libraries and the CLI at `-p:Version=2.0.0`.
  - `dotnet tool install` from the local feed succeeded.
  - `--help` exits 0 for all 25 commands; unknown commands and no arguments exit 1.
  - All six Learn lessons validate and build from an unrelated directory, and
    their `--json` output parses.
  - Missing files go to stderr with exit 1 and empty stdout.
  - A consumer restore of Kernel.Firmament fails as described in B1.

The fuzz harness itself was a throwaway outside the repo, built on
`CliRunner.Run` in-process. If you want it kept, it would go in
`tools/fuzz/`; that is a small follow-up.
