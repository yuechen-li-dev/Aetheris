# Compatibility surface inventory

## Firmament V1

| Operation | Classification | Evidence |
|---|---|---|
| read TOON | `RETAIN` | `FirmamentV1ToonReader` |
| read JSON | `RETAIN` | `FirmamentV1JsonReader` |
| auto-detect old serialization | `RETAIN` compatibility adapter | `LegacyFirmamentV1SourceReader`; never V2 semantics |
| canonical TOON write | `RETAIN` | deterministic LF-only round-trip tests |
| compile/validate/execute/STEP export | `DEPRECATE`, retain for compatibility | file build requires decoded version `1`; opt-in V1 suite remains |
| in-memory/Forge execution | `REMOVE_NOW` accidental crossover | `CompileSource` is V2-only after M6 |

There is no V1 JSON writer and no new V1 semantics.

## Legacy JSON `.firmasm`

| Operation | Classification | Evidence |
|---|---|---|
| reader/loader | `RETAIN` | `LegacyFirmasmJsonReader` |
| loader alias | `REMOVE_LATER` | `FirmasmManifestLoader` remains documented compatibility only |
| inspect/display | `RETAIN` | current assembly compiler detects JSON only under `.firmasm` profile |
| direct execute/export | `DEPRECATE`, retain for Preview compatibility | `FirmasmAssemblyExecutor` and CLI `asm` flows |
| same-format JSON writer | absent | no compatibility promise |

Migration preserves flat rigid transforms as `Placement LegacyExplicit`. It does not manufacture hierarchy, Interfaces, Roles, or Mates. Current `.firmasm` remains the V2 Assembly document profile.

## Package/API state

V1 codecs and legacy firmasm compatibility types remain public in their existing packages to avoid an M6 breaking move. `BrepBoolean` remains public. Recipes and Surgery are internal; Forge.Host and KernelSDK have neither public access nor friend-assembly access to Surgery.
