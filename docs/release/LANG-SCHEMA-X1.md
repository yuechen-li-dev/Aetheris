# LANG-SCHEMA-X1 — Explicit Firmament frontend schemas

## Verdict

Accepted. Firmament source can now declare its parser authority with `schema <Name>`; generic build, inspect, and validate preserve that selection rather than guessing after an explicit declaration.

| Schema | Frontend | Semantic output | Generic routing |
| --- | --- | --- | --- |
| Mechanical | Firmament V2 | existing V2 document | default V2 route |
| WireForm | WireForm authoring | `WireFormFeatureAir` | build, inspect, validate |
| Sweep | Circular Sweep authoring | `CircularSweepFeatureAir` | build, inspect, validate |
| SectionChain | SectionChain authoring | `SectionChain` materialization | build, inspect, validate |

The historical V1 `schema:` mapping was manufacturing/validation metadata, not dialect selection. It remains untouched; the new header only recognizes `schema <Name>` at the first meaningful declaration.

Explicit unknown, duplicate, late, and source-mismatched declarations fail with `firmament-schema-*` diagnostics. No explicit schema falls through to another parser. Schema-free inputs retain the prior compatibility heuristics.

The earlier universal-V2 admission experiment identified parser islands rather than a need for a mega-parser. WireForm, Sweep, and SectionChain are now named frontend boundaries. Profile/edge-finish and blind-drill forms remain Mechanical materializer routes; Piping, Structural, PlasticShell, Sculpting, and compatibility V1 remain documented frontend-debt candidates because their command routes are distinct but were not broadened into new public schemas in X1.
