# Aetheris CLI

`Aetheris.CLI` installs the `aetheris` command-line compiler for the bounded
Firmament exact-CAD and STEP AP242 workflow.

```powershell
dotnet tool install --global Aetheris.CLI --prerelease
aetheris --help
```

The tool supports validation, STEP build, inspection, analysis, and verification
from Firmament source. It requires the .NET 10 runtime.

`aetheris view` needs Cadmata. The NuGet global tool does not bundle the viewer;
download the Windows bundle from the GitHub release for package-relative Cadmata
discovery and the complete desktop experience.

See the [Aetheris manual](https://yuechen-li-dev.github.io/aetheris/) for the
supported Firmament surface and Preview 1 limitations.
