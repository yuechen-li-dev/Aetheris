# Bounded external threads

The external `Thread` feature cuts a finite groove into continuous major-diameter cylindrical stock. It supports the existing metric 60-degree truncated profile on a Cylinder or standalone HexBolt shank. The helical law, certified spline realization, and seam topology remain BRep authority.

`StartOffset` positions the groove centerline relative to the exposed cylindrical support start. `Length` sets its finite axial span; the bounded implementation admits 1–40 complete turns. The groove's outer footprint extends `7 * Pitch / 16` beyond each centerline endpoint. Leave stock outside that footprint at both ends. A HexBolt's support starts after `UnderHeadRadius` and ends before `TipChamferLength`. Invalid margins produce `thread-support-bounds` rather than clipping a thread into the head or chamfer.

The canonical Preview 4 witness is [hexbolt-showcase.firmament](../../../fixtures/Thread/hexbolt-showcase.firmament). Its 52 mm stock preserves 38 turns at 1.25 mm pitch, with 0.953125 mm of head-side land and 1.315625 mm of tip-side land. No tapered manufacturing runout is modeled.

```firmament
Thread MainThread {
    Surface: face(Bolt.Shank)
    MajorDiameter: 8mm
    Pitch: 1.25mm
    Length: 47.5mm
    StartOffset: 1.5mm
}
```

A standalone HexBolt may author `MakerMark`, `MakerMarkHeight`, and `MakerMarkDepth` together. Height and depth require explicit positive millimetres; the existing bounded planar cap engraving admits depth up to 0.3 mm. For example, `MakerMark: "CODEX"`, `MakerMarkHeight: 2.5mm`, `MakerMarkDepth: 0.2mm` reuse the established text profiles and counter handling. Reusable assembly engraving is explicitly unsupported. The old `ThreadedHexBoltMakerMark` C# entry point is deprecated.

Build through the repository CLI:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Thread/hexbolt-showcase.firmament --output artifacts/local/p4-01b/showcase/bolt-cli.step --json
```
