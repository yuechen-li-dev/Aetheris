# Aetheris 2.0.0 release notes

Aetheris compiles Firmament engineering source into reviewable artifacts. The
managed CLI runs in local development and CI; the Windows bundle also includes
Cadmata and the NativeAOT Forge Host.

```powershell
dotnet tool install --global Aetheris.CLI --version 2.0.0
aetheris validate fixtures/Canonical/Learn/01-plate.firmament --json
aetheris build fixtures/Canonical/Learn/01-plate.firmament --out artifacts/local/first-plate.step --json
```

Use the first-plate source from the [learning guide](../learn/01-first-part.md).
The compiler supports bounded exact parts, assemblies, Sheet Metal, semantic
PMI, STEP import/export, mesh export, drawings and analysis. See the
[supported features](supported-features.md) and [known issues](known-issues.md)
for the actual capability contracts.

Validation and build share domain selection. Unknown fields and unsupported
units reject at their owning binders. Native V2 `model` is a case alias for
`Model`; historical serialized inputs and `solid` grammar retain their separate
compatibility boundaries. Normal validation includes required engineering
preflight; parse-only validation explicitly reports its limited level.

The managed tool is qualified on Windows x64 and Linux x64. The self-contained
desktop bundle targets Windows x64. The public NuGet library set includes its
full dependency closure and pins Copeland's managed VTS/WGSL compiler to 0.1.0.
No Copeland checkout or private feed is needed. The core CLI has no third-party
OS-native dependencies; SQLite and 3DM remain optional adapters. Drawing Notes
use pinned PDF.js browser assets; PDF drawing output remains managed.

Forge Host Protocol v1 and the VS Code extension are independently versioned.
Cloth3D and Humanoid packages remain experimental. STEP geometry and explicitly
authored semantic PMI retain their engineering authority; assembly export does
not implicitly promote part PMI into assembly PMI.

Release assets include an inventory and SHA-256 checksums. See the
[bundle guide](../release-bundle.md) for layout and use.
