# Preview 3 factual launch claims

This is verification scaffolding, not launch or Hacker News prose. Counts and hashes must match the final A5 report before publication.

| Claim | Final A5 value | Evidence |
|---|---:|---|
| Qualified release target | Windows x64 (`win-x64`) | [`preview3-release-manifest.json`](preview3-release-manifest.json), final bundle smoke |
| Aetheris version | `2.0.0-preview.3` | `Directory.Build.props`, package audit, CLI `--version` |
| Forge Host protocol | v1 | packaged `info`/list/describe/invoke smoke |
| Foreign-language clients | Python, Go, Rust, TypeScript | shipped clients and protocol qualification; A4 proved byte-equivalent output |
| Public library packages | 16 | `scripts/package-public-libraries.ps1`, final package inventory |
| Cadmata tests | 81 passed in 16 files | final A5 validation report |
| VS Code tests | 13 passed | final A5 validation report |
| Serial .NET tests | 3,017 passed; 0 failed; 0 skipped | final A5 validation report |
| CTC-03 manufacturing PMI | 3 datums, 13 dimensions, 5 position controls, 8 annotations, 23 associated items | [`PREVIEW3-HARDEN-A4.md`](PREVIEW3-HARDEN-A4.md), final PMI smoke |
| A36 cantilever sanity witness | ~25.06 µm Aetheris vs ~24.7 µm simple beam theory | [`fea.md`](../public/firmament/fea.md), final FEA smoke |
| Release integrity | deterministic archive normalization retained; 19 staged binary hashes recomputed and verified | `scripts/package-release.ps1`, `scripts/finalize-publication.ps1`, final A5 report |
| Feature freeze | no new product capability in A5 | final A5 diff audit/report |

Do not convert this sheet into an HN title, post, comment plan, or marketing claim.
