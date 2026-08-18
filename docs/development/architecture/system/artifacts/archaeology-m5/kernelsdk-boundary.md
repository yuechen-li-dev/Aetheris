# KernelSDK and Surgery boundary

Classification:

| Primitive | M5 classification | Reason |
|---|---|---|
| explicit edge use, strict loop, face, shell builders; validation | internal; plausible safe-advanced later | typed/deterministic, but public identity/provenance contract is immature |
| legacy-sense loop helper | not ready | intentionally bypasses the strict new closure convention for canonical parity |
| raw topology/store mutation or validation bypass | requires unsafe; not exposed | can construct invalid state |
| recognized Recipes | preferred future advanced surface | preserves intent above topology mechanics |

Nothing is exposed in M5. An architecture test proves Surgery types are non-public and Core does not friend-expose internals to Forge.Host or Forge.KernelSDK. The extension `UNSAFE` opt-in permits arbitrary in-process C# and is not a CLR sandbox or a BRep permission boundary. Forge.Host continues through Templates, recognized construction interfaces, Modules, and normal generation.
