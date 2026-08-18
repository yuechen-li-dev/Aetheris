# Firmament migration

The recovered through-hole path is now:

```text
Firmament V2 source / ConstructionIR
  -> CIR box-cylinder recognition and ThroughHoleRecoveryPlan
  -> typed ThroughHoleRecipeRequestBuilder
  -> ThroughHoleConstructionRecipe
  -> BRep Surgery
  -> validated BRep -> STEP AP242
```

`AirHoleSimpleShaftMaterializer` routes canonical face-local `ThroughAll` simple shafts directly to the Recipe and publishes that route in semantic topology provenance. `ThroughHoleRecoveryExecutor` likewise no longer creates a box/tool BRep or invokes `BrepBoolean`. `HoleRecoveryExecutor` delegates its legacy through fallback to this executor.

Blind, counterbore, countersink, chamfered-entry, and stepped legacy fallbacks remain bounded Boolean users because M5 does not introduce their missing Recipes. Firmament V1 primitive execution remains unchanged and opt-in compatibility tests cover its historical route.
