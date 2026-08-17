# Aetheris 2.0.0-preview.3

Aetheris turns semantic engineering intent into deterministic CAD and analysis artifacts. Firmament V2 is its authoring language; the Aetheris compiler, geometry kernel, Standard Library, Sheet Metal and FEA modules lower that intent to STEP AP242, manufacturing outputs, and analysis results. Cadmata is the interactive 3D inspection surface, and Forge exposes qualified templates to other programs.

Preview 3 is feature-frozen and qualified on Windows x64. It includes bounded exact geometry, typed Templates and static engineering data, STEP AP242 import/export, semantic PMI, authored and reconstructed Sheet Metal workflows, a linear-elastic FEA path, the material catalog, Cadmata, and Forge Host Protocol v1. The exact boundaries are in the [Preview 3 support matrix](docs/public/reference/supported-features.md).

## Start here

- [Getting Started](docs/public/getting-started.md) — install, build a real part, inspect it, and invoke a Template.
- [Public documentation](docs/public/README.md) — authoritative user-facing Preview 3 behavior.
- [Firmament overview](docs/public/firmament/overview.md) — the language's engineering mental model.
- [CLI reference](docs/public/reference/cli.md) — stable commands and structured-output expectations.

After publication, install the CLI with the .NET 10 SDK:

```powershell
dotnet tool install --global Aetheris.CLI --prerelease
aetheris --version
```

From a source checkout, use the same real CLI path without installing it:

```powershell
dotnet run --project Aetheris.CLI -c Release -- build fixtures/FirmamentV2/Canonical/valid/box-holes-pmi-chamfer.firmament --output artifacts/mounting-plate.step --json
dotnet run --project Aetheris.CLI -c Release -- analyze artifacts/mounting-plate.step --json
```

The packaged CLI does not include Cadmata; use the Windows bundle for `aetheris view`. Maintainer build and test policy lives in [development documentation](docs/development/README.md). Historical milestone, experiment, architecture, and artifact reports remain valuable evidence, but they are not the public behavior contract.

## License and acknowledgments

Aetheris source is licensed under GNU AGPL-3.0, except for third-party assets which retain their own licenses and attribution. Alternative licensing is available on request. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Special thanks to the National Institute of Standards and Technology (NIST) for the STEP AP242 test models used in development and validation. Their inclusion does not imply NIST endorsement, authorship of Aetheris, or application of the Aetheris license to those models.
