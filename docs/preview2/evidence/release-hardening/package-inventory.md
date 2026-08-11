# Preview 2 package inventory and dependency policy

| Public artifact | Version | Publication |
| --- | --- | --- |
| `Aetheris.CLI` NuGet global tool | `2.0.0-preview.2` | NuGet.org |
| Windows x64 CLI + Cadmata bundle | `2.0.0-preview.2` | GitHub Release |
| Firmament VSIX | `0.2.0-preview.2` | GitHub Release |
| `Aetheris.Kernel.Core` | `2.0.0-preview.2` | NuGet.org |
| `Aetheris.Kernel.Firmament` | `2.0.0-preview.2` | NuGet.org |
| `Aetheris.Forge.Host` | `2.0.0-preview.2` | NuGet.org |
| `Aetheris.Forge.KernelSDK` | `2.0.0-preview.2` | NuGet.org |

The four public library entry points are backed by a complete, version-aligned
runtime dependency graph. Supporting packages published at the same version are
`Aetheris.Collaboration`, `Aetheris.Continuum`, `Aetheris.FEA`, `Aetheris.Forge`,
`Aetheris.Kernel.StandardLibrary`, `Aetheris.Modules`,
`Aetheris.Modules.BuiltIn`, `Aetheris.Piping`, `Aetheris.Semantics`,
`Aetheris.SheetMetal`, and `Aetheris.Surfacing`.

The CLI tool embeds the bounded runtime graph: Kernel.Firmament/Core,
StandardLibrary, Forge contracts, Semantics, Continuum, FEA, Collaboration,
Modules/BuiltIn, Surfacing, Piping, SheetMetal, and FrictionLab runtime support.

Trusted-publication and isolated public-feed restore evidence is recorded in
`public-library-publication.md`.
