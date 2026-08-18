# CLI compatibility matrix

| Input | build | validate | inspect | asm | export |
|---|---|---|---|---|---|
| V2 `.firmament` | current | current | current | n/a | current |
| V2 `.firmasm` | profile-specific | n/a | `asm inspect` current | `asm inspect` / `export-ap242` | current AP242 |
| V1 TOON | compatibility fallback | V2-only (rejects) | V2-only (rejects) | n/a | compatibility STEP |
| V1 JSON | compatibility fallback | V2-only (rejects) | V2-only (rejects) | n/a | compatibility STEP |
| legacy JSON `.firmasm` | n/a | n/a | `asm inspect` migration | `asm exec` legacy direct path | `asm export` STEP/package; `export-ap242` via migration |

`asm exec` and `asm export` are retained legacy JSON `.firmasm` compatibility commands and surface one deprecation diagnostic. The extension itself remains current for V2 Assembly documents.
