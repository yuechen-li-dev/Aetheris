# PMI on native faces by their inspection selectors

## Change

Native PMI accepted only axis faces (`face(-Z)`), Compose caps (`face(op.Top)`), holes and hole walls. A face the
Inspector names, such as a side of an extruded profile, could not be annotated, and an extruded profile exported no
PMI at all. Helios's Label tab therefore refused native parts.

- **Parser.** A target `face(owner.Port)` or `face(owner.Port("arg"))` is admitted when `owner` is a body, hole or
  Compose operation of the part. The extrusion of a profile part is such an owner. An unknown owner rejects with
  `firmament-v2-pmi-target-unresolved`.
- **Export.** `BuildV2SemanticPmi` resolves these targets through the built body's correspondence: the faces whose
  `FirmamentSelector` matches, compared without whitespace outside quotes. They feed Datum (plane, or axis on a
  cylinder), every explicit geometric control, and `Distance`, which is now admitted on native faces with the
  imported rules (thickness or location, parallel planes only). A selector naming no built face fails through the
  existing `pmi-*-target-unresolved` export checks.
- **Profile extrusions** now build PMI. Every planar side face from one segment gets its selector (previously only
  faces that could host a hole), and the caps are `face(<extrude>.Bottom)` and `face(<extrude>.Top)`.
- **Box with holes**: the four walls are named `face(±X)`, `face(±Y)` when each is the single planar face bounding
  the part in that direction; previously only ±Z were named.

## Evidence

- `ConstructionFacePmiTests` (4): the `extruded-bracket-faces` fixture exports 2 datums, 3 distances (3, 20 and
  30 mm) and 3 controls, each attached to its faces; a missing segment and an unknown owner reject by name; a box
  with a hole names all six walls and exports datums and parallelism on them.
- CLI round trip on the showcase bracket: `aetheris analyze` reads every record back with its face associations.
- Helios Label e2e on the bracket and on a box plate with a hole (see the Helios report).

## Verdict

Success. Native faces with an Inspector selector take every record Label writes. Compose bodies whose
build lane has no display correspondence still have no selectors; that is the next gap.
