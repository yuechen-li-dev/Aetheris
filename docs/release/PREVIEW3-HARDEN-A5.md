# PREVIEW3-HARDEN-A5

## Publication verdict

Yes. Aetheris `2.0.0-preview.3` completed the A5 publication gate. No tag, push,
package publication, or remote release was performed by A5.

After A5, Ubuntu push checks exposed two sources of platform-dependent STEP bytes:
line endings and final-bit variation in transcendental geometry values. The publication
follow-up made AP242 text explicitly CRLF and centralized numeric output at 13 fractional
digits, which remains far below kernel tolerances. Native Windows and Linux recipe tests
now produce the same hashes. Because those corrections change binary payloads, the A5
candidate hashes were superseded. The tag workflow generates `SHA256SUMS.txt` and
`RELEASE-INVENTORY.md` from the tagged commit and verifies them before publication.

The final bounded smoke passed from a clean extraction of the Windows ZIP in a path
containing spaces. ReleaseBlocker and MustFix count: zero. The remaining boundaries
are the documented Preview 3 limitations.

## Final artifacts

The tagged workflow stages 19 release-distributed binaries: the Windows x64 ZIP,
Firmament VSIX, CLI package, and 16 public libraries. It generates the canonical
`SHA256SUMS.txt` and `RELEASE-INVENTORY.md` from those exact bytes, verifies every
entry in a separate release-smoke job, then publishes the same downloaded workflow
artifact. Checked-in documentation intentionally does not duplicate mutable binary
hashes.

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
