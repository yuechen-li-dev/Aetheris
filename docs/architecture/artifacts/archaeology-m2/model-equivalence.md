# Model equivalence and deterministic serialization

The codec test uses equivalent V1 TOON and JSON documents and compares their normalized `FirmamentParsedDocument` fields (header, model, operation identity, family, and raw fields). The V1 TOON writer is read/write/read stable and emits LF only.

Representative V1 TOON fixture evidence:

| Input | SHA-256 |
|---|---|
| `testdata/firmament/examples/box_basic.firmament` | `FF568A3C34D8F198035E3C2AD2F0C98A58BCD6D7BF41F2BAD4A82E06ACC8CB11` |
| generated STEP through compatibility build | `EA7B9303D850C3CBF985CD478D68CAF4B415C0B060E304ECA6A779F1E72635DC` |
| `testdata/firmasm/examples/occt-as1/as1-assembly.firmasm` | `9A96D379BD3E0326898EC37D6A8565C052419C322F6325C686FC38BF27518EAE` |

Diagnostics are explicit: invalid declared V1 JSON reports `Firmament V1 JSON compatibility input is not valid JSON`; accepted V1 builds carry `firmament-v1-compatibility-input`; legacy JSON `.firmasm` carries `legacy-firmasm-syntax`.
