# DISPLAY-ARCH-X5.1 — Tessellate endpoint contract for imported occurrences

## 1. Problem statement

DISPLAY-ARCH-X5 validation exposed an order/load-sensitive failure in the server integration test for an exported-then-imported box occurrence. The direct tessellation endpoint sometimes returned HTTP 422 during the broader targeted server suite, even though the same test could pass when run alone.

## 2. Failing test

The failing test was:

```text
Aetheris.Server.Tests.KernelApiIntegrationTests.StepIo_ExportImportRoundTrip_TessellatesImportedOccurrence
```

It calls:

```text
POST /api/v1/documents/{documentId}/bodies/{importedOccurrenceId}/tessellate
```

The observed failure body was a bounded diagnostic envelope, not an import or occurrence lookup failure:

```text
success=false
status=422
code=ValidationFailed
source=Viewer.Tessellation.Timeout
message includes "bounded execution budget"
```

## 3. Root cause

The endpoint involved is the legacy/direct `/tessellate` mesh-lowering endpoint. It invokes `BrepDisplayTessellator.TessellateBounded`, which is intentionally all-or-nothing for a direct mesh response. Under broader integration-suite load, the bounded execution budget can expire before all faces are materialized, producing a stable timeout diagnostic and HTTP 422.

This was not a STEP import/export failure, not an imported occurrence id resolution failure, and not a DisplayIR DTO shape issue.

## 4. Endpoint distinction

### `/tessellate`

`/tessellate` is a direct bounded mesh endpoint. Its contract is explicit:

- success returns `TessellationResponseDto` mesh/edge data;
- bounded mesh failure returns a non-success API envelope with diagnostics such as `Viewer.Tessellation.Timeout`;
- it does not claim `displayAuthority = DisplayIR`.

### `/display/prepare`

`/display/prepare` is the DisplayIR view-authority endpoint. Its contract is different:

- success returns `DisplayPreparationResponseDto`;
- `displayAuthority = DisplayIR`;
- analytic, bounded mesh, wire, and diagnostic faces can coexist in a partial display response;
- display degradation must not be presented as import failure.

## 5. Chosen contract

X5.1 keeps `/tessellate` as a direct bounded mesh endpoint and allows either:

1. HTTP 200 with mesh data when bounded lowering completes; or
2. HTTP 422 with a stable bounded diagnostic when direct mesh lowering times out.

The user/view path remains `/display/prepare`, which must still return DisplayIR for the same imported occurrence.

## 6. Test changes

The server integration tests now make the endpoint distinction explicit:

- the legacy imported-occurrence tessellation test accepts success or an explicit bounded diagnostic envelope;
- `StepIo_ExportImportRoundTrip_TessellationEndpointContract_IsExplicit` documents the direct endpoint contract;
- `StepIo_ExportImportRoundTrip_DisplayPrepareStillReturnsDisplayIR` verifies the same imported occurrence still returns DisplayIR through `/display/prepare`;
- `TessellateEndpoint_DoesNotMasqueradeAsDisplayAuthority` verifies `/tessellate` does not report DisplayIR authority while `/display/prepare` does.

## 7. Non-goals

X5.1 does not change STEP import/export semantics, AP242 importer/exporter behavior, BRep topology, tessellator algorithms, DisplayIR authority, frontend typed renderables, Firmament V2 language/lowering, AIR Region route policy, CIR authority, Firmasm, or CAD feature behavior.
