# Migrated recipes

| Family | Old topology path | M3 path | Known assumptions | Result |
|---|---|---|---|---|
| orthogonal union | private coedge/loop/face; direct shell; binding validator | Surgery legacy-sense loop + known-loop face; canonical T-junction shell; Surgery validation | occupied cells already determine oriented exterior rectangles | behavior tests and STEP/reimport pass |
| polygonal/prismatic through cut | private outer/inner loop and face helpers; direct shell | Surgery legacy-sense loop + face; strict Surgery shell; Surgery validation | corresponding outer/inner rings, reversed cavity walls, through span | behavior tests and STEP/reimport pass |
| cylinder-root rectangular open slot/keyway | private six face loops; direct shell | Surgery legacy-sense loop + face; strict Surgery shell; Surgery validation | retained cylinder arc, floor, radial walls, caps are known | behavior test and STEP export/reimport pass |

`BrepBooleanBoxCylinderHoleBuilder` remains the similar control family on its private topology path. Recognition, Judgment routing, `SafeBooleanComposition`, and family policy did not move.
