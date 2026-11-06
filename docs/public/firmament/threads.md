# Bounded external threads

The external `Thread` feature cuts a finite groove into continuous major-diameter cylindrical stock. It supports the existing metric 60-degree truncated profile on a Cylinder or standalone HexBolt shank. The helical law, certified spline realization, and seam topology remain BRep authority.

`StartOffset` positions the groove centerline relative to the exposed cylindrical support start. `Length` sets its finite axial span; a thread that ends inside the stock admits 1–40 complete turns. The groove's outer footprint extends `7 * Pitch / 16` beyond each centerline endpoint. Leave stock outside that footprint at the start. A HexBolt's support starts after `UnderHeadRadius`; a groove that would reach the head produces `thread-support-bounds`. On a Cylinder the footprint must also end inside the stock.

On a HexBolt, a thread whose footprint reaches the tip chamfer runs out through the tip, as a die-cut thread does, so a nut can start on it. The groove continues until it leaves the material: the chamfer cone carries the helical notch the groove cuts into it, and the flat end face stays whole. The end of `Length` then only needs to reach the chamfer; the turn count reported is the number the groove makes before it clears the tip. The tip diameter must lie inside the thread's minor diameter.

The canonical Preview 4 witness is [hexbolt-showcase.firmament](../../../fixtures/Thread/hexbolt-showcase.firmament). Its 52 mm stock is threaded 47.5 mm from the tip, so the groove runs out through the 45-degree tip chamfer after 38 turns and leaves 3.753125 mm of head-side land. No tapered runout under the head is modeled.

```firmament
Thread MainThread {
    Surface: face(Bolt.Shank)
    MajorDiameter: 8mm
    Pitch: 1.25mm
    Length: 47.5mm
    StartOffset: 4.3mm
}
```

A standalone HexBolt may author `MakerMark`, `MakerMarkHeight`, and `MakerMarkDepth` together. Height and depth require explicit positive millimetres; the existing bounded planar cap engraving admits depth up to 0.3 mm. For example, `MakerMark: "CODEX"`, `MakerMarkHeight: 2.5mm`, `MakerMarkDepth: 0.2mm` reuse the established text profiles and counter handling. Reusable assembly engraving is explicitly unsupported. The old `ThreadedHexBoltMakerMark` C# entry point is deprecated.

Build through the repository CLI:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Thread/hexbolt-showcase.firmament --output artifacts/local/p4-01b/showcase/bolt-cli.step --json
```
