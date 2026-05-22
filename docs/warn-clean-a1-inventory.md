# WARN-CLEAN-A1 broader-gate inventory (post Core nullable cleanup)

Date: 2026-05-06 (UTC)

## Scope

This milestone inventories remaining warnings/failures in the canonical gate after WARN-CLEAN-A0 made `Aetheris.Kernel.Core` warning-free for `net10.0` and `net8.0`.

Commands used:

- `dotnet --version`
- `dotnet --info`
- `./scripts/test-all.sh`

## Environment snapshot

- .NET SDK: `10.0.203`
- Also installed: SDK `8.0.420`
- Host runtime: `10.0.7`

No SDK blocker was observed.

## Canonical gate result

`./scripts/test-all.sh` completed successfully with **all tests passing** and **four remaining warnings**.

### Remaining warnings

1. Project: `Aetheris.Kernel.Core.Tests` (`net10.0`)
   - Code: `CS8602`
   - Location: `Brep/Boolean/BrepBooleanTests.cs:183`
   - Text: Dereference of a possibly null reference.
   - Category: nullable contract
   - Recommended fix type: local guard (or nullability-safe assertion pattern)

2. Project: `Aetheris.Kernel.Core.Tests` (`net10.0`)
   - Code: `CS8602`
   - Location: `Brep/Boolean/BrepBooleanTests.cs:1973`
   - Text: Dereference of a possibly null reference.
   - Category: nullable contract
   - Recommended fix type: local guard around `surface` before property access

3. Project: `Aetheris.Kernel.Core.Tests` (`net10.0`)
   - Code: `xUnit2031`
   - Location: `Brep/Queries/BrepCncManufacturabilitySchemaTests.cs:35`
   - Text: Do not use a Where clause to filter before calling Assert.Single.
   - Category: analyzer/style
   - Recommended fix type: test cleanup (use `Assert.Single(collection, predicate)`)

4. Project: `Aetheris.Kernel.Firmament.Tests` (`net10.0`)
   - Code: `xUnit2012`
   - Location: `Assembly/FirmasmAssemblyExecutorTests.cs:62`
   - Text: Do not use Assert.True() to check if a value exists in a collection. Use Assert.Contains instead.
   - Category: analyzer/style
   - Recommended fix type: test cleanup (`Assert.Contains` predicate)

## Remaining test failures

None in the canonical gate run.

## Classification buckets

- A. Must-fix baseline test failures: none
- B. Must-fix compiler warnings: two `CS8602` warnings in `Aetheris.Kernel.Core.Tests`
- C. Acceptable/temporary warnings: none recommended; analyzer warnings are low-risk and should be cleaned immediately
- D. Tooling/environment issues: none

## Recommended cleanup order

1. Clear `CS8602` warnings in `Aetheris.Kernel.Core.Tests` (`BrepBooleanTests` lines ~183 and ~1973).
2. Clear xUnit analyzer warnings in test projects (`xUnit2031`, then `xUnit2012`).
3. Re-run `./scripts/test-all.sh` to confirm warning-free broader gate.
4. Resume CIR-F1 once the warning inventory list is empty.

## Outcome

Outcome A: complete broader-gate inventory produced.
