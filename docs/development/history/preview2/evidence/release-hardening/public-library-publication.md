# Public library NuGet publication proof

Verified 2026-08-10/11.

The public library workflow at
<https://github.com/yuechen-li-dev/Aetheris/actions/runs/31457582605> built the
complete package graph from commit
`f91f561dfc230c3858d1312e3aba509d2d515cde`, ran the normal build/test lane,
restored an isolated consumer from the packaged local feed, authenticated using
NuGet trusted publishing, and pushed every package in dependency order. NuGet
returned HTTP `201 Created` for all fifteen packages.

## Public entry packages

- <https://www.nuget.org/packages/Aetheris.Kernel.Core/2.0.0-preview.2>
- <https://www.nuget.org/packages/Aetheris.Kernel.Firmament/2.0.0-preview.2>
- <https://www.nuget.org/packages/Aetheris.Forge.Host/2.0.0-preview.2>
- <https://www.nuget.org/packages/Aetheris.Forge.KernelSDK/2.0.0-preview.2>

## Supporting dependency packages

`Aetheris.Collaboration`, `Aetheris.Continuum`, `Aetheris.FEA`,
`Aetheris.Forge`, `Aetheris.Kernel.StandardLibrary`, `Aetheris.Modules`,
`Aetheris.Modules.BuiltIn`, `Aetheris.Piping`, `Aetheris.Semantics`,
`Aetheris.SheetMetal`, and `Aetheris.Surfacing` are also published at
`2.0.0-preview.2` so none of the entry packages has an unresolved private
project dependency.

After NuGet catalog propagation completed, a clean consumer restored all four
entry packages using only `https://api.nuget.org/v3/index.json` and a brand-new
global-packages directory. It loaded all four assemblies successfully, each
reporting `2.0.0-preview.2` with source revision
`f91f561dfc230c3858d1312e3aba509d2d515cde`.
