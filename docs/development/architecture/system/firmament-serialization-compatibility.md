# Firmament serialization and compatibility formats

Firmament V2 is Aetheris's sole canonical engineering authoring language.

| Identity | Status | Entry point / policy |
|---|---|---|
| Firmament V2 `.firmament` | Current authoring | `FirmamentV2Parser` |
| Firmament V2 `.firmasm` Assembly profile | Current assembly authoring | `FirmamentAssemblyDocumentCompiler` |
| Firmament V1 TOON | Historical serialization and compatibility input | `FirmamentV1ToonReader`; canonical output: `FirmamentV1ToonWriter` |
| Firmament V1 JSON | Historical serialization and compatibility input | `FirmamentV1JsonReader`; read-only |
| Legacy JSON `.firmasm` | Deprecated transform-first compatibility serialization | `LegacyFirmasmJsonReader` |

`LegacyFirmamentV1SourceReader` preserves the old JSON-vs-TOON auto-detection route for V1 callers. It is a compatibility adapter, never V2 source semantics.

Legacy JSON `.firmasm` remains an explicit migration input to the V2 Assembly profile. Its flat occurrence transforms are preserved as `Placement LegacyExplicit`; migration must not invent Interfaces, Roles, Mates, hierarchy, or any other modern engineering semantics absent from the historical data.

There is no V1 JSON writer and no same-format legacy JSON `.firmasm` writer. The V1 canonical historical write form is TOON. Legacy `.firmasm` export remains the existing STEP/package compatibility route, not JSON round-trip serialization.
