# Aetheris CLI

`Aetheris.CLI` installs the `aetheris` command-line compiler for the bounded
Firmament exact-CAD and STEP AP242 workflow.

```powershell
dotnet tool install --global Aetheris.CLI --version 2.0.0
aetheris --help
```

The tool supports validation, STEP build, inspection, analysis, and verification
from Firmament source. It requires the .NET 10 runtime. Aetheris is licensed
under the GNU Affero General Public License v3.0 (`AGPL-3.0`); third-party
assets retain their respective licenses and provenance. Alternative licensing
is available on request.

`aetheris-3dm inspect <file.3dm> [--json]` inventories Rhino/OpenNURBS geometry,
units, topology, and current import blockers. It does not convert 3DM to STEP.
`aetheris-3dm recover <file.3dm> [--json]` measures provisional analytic and
non-rational recovery candidates. See `3DM-IMPORT-X0.md` and `3DM-RECOVERY-X1.md`
for the local qualification boundaries.

`aetheris view` needs Cadmata. The NuGet global tool does not bundle the viewer;
download the Windows bundle from the GitHub release for package-relative Cadmata
discovery and the complete desktop experience.

See the [Aetheris manual](https://yuechen-li-dev.github.io/aetheris/) for the
supported Firmament surface and capability limits.

The core CLI bundles no third-party OS-native libraries. The .NET runtime/apphost is still required (or included in self-contained distributions). 3DM inspection and recovery are supplied by the separate `Aetheris.ThreeDm.CLI` tool (`dotnet tool install --global Aetheris.ThreeDm.CLI --version 2.0.0`), which includes native Rhino3dm. The built-in material catalog is managed; custom SQLite catalogs use the optional `Aetheris.Materials.Sqlite` library.

Drawing Notes previews and crops render locally in the browser using bundled PDF.js assets; no CDN or native PDF renderer is required. Headless PNG render/crop commands require `--renderer <path-to-pdftoppm>`, selecting an external Poppler installation explicitly. PDF drawing generation itself remains managed vector output and needs no rasterizer.
