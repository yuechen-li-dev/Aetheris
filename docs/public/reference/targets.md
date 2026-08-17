# Target and selector reference

Selectors are domain-specific because their semantic models are different.

| Domain | Typical verified form | Example | Used by |
|---|---|---|---|
| Native `Model` geometry | axis face or exposed semantic selector | `+Z`, `face(+Z)`, `Bracket.Outer` | Hole, EdgeFinish, PMI |
| `SheetMetal` | named semantic planar region/path | `On: Base`, `From: Base.Rear` | holes/cuts, flanges, patterns |
| Native FEA | body axis face | `beam.face(-X)` | Fixed, Force |
| Imported STEP / FEA | imported AP242 face identity | `body.face(#170)` | Fixed, Force, bounded PMI |
| Assembly | typed role, port, interface, or DatumFrame references | `Lid.Datums.BodySeat` | mates, fit/placement |

In a native `Model`, hole `On: +Z` selects an entry face while PMI `Target: face(+Z)` selects a datum plane; a named hole is the target of `HoleDiameter`. In Sheet Metal, `face(-Z)` is not a planar-region name and `Hole<Shaft>` is not the hole declaration form. Assembly `.firmasm` has a separate typed relationship model and does not publish Model face selectors as a universal convention.

Raw internal B-rep identifiers are not stable native selectors. `#170` is allowed only as identity from imported STEP, not as a native topology ID invented by an author.
