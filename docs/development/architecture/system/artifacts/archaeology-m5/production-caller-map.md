# Production Boolean caller map

Counts are direct call sites outside Core/tests/labs: **15 before M5; 12 after M5**. Caller classes fell from six to four.

| Caller | Class | Old path | M5 path / retention | Risk |
|---|---|---|---|---|
| `ThroughHoleRecoveryExecutor` | known construction | box + cylinder -> `Subtract` | semantic plan -> request builder -> through-hole Recipe | low; migrated with differential parity |
| `AirHoleSimpleShaftMaterializer` | canonical V2 known construction | semantic plan -> profile-stack emitter | face-local ThroughAll simple shaft -> through-hole Recipe | low; migrated with CLI STEP dogfood |
| `HoleRecoveryExecutor` through variant | known construction | delegates to above after bounded routing | delegated path is now direct Recipe | low |
| `HoleRecoveryExecutor` blind/counterbore/countersink/stepped | known bounded legacy family | one or repeated `Subtract` fallbacks; profile-stack route preferred where admitted | retained: no matching explicit Recipe in M5 | medium/high |
| `StandardLibraryReusableParts` cube/hole | known construction | primitives -> `Subtract` | semantic dimensions -> request builder -> Recipe | low; migrated |
| recognized CIR box/cylinder | import recovery + recognized construction | primitives -> `Subtract` | recognized parameters -> request builder -> Recipe | medium; migrated and preserves replay feature ID |
| recognized CIR box/box | bounded compatibility | primitives -> `Subtract` | retained; no explicit construction Recipe | medium |
| `FirmamentPrimitiveExecutor` | V1 compatibility and generic V2 primitive bridge | operation enum -> Boolean facade | retained to preserve format/execution behavior | high |
| server Boolean endpoint | external compatibility API | arbitrary body IDs + operation -> facade | retained and documented as bounded, not general | high |

`ClassifyBooleanCase` has no external production caller. Test and FrictionLab calls remain historical/experimental evidence and were not migrated for count reduction.
