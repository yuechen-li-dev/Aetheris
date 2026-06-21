# HOLE-X4 — Firmament V2 semantic hole source hook

HOLE-X4 adds the smallest Firmament V2 authoring hook for production semantic holes. Source now declares semantic hole intent instead of raw cylinder/cone Boolean tools:

```firmament
modify base {
  hole<shaft> mount {
    on: face(+Z)
    center: [0, 0]
    diameter: 8
    end: throughAll
  }
}
```

Supported variants are exactly:

- `hole<shaft>`
- `hole<counterbore>`
- `hole<countersink>`

Supported placement is face-local on a stable planar face selector or face alias. The X4 materialization tests exercise `face(+Z)` / top-entry placement because the existing AirHole materializer is a rectangular top/bottom profile-stack lane.

Supported end conditions are `end: throughAll` and `end: depth <value>`.

Supported dimensions are explicit scalar fields:

- shaft: `diameter` / `shaftDiameter`, or radius aliases `radius` / `shaftRadius`;
- counterbore: `counterboreDiameter` or `counterboreRadius`, plus `counterboreDepth`;
- countersink: `countersinkDiameter` or `countersinkRadius`, plus `countersinkAngle` in degrees.

The parser stores these declarations as semantic hole AST nodes. Lowering converts those nodes to `AirHoleFeature`:

- `hole<shaft>` -> `AirHoleFeature.CreateSimpleShaft`;
- `hole<counterbore>` -> `AirHoleFeature.CreateCounterbore` with counterbore and shaft stack roles;
- `hole<countersink>` -> `AirHoleFeature.CreateCountersink` with countersink and shaft stack roles.

The parser does not expose raw cylinder/cone Boolean authoring for this path. Profile-stack/BRep objects remain downstream lowering furniture owned by the existing `AirHoleSimpleShaftMaterializer`; Firmament source and AIR preserve semantic hole intent.

Deferred by design: standards and fit libraries, M-size screw tables, threads/taps, drill tips, groups/patterns, up-to-face/up-to-next, arbitrary datum placement, non-planar entry faces, multi-body propagation, raw 3D Boolean source syntax, and STEP/DisplayIR/frontend behavior changes.

Validation commands used for this implementation:

```bash
dotnet build Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 /m:1
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj -f net10.0 --no-build --filter "FirmamentV2SemanticHole"
```
