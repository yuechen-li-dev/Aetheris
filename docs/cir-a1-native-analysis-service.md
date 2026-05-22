# CIR-A1: Native Internal CIR Analysis Service

`CirNativeAnalysisService` introduces an internal/test-facing API for CIR-backed native analysis without changing public CLI behavior.

## Purpose

- Analyze CIR-supported Firmament lowering plans without materializing BRep.
- Provide a stable structured result contract for CIR-A2 hidden CLI wiring.
- Surface uncertainty and lowering coverage explicitly.

## Supported inputs

- `FirmamentPrimitiveLoweringPlan` via `AnalyzeFirmamentPlan`.
- `CirTape` + bounds via `AnalyzeTape`.
- `CirNode` via `AnalyzeNode` (lowered to tape internally).

## Result contract overview

Top-level result includes:

- `Success`, `Backend` (`cir`), `InputKind`, `ResultKind`.
- `Notes` and `Diagnostics`.
- `Lowering` coverage summary (supported flag, supported/unsupported op counters, diagnostics).
- `Bounds`.
- `Volume` metadata (dense/adaptive estimator, estimate, sampling/region counters, adaptive trace count/head).
- point classification entries (point, classification, signed-distance value).

## No-fallback policy

When Firmament-to-CIR lowering is unsupported, the service returns `Success=false` with lowering diagnostics and does **not** fall back to BRep.

Returned note:

- `BRep backend may still support materialized analysis; CIR lowering is unsupported for this model.`

## Status and next step

This is internal/experimental only and intentionally not public CLI surface.

Next step (CIR-A2): wire hidden/experimental CLI entry to this service with explicit backend selection and unchanged production defaults.
