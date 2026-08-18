# Compatibility and deprecation table

| Surface | Status | Replacement | Removal timing | Warning behavior |
|---|---|---|---|---|
| Firmament V2 `.firmament` | supported canonical authoring | none | no planned removal | none |
| Firmament V2 Assembly `.firmasm` | supported canonical assembly profile | none | no planned removal | none |
| V1 TOON reader | retained compatibility | none required for historical data | no planned removal | none on direct read |
| V1 JSON reader | retained compatibility | none required for historical data | no planned removal | none on direct read |
| V1 TOON writer | retained deterministic historical serialization | use V2 for new authoring | later, only with consumer evidence | none |
| V1 compile/execute/export | deprecated compatibility | author new work in V2 | later; no release date promised | one warning after successful file build |
| V1 auto reader adapter | retained compatibility facade | named TOON/JSON readers | later | XML documentation only |
| Legacy JSON `.firmasm` reader | retained compatibility | V2 Assembly `.firmasm` for new work | no planned reader removal | one concise legacy-format diagnostic |
| Legacy `.firmasm` direct execution/export | Preview compatibility | V2 Assembly profile | later, after real consumers migrate | existing concise diagnostic |
| `FirmasmManifestLoader` alias | deprecated-by-documentation adapter | `LegacyFirmasmJsonReader` | later | no `[Obsolete]` warning storm |
| `BrepBoolean` | bounded compatibility/generic facade | Recipes or higher-level semantic construction | no planned removal | typed rejection outside admitted families |
| recognized Recipes | internal preferred exact-construction layer | none | potential future advanced API | none |
| strict Surgery builders | internal | no public replacement yet | reconsider after identity/provenance contracts mature | inaccessible to public callers |
| legacy-sense Surgery seam | internal-only compatibility | strict builders for new representations | later if parity permits | none |
| raw topology mutation | unsafe, not exposed | bounded Recipe/Surgery | no exposure planned | inaccessible |
