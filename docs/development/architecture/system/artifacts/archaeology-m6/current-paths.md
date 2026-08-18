# Current path proofs and preserved regressions

## Current V2 through-hole

```text
Firmament V2 Hole<Shaft>
  -> semantic hole construction
  -> ThroughHoleRecipeRequestBuilder
  -> ThroughHoleConstructionRecipe
  -> internal Surgery loop/face/shell builders
  -> BrepSurgeryValidation
  -> STEP AP242 export/reimport
```

`FirmamentV2SemanticHoleTests`, `RecognizedConstructionRecipeTests`, and the CLI build/inspect dogfood exercise this path. The semantic route emits `semantic AirHoleFeature -> ThroughHoleRecipeRequest -> ThroughHoleConstructionRecipe`; it does not create operands for `BrepBoolean` rediscovery.

## V1 compatibility

```text
explicit firmament.version = 1 file
  -> LegacyFirmamentV1SourceReader
  -> FirmamentCompiler / historical executor
  -> bounded BrepBoolean where retained
  -> STEP + one compatibility warning
```

`FirmamentV2DiagnosticRoutingBuildTests` proves valid V1 file compatibility and proves that the V2-only in-memory boundary rejects the same source.

## Generic server compatibility

```text
two stored external bodies + operation
  -> server Boolean endpoint
  -> BrepBoolean
  -> admitted recognized family OR typed NotImplemented rejection
```

`KernelApiIntegrationTests.UnsupportedBoolean_OnV1_ReturnsUnprocessableEntityWithDiagnosticEnvelope` proves the typed rejection contract.

## Permanent teaching evidence

- stepped root cause and history: `BrepBooleanCoaxialSubtractStackFamilyTests`, `BrepBooleanBoxCylinderHoleContinuationTests`, and `docs/brep-boolean-stack-a0...a4`
- overlap/tangency: Boolean hardening and Firmament safe-subtract graph tests
- rotated/conic limitations: rotated analytic Boolean regression tests and deferred-scope documentation
- mixed continuation: safe-composition mixed analytic/prismatic rejection tests
- generic CIR lesson: `GenericCirBrepExecutorLab` and `SteppedHoleExecutionArchitectureLab`
- legacy-vs-Recipe parity: `RecognizedConstructionRecipeTests` and `BrepSurgeryRecipeParityTests`

These are historical/educational contracts, not invitations to expand the central dispatcher.
