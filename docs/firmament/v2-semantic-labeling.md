# Firmament V2 semantic STEP labeling

The canonical language supports imported canonical STEP, human-readable face IDs,
recognition, and the bounded through-hole replacement route.

```firmament
Model LabeledPart {
    Units: mm
    InlineStep Source { Path: "part.step" }

    Recognize Source {
        Region MountHole {
            Kind: HoleShaft
            Confidence: High
            Faces: [7]
            Evidence: { SurfaceFamily: Cylindrical Radius: 4mm Through: true }
        }
    }

    Replace Source.MountHole With Hole<Shaft> MountHole {
        On: Source.Face(7)
        Center: Point2(0mm, 0mm)
        Diameter: 8mm
        End: ThroughAll
        HostSize: [80mm, 50mm, 25mm]
    }
}
```

Run `aetheris analyze part.step --face 7 --json` to inspect a face before
authoring. `Faces` and `Source.Face(n)` use the sequential analysis-facing ID;
the parser resolves it to the imported STEP `ADVANCED_FACE` entity through the
InlineStep topology map. For traceability only, `StepFaceEntities: ["#191"]`
is also accepted in a `Region`; do not mix it with `Faces`.

Currently admitted recognition kinds are `HoleShaft` and `DatumPlane`. The
replacement materializer is intentionally limited to verified `Hole<Shaft>`
through-all rebuilds and requires `On`, `Center`, `Diameter`, and `HostSize`.
The lowercase `solid … : InlineStep`, `recognize`, and `replace` forms remain
compatibility syntax and normalize to the same V2 records.
