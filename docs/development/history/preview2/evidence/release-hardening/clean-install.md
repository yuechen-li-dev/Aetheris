# Clean-install contract

The release smoke lane installs `Aetheris.CLI.2.0.0-preview.2.nupkg` into a new
tool directory from a local package feed, outside project-reference execution.
It verifies version/help, canonical compile, STEP inspection/verification,
assembly inspection, and Drawing PDF/PPTX output. The standalone ZIP is expanded
into a clean directory and tested independently.

Official paths are the NuGet global tool, self-contained Windows x64 bundle,
and Firmament VSIX. Prerequisites are listed in the Preview 2 release notes.
