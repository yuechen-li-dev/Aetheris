# BUILD-TEST-A0 — net10-only target and legacy V1 test policy

## Why net10-only

Aetheris now uses `net10.0` as the only active .NET target. The previous `net8.0;net10.0` multi-targeting doubled normal build and test work while the project is concentrating on the V2 compiler architecture. Dropping `net8.0` keeps local and CI loops focused on the current runtime without changing geometry, compiler, BRep, STEP, or lowering behavior.

## Active architecture

- `Aetheris.Kernel.Core` is the LLVM-analogue kernel/backend substrate. Core geometry, BRep, STEP, recovery, and import/export tests remain active by default.
- `Aetheris.Kernel.Firmament` Firmament V2 is the Clang-analogue source frontend. Parser, AIR, lowering, region, artifact, and current compiler validation tests remain active by default.
- Firmament V1 remains valid historical TOON/YAML-ish structured syntax, but it is legacy/frozen. It is retained for compatibility, corpus, regression evidence, and reference.
- FrictionLab remains legacy/experimental unless a lab result is explicitly promoted into active V2/Core production-adjacent tests.

## Test-suite policy

Normal `dotnet test` runs active tests only:

```bash
dotnet test -f net10.0
```

The default project configuration excludes legacy Firmament V1 test source files from `Aetheris.Kernel.Firmament.Tests` and excludes the FrictionLab test project sources unless legacy tests are explicitly requested. This avoids relying on traits alone, which would still execute tests under plain `dotnet test`.

Legacy tests are opt-in with `AETHERIS_RUN_LEGACY_TESTS=1`:

```bash
AETHERIS_RUN_LEGACY_TESTS=1 dotnet test -f net10.0
```

For a narrower legacy run:

```bash
AETHERIS_RUN_LEGACY_TESTS=1 dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --filter FirmamentPrimitive
AETHERIS_RUN_LEGACY_TESTS=1 dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -f net10.0 --filter CIRLab
```

The helper scripts are:

```bash
./scripts/test-all.sh
./scripts/test-legacy.sh
```

`test-all.sh` is the active/default lane. `test-legacy.sh` exports `AETHERIS_RUN_LEGACY_TESTS=1` and runs the same `net10.0` framework lane with legacy sources included.

## Promoting useful legacy tests

When a Firmament V1 or FrictionLab scenario becomes relevant to current V2/Core work:

1. Copy or adapt the motivating assertion into an active V2/Core test file.
2. Keep the active test focused on the current architecture path: V2 parser/frontend, AIR, lowering, Core geometry, BRep, STEP, or artifact behavior.
3. Do not merely un-gate broad legacy suites to keep one useful assertion alive.
4. Leave the historical V1/FrictionLab test in place for deliberate legacy runs unless it is superseded by an explicit cleanup milestone.

## Non-goals

This policy change does not:

- change geometry behavior;
- change BRep topology behavior;
- change STEP import/export behavior;
- change Firmament V2 parser or lowering behavior;
- change AIR lowering behavior;
- change artifact emission behavior;
- change CIR behavior;
- redesign Firmasm;
- delete V1 tests or fixtures;
- mass-disable `Aetheris.Kernel.Core` tests.
