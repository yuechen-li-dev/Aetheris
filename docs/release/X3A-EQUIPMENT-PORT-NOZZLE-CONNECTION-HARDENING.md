# X3a — Equipment port and nozzle connection hardening

## Executive verdict

**Accepted.** The X3 flagship no longer uses an unexplained air gap between route endpoints and equipment proxies. Each owned port now materializes a hollow nozzle stub from the equipment face to the route endpoint, and a first-class mate joins the nozzle's pipe-side Interface to the endpoint pipe Interface.

The change is deliberately bounded. It adds no flange catalog, equipment CAD library, penetration system, or generalized collision exception language.

## Semantic seam

```text
Equipment → owning KeepOut
→ owned Port (pipe-side mating position + outward direction)
→ hollow NozzleStub (equipment face → port)
→ coincident/opposed PipingMate
→ endpoint PipeSegment
```

`Equipment` names its conservative proxy `KeepOut`. An owned `Port` names that equipment and a positive `NozzleLength`. Its derived nozzle root must lie on the owner's outward AABB face. The port's existing stable Interface is the nozzle's `PipeMate`; the nozzle also exposes an `EquipmentMate` at its root. The endpoint pipe segment exposes a distinct Interface at the same point with the opposite outward direction.

## Scoped clearance exemption

The former 50 mm visual gap existed because every route component was checked against every equipment KeepOut. X3a does not weaken that rule globally.

For each owned port, the compiler records exactly one `PipingKeepOutExemptionIr`:

```text
Port: <target port>
Nozzle: <that port's nozzle only>
KeepOut: <that port's owning equipment only>
Scope: NozzleEnvelopeOnly
```

The accepted route continues to check every KeepOut, including its endpoint equipment. The nozzle checks every foreign KeepOut at full required clearance. Only nozzle-to-owning-equipment contact is exempt, allowing the stub root to touch the proxy face. Regression fixtures reject an off-face nozzle root and a nozzle that crosses a foreign bracket even though its route remains clear.

## Physical realization

Nozzles use the same qualified hollow analytic pipe construction as straight route segments. They remain distinct `Standard.Piping.NozzleStub` semantic/AP242 occurrences and BOM components. Their root annulus touches the equipment proxy face; their tip annulus is coincident with the endpoint pipe annulus. No BRep topology editing or fusing is used as semantic authority.

## Flagship evidence

Source: `fixtures/Canonical/Piping/pump-skid.firmament`

Generation command:

```powershell
dotnet run --project Aetheris.CLI -c Release -- build fixtures/Canonical/Piping/pump-skid.firmament --output artifacts/local/x3a/x3a-pump-skid.step --json
```

| Field | Result |
|---|---:|
| logical connections / routes | 2 / 2 |
| pipe segments / Elbow90 fittings | 12 / 10 |
| equipment / owned nozzles / endpoint mates | 2 / 4 / 4 |
| scoped KeepOut exemptions | 4, all `NozzleEnvelopeOnly` |
| AP242 bodies | 30 (22 route components + 4 nozzles + 4 proxies) |
| analytic cylinders / tori / planes | 32 / 20 / 76 |
| verified minimum non-exempt clearance | 37.499 mm conservative lower bound (30 mm required) |
| bounds | `[0,-112.5,0] → [1250,662.5,800]` mm |
| components enclosed | true |
| AP242 reimport | succeeded |
| STEP SHA-256 | `f515741168825ff90052a7ac76b4e21cd87f4246cd24b2aa9ec4dbdb76d52510` |

The routing JSON now reports equipment, owned ports, nozzle root/tip positions, endpoint mates, and scoped exemptions. The BOM separately groups four `Standard.Piping.NozzleStub` components; nozzle stubs are not added to the pipe Cut List.

## Qualification

Focused X3/X3a tests cover ownership, on-face roots, physical length, coincident/opposed mates, exemption scope, own-owner contact, full foreign clearance, invalid foreign collision, AP242 occurrence/reimport, and all prior routing/locality invariants. Qualification passed with 21 focused tests, all 87 canonical fixtures, a zero-warning Release build, all 3,068 discovered .NET tests, 82 client tests plus build/lint, 13 VS Code extension tests plus typecheck/build/package, a fresh packaged CLI, fresh win-x64 NativeAOT Forge `list`/`describe`/`invoke`, the repository layout guard, Markdown link scan, and `git diff --check`.

## Manual inspection

Open `artifacts/local/x3a/x3a-pump-skid.step` and inspect:

- both pump nozzle roots touching the pump proxy face;
- both cooler nozzle roots touching the cooler proxy face;
- no gap or overlap at each nozzle-to-pipe mate;
- unchanged frame clearance and route shapes;
- elbow orientation and general skid plausibility.
