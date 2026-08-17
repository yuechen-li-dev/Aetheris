# PREVIEW3-HARDEN-A5

## Publication verdict

Yes. Aetheris `2.0.0-preview.3` is ready for the project owner to commit, tag as
`v2.0.0-preview.3`, and publish using the exact staged bytes recorded below. No tag,
push, package publication, or remote release was performed by A5.

The final bounded smoke passed from a clean extraction of the Windows ZIP in a path
containing spaces. ReleaseBlocker and MustFix count: zero. The remaining boundaries
are the documented Preview 3 limitations.

## Final artifacts

The canonical generated files are
`artifacts/release/a5-final/SHA256SUMS.txt` and
`artifacts/release/a5-final/RELEASE-INVENTORY.md`. All 19 checksum entries were
recomputed from disk and verified after final staging.

| Staged artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `Aetheris-2.0.0-preview.3-win-x64.zip` | 107,779,368 | `2e6a8efabd83363a6e5af4be12f2d113cc626f2cd6d90902abb99bcc4fdb89b8` |
| `aetheris-firmament-0.3.0-preview.3.vsix` | 11,937 | `def44bc6a83aed90a67505987ec36d00aa134d900fab734c48b83d0aeb59f361` |
| `packages/Aetheris.CLI.2.0.0-preview.3.nupkg` | 33,411,117 | `abe221bdb24e31b1cedd32b5b08fb3d220754f6254abd13a193758d3b5ebb07e` |
| `packages/public-libraries/Aetheris.Collaboration.2.0.0-preview.3.nupkg` | 17,633 | `8af8cdf29798d4ccbf83cae928b67bbf6c5de4ced97f55aa69f8f6203265fe6b` |
| `packages/public-libraries/Aetheris.Continuum.2.0.0-preview.3.nupkg` | 254,726 | `97448b0a88880c0271832375ee78ebc331bd497b90d0da24a2a6e7eae2a3d0d4` |
| `packages/public-libraries/Aetheris.FEA.2.0.0-preview.3.nupkg` | 110,851 | `e31484dccc5af838ff573e4b1809f6b1fc20e8a53a1e9cfb42d0b5e067324139` |
| `packages/public-libraries/Aetheris.Forge.2.0.0-preview.3.nupkg` | 60,458 | `964cb6d4543240a9fdd0ef34c4a747ec3666142644be1fefd789f49483861e66` |
| `packages/public-libraries/Aetheris.Forge.Host.2.0.0-preview.3.nupkg` | 78,114 | `95b91c3512744cf8762b33a7ef818f7dd9ca73df454e84d314411ee6696616cf` |
| `packages/public-libraries/Aetheris.Forge.KernelSDK.2.0.0-preview.3.nupkg` | 4,859 | `acc1eec08bd0097ffe0508d87cb1027488986c3453e02013ed8b7c4e338ca6b7` |
| `packages/public-libraries/Aetheris.Geometry.2.0.0-preview.3.nupkg` | 87,063 | `58a3bcb155a1864694ba7612520e08910612d5d7f790a75b1622b5bd7fd1bb7f` |
| `packages/public-libraries/Aetheris.Kernel.Core.2.0.0-preview.3.nupkg` | 866,015 | `110dbc667abb005270dfd486fd1f5e09ba551d374ddad7b0d7659c35a438e9ca` |
| `packages/public-libraries/Aetheris.Kernel.Firmament.2.0.0-preview.3.nupkg` | 1,493,720 | `ef7c5366f21b07e60a1b3482beada2237c1bb6f89846852600b3512cd57674d1` |
| `packages/public-libraries/Aetheris.Kernel.StandardLibrary.2.0.0-preview.3.nupkg` | 88,369 | `29a17260d9bb56b2bf79c8bbe3ae7b46efec533380da2f1ebc564cb954bcd93e` |
| `packages/public-libraries/Aetheris.Modules.2.0.0-preview.3.nupkg` | 17,185 | `a8e9b4d3a180ca0a50053952da13fc36d7a0a8c6e999c666fd481df8497e09bd` |
| `packages/public-libraries/Aetheris.Modules.BuiltIn.2.0.0-preview.3.nupkg` | 6,526 | `3e7a33aa17e5c95cebaefec5362066b8082f953eacfba7f2712f598244b9f542` |
| `packages/public-libraries/Aetheris.Piping.2.0.0-preview.3.nupkg` | 20,517 | `69cfa0e1a721e8f8534f9c52b0fd6ad6e68ce1439cd61050aa510055ee28c87f` |
| `packages/public-libraries/Aetheris.Semantics.2.0.0-preview.3.nupkg` | 30,509 | `f2360d2e86e9d8dfce0904c1d947e37761c50f1dd5c236c116fbbd9d8f703187` |
| `packages/public-libraries/Aetheris.SheetMetal.2.0.0-preview.3.nupkg` | 304,744 | `dc5579fe7128d6baed813a4b5b1dc8915009b1c6ee929f6e688082506fa52e08` |
| `packages/public-libraries/Aetheris.Surfacing.2.0.0-preview.3.nupkg` | 64,539 | `440fad9d7b4d27a76ae2338042206ea649c9a6a5ca468a518c006765ae16209f` |

