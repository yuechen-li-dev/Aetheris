# StandardLibrary migration

`cube_with_cylindrical_hole` already owns fixed feature intent (20-unit cube, radius 3, through span 24). It now calls the typed request builder and `ThroughHoleConstructionRecipe` directly; StandardLibrary does not consume Surgery.

The differential test compares this result against the former primitive/Boolean path for topology/geometry/binding counts and canonical STEP hash, then exports and reimports STEP. The Recipe history now retains the standard-part feature ID; geometry and STEP remain identical.
