# Format routing

- V1 TOON: `FirmamentV1ToonReader` -> `FirmamentParsedDocument` -> retained V1 validation/lowering/execution.
- V1 JSON: `FirmamentV1JsonReader` -> the same normalized V1 model.
- Historical auto-detection: `LegacyFirmamentV1SourceReader`; JSON is selected only when it is valid JSON, otherwise TOON is decoded.
- Legacy JSON `.firmasm`: `LegacyFirmasmJsonReader` -> flat manifest/load bridge -> deterministic V2 Assembly migration with `LegacyExplicit` placement.
- Current V2 `.firmasm`: `FirmamentAssemblyDocumentCompiler` -> current Assembly parser. JSON shape alone selects the named legacy reader.

The legacy readers preserve data and diagnostics; they do not assign V2 engineering meaning.