The ZIP contains the self-contained CLI and Cadmata host, the NativeAOT Forge Host,
material catalog, public docs, licenses, tracked examples, and four foreign-language
client sources. Inspection found 672 archive entries under one
`Aetheris-win-x64/` root, no source/build/scratch directories, no PDBs, no stale
Preview version text, and no developer-machine paths.

## Public presentation

- The root README now explains the Firmament -> lowering -> STEP/Cadmata/Forge
  architecture, bounded capability set, Windows x64 qualification, installation
  surfaces, canonical examples, and AGPL/alternative-license position without
  milestone history.
- Three canonical images now live under `docs/public/assets`: the ordinary CAD
  mounting block, the CTC-03 semantic-PMI viewport, and a qualified Sheet Metal flat
  pattern. They are current Preview 3 assets with captions and no machine-local UI.
- Getting Started, CLI JSON-contract wording, release-bundle instructions, release
  notes, and the supported-feature PMI boundary were corrected against the shipped
  CLI. The canonical ordinary CAD, authored Sheet Metal, A36 FEA, and Forge examples
  are directly linked.
- The release notes are organized by product area. The user-facing known issues and
  support matrix were reviewed without broadening any claim.
- The NIST acknowledgment, Stanford Bunny provenance, third-party license boundary,
  AGPL-3.0 code license, and alternative-licensing statement remain visible.

## Release metadata and staging

- Aetheris/package version: `2.0.0-preview.3`.
- Qualified release RID: `win-x64` only.
- Public libraries: 16, plus the separately packaged `Aetheris.CLI` tool.
- Forge Host Protocol: v1, intentionally independent of the product version.
- Firmament VS Code extension: `0.3.0-preview.3`, intentionally independent.
- Expected tag: `v2.0.0-preview.3`.

All 17 NuGet packages have the expected version, non-generic descriptions, the real
repository URL, `AGPL-3.0-only` license expression, embedded README, and Preview 3
internal dependency versions. Public-library packaging now forces a rebuild so it
cannot silently reuse assemblies from an older commit. An isolated NuGet cache
proved the final packages report source revision `8b15c80376ccbe3d1e4487a48d96e753e407a4f7`.

The owner-facing tag checklist and factual GitHub release body are in
[`PREVIEW3-RELEASE-STAGING.md`](PREVIEW3-RELEASE-STAGING.md). No Hacker News title,
post, comment strategy, or launch copy was produced. The evidence-only claim sheet is
[`PREVIEW3-CLAIMS.md`](PREVIEW3-CLAIMS.md).

## CLA preparation

