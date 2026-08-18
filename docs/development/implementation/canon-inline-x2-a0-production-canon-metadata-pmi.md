# CANON-INLINE-X2-A0 production canon metadata/PMI audit

## Current canon behavior

`aetheris canon` imports a part-like STEP/AP242 file with `Step242Importer.ImportBody` and re-exports the resulting `BrepBody` with `Step242Exporter.ExportBody`. Before this milestone the command always used exporter defaults, which are deterministic and snapshot-friendly:

- `FILE_DESCRIPTION` was always `Aetheris AP242 subset export`.
- `FILE_NAME` was always `aetheris_export.step`, timestamp `1970-01-01T00:00:00`, author/organization `Aetheris`, and authoring/originating system `Aetheris.Kernel`.
- `PRODUCT` id was always `AETHERIS`, product name defaulted to `AetherisBody`, and product description was empty.
- `MANIFOLD_SOLID_BREP`, `BREP_WITH_VOIDS`, and `SHAPE_REPRESENTATION` names used the exporter product name.

That behavior remains the deterministic default.

## Canon modes

The CLI now exposes an explicit mode switch:

```bash
aetheris canon input.step --out deterministic.step --mode deterministic
aetheris canon input.step --out production.step --mode production
```

`deterministic` remains the default and keeps stable generic metadata for tests, snapshots, and reproducible hashes.

`production` means `ProductionPreserveMetadata`: canon still rebuilds Aetheris-canonical AP242 from imported topology, but it preserves the bounded source metadata fields that are currently cheap and explicit to recover.

## Metadata preserved now in production mode

Production mode reads source header/product metadata from the input STEP text and feeds supported values into `Step242ExportOptions`:

- source `FILE_DESCRIPTION` first description string;
- source `FILE_NAME` file name;
- source `FILE_NAME` author first string;
- source `FILE_NAME` organization first string;
- source `FILE_NAME` creation timestamp;
- source `FILE_NAME` originating system;
- source `FILE_NAME` authorization;
- source `PRODUCT` name;
- source `PRODUCT` description.

The output remains canonicalized AP242, not a byte-preserving rewrite. Product id remains `AETHERIS` to preserve the Aetheris-canonical identity marker while product/body names and descriptions can carry source part metadata.

## Metadata still dropped or normalized

The current implementation does not preserve arbitrary header cardinality or graph metadata. In particular, it does not preserve:

- multiple authors or organizations beyond the first string;
- source `FILE_SCHEMA` variants beyond the Aetheris AP242 canonical schema;
- preprocessor version as distinct from originating system in the production output;
- arbitrary `PRODUCT_DEFINITION`, presentation, approval, security, management, validation, or vendor extension graphs;
- original entity ids, ordering, formatting, comments, or byte layout.

This is intentional for X2-A0: production canon is canonical AP242 with bounded metadata preservation, not raw STEP editing.

## Source PMI preservation status

Current status: **partially supported**.

The exporter can emit bounded semantic PMI records when Firmament/Aetheris semantic models supply `Step242SemanticPmi` payloads. Evidence exists for semantic hole diameter and datum output in the STEP exporter and Firmament V2 PMI pipeline.

The importer does **not** parse existing source semantic PMI into a reusable PMI model during canon. Therefore source PMI in an external/vendor STEP file is dropped by `aetheris canon`, including production mode. No source PMI preservation claim should be made until a passing import/canon/re-export test proves it.

Recommended next milestone:

```text
CANON-PMI-X1 — parse supported semantic PMI subset into Step242SemanticPmi records during import and re-emit in production canon mode
```

## Inline STEP PMI attachment groundwork

INLINE-STEP-X1 already preserves useful provenance for the wrapper path: parsed `InlineStep` records include the source path, normalized path, SHA-256 content hash, canonical-input flag, and canonical evidence string. X1 still requires Aetheris-canonical AP242 and rejects arbitrary raw external/vendor STEP.

What is missing for INLINE-STEP-X2 semantic PMI attachment is a topology target reference contract:

- There is no stable canonical face/entity id map exposed by `Step242Importer` today.
- Imported `BrepBody` topology has internal `FaceId`/`EdgeId` handles, but those ids are importer-created and are not yet documented as stable user-facing selectors for Firmament syntax.
- A clean X2 path is for canonical STEP import to expose an optional map from canonical STEP entity references (for example `ADVANCED_FACE` instance ids) to imported topology handles, then allow Firmament PMI overlays to target imported topology through a narrow canonical reference model.

Smallest proposed target model for INLINE-STEP-X2:

```text
InlineStepTopologyTarget
  inline symbol name
  source canonical content hash
  topology kind: face | edge | vertex
  canonical STEP entity reference: #<id>
  resolved internal topology handle after import
```

That keeps raw vendor STEP out of Firmament source, avoids graphical PMI, and allows semantic Firmament overlays to bind to Aetheris-canonical topology after `aetheris canon --mode production` has prepared the source.

## Deferred work

- Parse and preserve a tested semantic PMI subset from source AP242.
- Expose canonical STEP entity to topology handle maps from `Step242Importer`.
- Add Firmament InlineStep PMI target syntax only after the reference model is implemented.
- Preserve richer product management metadata only when there is a bounded importer/exporter model and tests.
