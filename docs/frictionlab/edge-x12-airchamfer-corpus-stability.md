# EDGE-X12 — Gated AirChamfer artifact corpus stability check

## Purpose and scope

EDGE-X12 adds an explicitly gated/manual stability check for the EDGE-X11 AirChamfer STEP artifact corpus. The check exists to prove that repeated runs of the experimental/lab corpus route produce stable JSON evidence, stable STEP marker summaries, stable topology summaries, and stable STEP artifact bytes for the current controlled cases.

The route under check remains:

```text
AirChamferShadowRoute -> AirChamferRealBodyPrototype
```

This is only an artifact-corpus guard for the experimental CLI lane. It is not a production chamfer route, not a replacement for legacy `BrepBoundedChamfer`, and not a broad geometry feature expansion.

## EDGE-X11 reference

EDGE-X11 introduced the experimental CLI corpus route:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental airchamfer-corpus --out-dir <dir> --json
```

That command generates successful STEP artifacts plus the corpus JSON summary:

- `edge-x11-airchamfer-cube-canonical.step`
- `edge-x11-airchamfer-cube-nonorthogonal.step`
- `edge-x11-airchamfer-corpus.json`

Rejected or deferred corpus rows remain JSON-only evidence and do not write STEP files.

## Manual corpus generation

Generate the corpus manually with:

```bash
tmp=$(mktemp -d)
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental airchamfer-corpus --out-dir "$tmp/edge-x11" --json
```

## Gated stability test

The EDGE-X12 check lives in `Aetheris.CLI.Tests` as:

```text
AirChamferCorpusStability_Repeated_Cli_Runs_Produce_Stable_Json_Markers_Topology_And_Step_Hashes
```

It is marked with the xUnit trait:

```text
Category=ArtifactCorpus
```

It also has an environment-variable guard so normal/default test runs do not execute the expensive/manual artifact corpus comparison body:

```text
AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1
```

Run it explicitly with either the name filter:

```bash
AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1 dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "AirChamferCorpusStability"
```

or the trait filter:

```bash
AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1 dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "Category=ArtifactCorpus"
```

If the environment variable is omitted, the test reports a clear skip/no-op message and returns without generating or comparing the corpus. This keeps `dotnet test` and `./scripts/test-all.sh` on their normal/default paths unless a developer explicitly opts in.

## What is compared

The stability check creates two independent temporary output directories and invokes the CLI corpus route twice through `CliRunner`, matching existing CLI test conventions.

For each run it verifies:

- stdout JSON parses successfully;
- `edge-x11-airchamfer-corpus.json` exists and parses successfully;
- stdout/file JSON agree on corpus version, milestone, and case count;
- top-level diagnostics include:
  - `edge-x11-legacy-authority-preserved`;
  - `edge-x11-no-production-route-replacement`;
  - `edge-x11-no-3d-boolean-used`;
- top-level guard booleans preserve legacy authority, no production output change, no production route replacement, and no 3D Boolean;
- stable projected JSON fields match across repeated runs, ignoring run-specific output directory and summary path values.

For each case it compares:

- case names;
- statuses;
- artifact filenames for successful cases;
- required-present marker result booleans;
- forbidden-absent marker result booleans;
- raw marker booleans from `stepMarkerSummary.markers`;
- topology summary JSON when present;
- case-level guard booleans for legacy authority, production output, production route replacement, and 3D Boolean;
- diagnostics and errors.

For successful STEP artifacts it verifies the marker smoke contract:

Required present:

- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `PLANE`

Forbidden absent:

- `CYLINDRICAL_SURFACE`
- `BREP_WITH_VOIDS`

## STEP comparison mode

EDGE-X12 uses raw STEP SHA256 hash comparison for the current EDGE-X11 artifacts. Local repeated runs showed that the raw STEP text is deterministic for both successful corpus cases, and the gated test compares hashes by artifact filename.

If a future exporter introduces legitimate run-specific identifiers, timestamps, or ordering into these experimental artifacts, the test should be intentionally changed to a normalized comparison mode rather than forcing raw hash parity. The normalized mode should keep comparing JSON schema/status, STEP marker booleans, relevant entity counts if available, and topology summaries.

## Why this is not part of normal unit tests

The check writes artifact directories, runs the corpus route twice, parses JSON summaries, reads STEP files, and hashes the generated STEP bytes. That is valuable for manual artifact-corpus validation but too specific for default unit-test execution. The explicit trait/name filter plus environment guard prevents accidental inclusion in normal test sweeps while preserving an exact command for release/milestone validation.

## Non-goals

EDGE-X12 does not add or change:

- production chamfer behavior;
- production fillet behavior;
- production chamfer route replacement;
- STEP exporter/importer behavior;
- Boolean core behavior;
- arbitrary edge selection;
- 3D Boolean usage;
- fillet geometry;
- chain/corner support;
- sketch solver behavior;
- clipping engine behavior;
- NURBS/freeform support.

## Tests run

Recommended validation for this milestone:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- --help
tmp=$(mktemp -d) && dotnet run --project Aetheris.CLI --framework net10.0 -- experimental airchamfer-corpus --out-dir "$tmp/edge-x11" --json
AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1 dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "AirChamferCorpusStability"
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "AirChamfer|Experimental|Lab|Step|CliBaseline|Export|Corpus"
dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "AirChamferShadow|AirChamferFeatureRecognition|AirChamferControlledBody|AirChamferTopologyGraft|AirChamferClosedWitness|AirChamferGeometryArtifact|AirChamferTopologyPlan|AirChamferJudgment|AirChamferPolicy|AirChamferPatch|EdgeSweep|Chamfer|Fillet|EdgeFinish|CIRLab"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "Chamfer|Fillet|Corner|TriangularPrism|FirmamentPrimitive|FirmamentStepExporter|LineArcProfileExtrude|SemanticRecovery|FrepMaterializer|Rematerialize"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "Judgment|Chamfer|Fillet|Corner|TriangularPrism|BrepPrimitives|Step242|Primitive|Extrude|Boolean|SafeComposition"
```
