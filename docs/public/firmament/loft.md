# Two-section loft

`Loft` makes a capped solid between two named 2D outlines on independently framed construction planes. `Loft<Hollow>` derives inner outlines from a positive nominal thickness to make a thin-wall solid. The admitted outlines are a `Circle2` or `Ellipse2` used as the sole outer loop of a named `Profile`. Both forms lower to the existing G0 `SectionChain` ruled materializer, pcurve validator, BRep, and AP242 exporter; there is no second loft kernel.

```firmament
schema SectionChain
Model TwistedShell {
    Units: mm
    Concept Struct Stations {
        Lower: Plane { Origin: [0mm, 0mm, 0mm]; Normal: [0, 0, 1]; Up: [0, 1, 0] }
        Upper: Plane { Origin: [0mm, 0mm, 50mm]; Normal: [0, 0, 1]; Up: [0, 1, 0] }
    }
    Construction Plane LowerFrame { Trace: Stations.Lower }
    Construction Plane UpperFrame { Trace: Stations.Upper }
    Circle2 LowerCircle { Center: [0mm, 0mm] Radius: 25mm }
    Circle2 UpperCircle { Center: [0mm, 0mm] Radius: 25mm }
    Profile LowerOutline { Loop Outer { LowerCircle |> TraceLoop } }
    Profile UpperOutline { Loop Outer { UpperCircle |> TraceLoop } }

    Loft<Hollow> Shell {
        RearProfile: LowerOutline
        RearFrame: LowerFrame
        FrontProfile: UpperOutline
        FrontFrame: UpperFrame
        Thickness: 1mm
        Rule: Ruled
        Correspondence: CommonRay
        Reference: [1, 0, 0]
        Twist: 63deg
    }
}
```

For a filled solid, replace `Loft<Hollow> Shell` with `Loft Shell` and omit `Thickness` and `SeamGap`. The [solid witness](../../../fixtures/Canonical/SectionChain/solid-loft-common-ray.firmament) lofts a circle to a rotated ellipse and exports six closed faces.

`Rule: Ruled` makes each generator a straight segment between the sections. `Correspondence: CommonRay` pairs outline points by polar ray from each outline's center, projected from the world-space `Reference` direction into each section plane. This makes the pairing independent of how an ellipse's axes happen to be rotated or where its native parameter starts. `Twist` rotates the **front correspondence** relative to that reference; it does not rotate or move the front outline. It is available in both forms, defaults to `0deg`, and admits `-180deg..180deg`. Equal coaxial circular sections with `Twist: 63deg` form a ruled hyperboloid-like shell: for two radius-25 mm circles, the mid-height generator radius is `25 cos(31.5°) ≈ 21.316 mm`.

The materializer currently admits one outer loop per section, so the generated wall uses a narrow topological seam. `SeamGap` defaults to `0.1deg` and accepts `0.01deg..5deg`. The slit is explicit; it is not silently sewn shut. The major/minor axes are each reduced by `Thickness` at the inner ellipse, so thickness is nominal around an ellipse rather than an exact normal offset.

The [lamp shade](../../../fixtures/Canonical/ThreeDm/lamp-shade-loft.firmament) is the `Twist: 0deg` circle-to-tilted-ellipse case. The [63° witness](../../../fixtures/Canonical/SectionChain/hollow-loft-twist.firmament) exercises the optional twist. Both use `aetheris build <file> --out artifacts/local/<name>.step --json`; `aetheris section-chain build` also reports the underlying correspondence, pcurves, topology, and STEP reimport. A loft source may be an assembly part via `LoftFile<"relative-source.firmament">`.

These are bounded two-section lofts. Arbitrary profile topology, open loft sheets, rails, multi-section global fairing, exact normal ellipse offset, and arbitrary twist laws remain unsupported.
