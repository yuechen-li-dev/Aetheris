# AIR-V2B blind/counterbore interval production

AIR-V2B productionizes AIR-X2 findings from `docs/development/milestones/frictionlab/air-x2-blind-counterbore-interval-semantics-lab.md`:
- blind-hole AIR requires explicit solid/no-hole interval semantics;
- counterbore AIR can route as contiguous layered radii;
- legacy bounded blind/counterbore execution remains available as fallback.

## Production changes
- Added explicit AIR layer semantics with `AirProfileStackLayerKind` (`CircularCutInterval`, `SolidInterval`, `Unsupported`).
- Blind AIR model conversion emits explicit `solid + cut` contiguous intervals (never zero-radius holes), but production blind execution remains deferred to legacy fallback pending parity.
- Counterbore AIR conversion now accepts contiguous layered radii plans and routes through profile-stack executor.
- Executor now supports mixed cut/solid intervals by creating cylinders only for cut layers.

## AIR-V2B accepted shapes
- Through: one cut interval spanning host.
- Stepped: contiguous cut intervals (existing V2 behavior).
- Counterbore: contiguous two-layer cut profile with small through + larger entry tier.
- Blind: model supports contiguous two-layer profile containing one `SolidInterval` and one `CircularCutInterval`; production execution currently falls back to legacy bounded blind route.

## Fallback boundary
- AIR rejection does not remove legacy bounded routes.
- On AIR rejection, diagnostics report rejection and then legacy route evaluation.

## Diagnostics contract
- `hole-family AIR attempt: profile-stack route evaluation started.`
- accept examples:
  - `air-profile-stack-v2b-counterbore-contiguous-accepted`
  - `air-profile-stack-v2b-blind-solid-interval-recognized`
  - `air-profile-stack-v2b-blind-emitter-deferred`
  - `air-profile-stack-v2b-fallback-legacy-blind`
- reject examples:
  - `air-profile-stack-v2b-*-rejected-*`
- fallback marker:
  - `hole-family AIR attempt rejected; evaluating legacy fallback routes.`

## Test commands run
- `dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "AirProfileStack|ProfileStack|ThroughHoleRecovery|SteppedHole|BlindHole|Counterbore|HoleRecoveryPolicy|SemanticRecovery|FrepMaterializer|Rematerialize|FirmamentStepExporter"`
- `dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "AirProfileStack|ProfileStackExtrude|RecoveryPolicy|CIRLab"`

## Remaining limitations
- Conical families (countersink/chamfer) remain deferred to conical route.
- AIR remains bounded to cylindrical profile-stack semantics.
