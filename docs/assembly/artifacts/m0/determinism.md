# Determinism proof

Two independent `aetheris asm inspect` compilations of `bearing-module.firmament` produced byte-identical canonical inspection JSON. Performance telemetry is opt-in (`--profile`) and is not embedded in canonical `AssemblyIr`.

Final SHA-256 proof:

```text
8C79ECC1928605A96EAA188418E3CE393834DE3576FFEA3B0791E9BCEBBA798B  assembly-ir.json
8C79ECC1928605A96EAA188418E3CE393834DE3576FFEA3B0791E9BCEBBA798B  repeat
1217C0A3B8E0E30B2A65A3096DAE28B09A7CD5E380903B672324A93581A39DEC  failing-assembly-ir.json
```
