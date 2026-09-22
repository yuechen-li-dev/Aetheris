# Aetheris CLI

`Aetheris.CLI` installs the `aetheris` command-line compiler for the bounded
Firmament exact-CAD and STEP AP242 workflow.

```powershell
dotnet tool install --global Aetheris.CLI --prerelease
aetheris --help
```

The tool supports validation, STEP build, inspection, analysis, and verification
from Firmament source. It requires the .NET 10 runtime. Aetheris is licensed
under the GNU Affero General Public License v3.0 (`AGPL-3.0`); third-party
assets retain their respective licenses and provenance. Alternative licensing
is available on request.

`aetheris inspect-3dm <file.3dm> [--json]` inventories Rhino/OpenNURBS geometry,
units, topology, and current import blockers. It does not convert 3DM to STEP.
`aetheris recover-3dm <file.3dm> [--json]` measures provisional analytic and
non-rational recovery candidates. See `3DM-IMPORT-X0.md` and `3DM-RECOVERY-X1.md`
for the local qualification boundaries.

`aetheris view` needs Cadmata. The NuGet global tool does not bundle the viewer;
download the Windows bundle from the GitHub release for package-relative Cadmata
discovery and the complete desktop experience.

See the [Aetheris manual](https://yuechen-li-dev.github.io/aetheris/) for the
supported Firmament surface and Preview 3 limitations.
