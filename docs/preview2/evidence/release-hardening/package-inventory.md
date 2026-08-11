# Preview 2 package inventory and dependency policy

| Public artifact | Version | Publication |
| --- | --- | --- |
| `Aetheris.CLI` NuGet global tool | `2.0.0-preview.2` | NuGet.org |
| Windows x64 CLI + Cadmata bundle | `2.0.0-preview.2` | GitHub Release |
| Firmament VSIX | `0.2.0-preview.2` | GitHub Release |
| `Aetheris.Forge.Host` assembly | `2.0.0-preview.2` | Included in source/build contracts; not independently published in this freeze |
| `Aetheris.Forge.KernelSDK` assembly | `2.0.0-preview.2` | Included in source/build contracts; not independently published in this freeze |

Only `Aetheris.CLI` is an intentionally established NuGet package for Preview 2.
Host and KernelSDK have coherent names and public assembly versions, but their
current project-reference dependency graph has not been promoted into a complete
independently published NuGet package family. Publishing only their leaf packages
would create unresolved dependencies, so this release does not do so accidentally.

The CLI tool embeds the bounded runtime graph: Kernel.Firmament/Core,
StandardLibrary, Forge contracts, Semantics, Continuum, FEA, Collaboration,
Modules/BuiltIn, Surfacing, Piping, SheetMetal, and FrictionLab runtime support.
