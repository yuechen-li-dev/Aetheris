# Preview 2 publication proof

Verified 2026-08-10/11 after the tagged release workflow completed.

## Release identity

- Release commit: `bf87ac1568d19ab430c10367683da81138ce489a`
- Tag: `v2.0.0-preview.2`
- GitHub release: <https://github.com/yuechen-li-dev/Aetheris/releases/tag/v2.0.0-preview.2>
- Validated workflow: <https://github.com/yuechen-li-dev/Aetheris/actions/runs/31452887534>
- GitHub state: published, not draft, intentionally not marked prerelease, and
  selected as Latest. The older PMI Injection Demo is no longer Latest.

The workflow completed its build/test, installed-artifact release smoke, NuGet
trusted-publication, and GitHub publication jobs successfully.

## Published assets

The assets were downloaded again from the public GitHub release. Their computed
SHA-256 values matched both the attached `SHA256SUMS.txt` and GitHub's asset
digests.

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| `Aetheris-2.0.0-preview.2-win-x64.zip` | 95,324,022 | `17b30986e3bbd3371602b4ebc66fd4cf57282e19f713cccf2d4be5a1041d2025` |
| `aetheris-firmament-0.2.0-preview.2.vsix` | 12,157 | `152509f13776637d008f48369a337d53a855e7abb41466575b0063eff08ef3f6` |
| `Aetheris.CLI.2.0.0-preview.2.nupkg` | 3,528,957 | `d100d926aedf39804f5050bdef6550abb33239ab9b597141370ca40dd69386e1` |

The locally repeated Windows packaging proof is recorded separately in
`validation-report.md`. Hosted-runner output has its own frozen checksum set
because compression and SDK/tool inputs are environment-specific; the release
workflow publishes and smoke-tests one exact artifact set rather than rebuilding
between validation and upload.

## NuGet publication and public-feed smoke

The trusted-publishing log records NuGet accepting
`Aetheris.CLI.2.0.0-preview.2.nupkg` with HTTP `201 Created`. After catalog/CDN
propagation, the public flat-container index exposed `2.0.0-preview.2`.

A fresh tool directory then installed exactly `Aetheris.CLI` version
`2.0.0-preview.2` using only `https://api.nuget.org/v3/index.json`. The installed
tool reported `aetheris 2.0.0-preview.2`, validated and built the canonical
`box-through-hole.firmament`, inspected its generated STEP, and completed
`asm inspect` on the M3 bearing-module fixture successfully.

Public package: <https://www.nuget.org/packages/Aetheris.CLI/2.0.0-preview.2>
