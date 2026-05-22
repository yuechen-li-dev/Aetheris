# Firmament Build and Export Workflow

This document describes the current Firmament v1 golden path as it exists in the repository today.

## Source shape

A Firmament source file is a `.firmament` file with canonical section order:

1. `firmament`
2. `model`
3. `schema` (optional)
4. `ops`
5. `pmi` (optional)

The syntax is the repository's indentation-based TOON shape, not JSON.

## High-level compile flow

The current in-repo flow is:

1. parse `.firmament` source into the parsed document model
2. validate required fields, source ordering, selectors, placement, and schema shape
3. lower supported primitive and boolean ops into execution plans
4. execute supported geometry ops
5. execute validation ops against the compiled/executed result
6. optionally export the selected executed body to STEP AP242

There is not yet a polished standalone Firmament CLI documented in this repository. Today, the workflow is exercised through the compiler/exporter code paths, the `FirmamentBuildAndExport.Run(string sourcePath)` helper, and the automated tests.

## Export policy

The current STEP path exports the **last executed geometric body**.

That means:

- only successfully executed primitive or boolean bodies are candidates
- validation ops are never export bodies
- if a later geometric op does not execute, export can fall back to an earlier executed body

This is the current explicit export contract, not a future roadmap statement.

## "Last executed geometric body"

In the current implementation, "last executed geometric body" means:

- walk executed primitives and executed booleans
- compare their source op indices
- choose the successfully executed body with the greatest op index

So the exported body is tied to execution success, not simply to the last textual op in the file.

## Where examples and artifacts live

- example source files: `testdata/firmament/examples/`
- existing fixture corpus used by implementation tests: `testdata/firmament/fixtures/`
- generated STEP artifacts checked by exporter tests and demo helper flows: `testdata/firmament/exports/`

## How to review the golden path today

The most direct way to review the current workflow is through the Firmament tests:

- compile/format/validation coverage in `Aetheris.Kernel.Firmament.Tests`
- export coverage in `Aetheris.Kernel.Firmament.Tests/FirmamentStepExporterTests.cs`
- example smoke coverage in `Aetheris.Kernel.Firmament.Tests/FirmamentExamplePackSmokeTests.cs`

That keeps the documentation aligned to the implemented repository surface without inventing a CLI that does not exist yet.
