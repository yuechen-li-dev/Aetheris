# Bounded material offsets

`AddOffset` and `RemoveOffset` describe a bounded local material change using a closed semantic tool family. They are the geometric fallback between named engineering features and advanced Sculpt operations; they are not arbitrary public Boolean union or subtraction.

Use a stronger noun when one exists: use `Hole` for a hole, `Pocket` for an enclosed finite pocket, `Boss` for a boss, and `Slot` for a slot. Use an Offset for a bounded local pad, groove, channel, or edge notch that lacks that stronger engineering meaning. Use Sculpt when explicit topology surgery and preservation contracts are required.

## Qualified Prism lane

```firmament
Compose Plate {
    Base StockBody { Profile: StockProfile; From: 0mm; To: 10mm; Role: Stock }

    RemoveOffset<Prism> EdgeGroove {
        Target: Plate
        On: Top
        Profile: GrooveProfile
        Depth: 4mm
    }
}
```

`Prism` consumes an existing closed, nonzero-area Profile made from bounded line and circular-arc segments. Full-circle and ellipse primitives are rejected until the section-arrangement authority admits them; a circle can be authored as two bounded semicircular arcs when needed. The current bounded lane uses the Compose `+Z` frame. Add requires a positive `Height`. Remove requires exactly one of a positive `Depth` or `Termination: ThroughAll`.

A removal profile on the whole `Top` support may cross the target silhouette; this is how edge grooves and notches are expressed. The tool must still remove positive material, leave one connected nonempty body, and produce valid exact topology. On a bounded `Span<Plane>`, the full footprint must remain inside the span because the span is an authored support contract.

The compiler retains target, support, profile, direction, termination, authorized bounds, material effect, and stable identity before lowering through the existing exact section-stack construction path. Zero dimensions, missing targets, non-intersection, disconnected addition, splitting removal, and complete removal fail without emitting a body.

Cylinder and Sphere offsets are not qualified yet. In particular, arbitrary-axis removal against curved targets and exact spherical-cap addition require new bounded analytic topology builders. The compiler rejects those tool families rather than exposing or guessing a general Boolean operation.

Do not confuse material offsets with Sculpt `OffsetRegion`: `OffsetRegion` displaces an existing surface region; `AddOffset` and `RemoveOffset` change material using a bounded semantic tool.