[`CLA-CANDIDATE.md`](../legal/CLA-CANDIDATE.md) records the requested business intent:
contributors retain ownership; the project receives broad, non-exclusive rights for
AGPL and alternative licensing; and alternative licensing must not replace continuing
AGPL availability. It is prominently marked candidate-only, not legal advice, not
operative, and pending qualified human attorney review. It deliberately defines no
acceptance mechanics.

[`CONTRIBUTING.md`](../../CONTRIBUTING.md) documents the build/test path, feature
freeze, engineering expectations, rights/provenance, practical AI-assisted-work
responsibility, and current CLA status. Unresolved recipient, irrevocability,
sublicensing, patent, employer/corporate, assent, continuity-scope, warranty, and
governing-law questions are handed to counsel in
[`CLA-COUNSEL-QUESTIONS.md`](../legal/CLA-COUNSEL-QUESTIONS.md), not guessed here.

## Final smoke

The exact ZIP was extracted to
`artifacts/a5 candidate smoke final/Aetheris-win-x64` and exercised without source
checkout runtime dependencies:

1. `aetheris.exe --help` started and exposed the public command surface.
2. The Getting Started PMI plate validated, built to STEP, and reimported as a
   seven-face part with one datum and one toleranced diameter.
3. The authored L-bracket built formed STEP and flattened to a 13,701-byte STEP plus
   1,559-byte SVG.
4. The bundled cantilever resolved `standard:aluminum/6061-t6` and converged its
   linear-elastic solve.
5. NativeAOT Forge Host `info`, `list`, and `describe` returned Protocol v1 and five
   Templates; the shipped Python client invoked the enclosure Template and produced
   STEP, flat STEP, and SVG.
6. The packaged Cadmata production host served its application shell with HTTP 200.

A fresh external .NET project restored only from the final public-library directory
plus NuGet.org, with an isolated package cache. It loaded four package assemblies at
Preview 3/source revision `8b15c803`, resolved Aluminum 5052-H32 from the packaged
SQLite catalog, discovered five Forge Templates, invoked one through the direct API,
and reimported the emitted STEP AP242.

## Final validation

- Release build: pass, 0 warnings, 0 errors.
- Full serial .NET suite: 3,017 passed, 0 failed, 0 skipped. The pre-existing
  `Aetheris.FrictionLab.Tests` assembly still has no discoverable tests.
- Cadmata: typecheck, 16 files / 81 tests, production build, and lint pass.
- VS Code extension: typecheck, 13 tests, build, and VSIX packaging pass.
- Public docs/examples: expanded qualification covers root/public/release/package
  Markdown links and documented CAD, Sheet Metal, FEA, and Forge routes.
- NuGet: 16 public libraries plus CLI packed; metadata audit and isolated external
  restore/run pass.
- NativeAOT Forge publish: pass with the existing audited trimming/AOT warnings;
  packaged discovery/invocation pass.
- Release ZIP and checksum generation: pass; all 19 SHA-256 entries verified.
- Version/stale-path/archive-debris scans: pass.
- `git diff --check`: pass.

The release build and full serial suite were rerun after the final package metadata
changes. The later report/checksum edits do not change product code or binary payloads.

## Remaining known issues

The canonical list is [`known-issues.md`](../public/reference/known-issues.md). It
records Windows x64 qualification, bounded imported STEP containment, the generic
tessellated mass-verifier boundary, opening-domain limits, imported-unit metadata,
ordinary-CAD material persistence, and the harmless Cadmata Three.js warning. These
are documented Preview 3 boundaries, not unresolved release defects.

## Feature freeze

Feature freeze is intact. A5 changed public presentation, legal-review preparation,
package descriptions, artifact generation, fixture composition, validation coverage,
and release evidence only. It added no CAD, Firmament, Sheet Metal, PMI, FEA,
material, Forge protocol, Cadmata, platform, licensing-enforcement, or architecture
capability.
