# Inventory module qualification

The four module kinds use one AST-owned dependency graph. Locators are relative
to the declaring document. `machine.firmasm` composes Parts and a Subassembly;
`factory.firmament` composes that Assembly, a nested Scene and another Part.
The two Bracket exports belong to separate files and retain separate schemas.
`include-simple.firmament` retains the short, unqualified include form.
`missing-member.invalid.firmament` must reject without producing geometry.

The component tests also qualify STEP through an explicitly generated asymmetric
Part artifact, source snapshots, ambiguity, cycles, private scopes, cache reuse,
source locations and published ports through the ordinary Mate solver.

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Regression/Inventory/machine.firmasm --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm export-ap242 fixtures/Regression/Inventory/machine.firmasm --out artifacts/local/firmament-inventory/machine.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll scene inspect fixtures/Regression/Inventory/factory.firmament --json
```
