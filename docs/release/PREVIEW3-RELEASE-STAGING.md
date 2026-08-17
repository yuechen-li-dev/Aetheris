# Preview 3 release staging

## Tag and commit

- Expected annotated tag: `v2.0.0-preview.3`
- Version: `2.0.0-preview.3`
- Qualified RID: `win-x64`
- Forge Host Protocol: `1` (independently versioned)
- VS Code extension: `0.3.0-preview.3` (independently versioned)
- Publish/push status: not performed by A5

Create the tag only from the final clean release commit after the A5 report records the same commit and final artifact hashes.

## Required release assets

- `Aetheris-2.0.0-preview.3-win-x64.zip`
- `Aetheris.CLI.2.0.0-preview.3.nupkg`
- 16 public `Aetheris.*.2.0.0-preview.3.nupkg` library packages
- `aetheris-firmament-0.3.0-preview.3.vsix`
- `SHA256SUMS.txt`
- `RELEASE-INVENTORY.md`

After both package scripts succeed, consolidate the staged assets and regenerate the
canonical hashes and inventory from their actual bytes:

```powershell
.\scripts\finalize-publication.ps1 `
  -ReleaseDirectory artifacts/release/a5-final `
  -PublicLibrariesDirectory artifacts/release/a5-public-packages-publication
```

The public documentation is under [`docs/public`](../public/README.md). The canonical release notes and limitations are [`release-notes.md`](../public/reference/release-notes.md) and [`known-issues.md`](../public/reference/known-issues.md).

## GitHub release body candidate

Aetheris `2.0.0-preview.3` is the feature-frozen Windows x64 release of a semantic/compiler-style CAD system. Firmament engineering intent is lowered through bounded geometry, manufacturing, and analysis workflows to STEP AP242 and related artifacts, with Cadmata for interactive 3D inspection and Forge Host Protocol v1 for language-neutral Template invocation.

Highlights:

- Firmament V2 Records, Templates, static engineering data, and explicit diagnostics
- bounded analytic/prismatic modeling with Boss, finite Pocket, semantic holes, and EdgeFinish
- STEP AP242 import/export and semantic PMI
- formed/flat Sheet Metal with material, DFM, STEP, and SVG outputs
- Standard Library materials and bounded linear-elastic FEA
- Cadmata production UI and NativeAOT Forge.Host with Python/Go/Rust/TypeScript clients

Download the Windows ZIP for the complete product. The standalone CLI tool, 16 public libraries, and VS Code extension are separate assets. Preview 3 release binaries are qualified only for `win-x64`; geometry, imported STEP, Sheet Metal, PMI, and FEA boundaries are documented in [known issues](../public/reference/known-issues.md) and the [support matrix](../public/reference/supported-features.md).

Start with [Getting Started](../public/getting-started.md). Verify binary downloads with `SHA256SUMS.txt`.

Aetheris code is available under GNU AGPL-3.0; alternative licensing is available on request. Third-party assets retain their respective licenses and provenance.

## Owner release checklist

1. Confirm the A5 report verdict, clean commit, and no post-validation changes.
2. Compare upload bytes to `RELEASE-INVENTORY.md` and `SHA256SUMS.txt`.
3. Create and push annotated tag `v2.0.0-preview.3`.
4. Create the GitHub release from that tag and upload the exact qualified artifacts.
5. Paste/adapt the factual body above; do not infer support beyond the linked matrix.
6. Verify the published asset hashes once after upload.
