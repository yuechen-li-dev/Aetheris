# Mathematical Knot WireForms

`Knot` authors a named smooth closed mathematical curve as a constant-diameter WireForm tube. It describes a geometric sculpture, not the physical process of tying rope or bending and welding stock. Contact, friction, tightening, elastic deformation, and a closure manufacturing method are unspecified.

```firmament
WireForm Knot1 {
    Diameter: 6mm
    Material: Standard.Materials.StainlessSteel.304_Annealed
    StartFrame { Origin: [0mm,0mm,0mm]; Tangent: [1,0,0]; Up: [0,0,1] }
    Knot Sculpture {
        Family: Trefoil
        Scale: 20mm
        Handedness: RightHanded
        Phase: 0deg
    }
}
```

WIRE-X2 supports `Trefoil`, `FigureEight`, and bounded `TorusKnot` families. `StartFrame` is the rigid placement frame: its origin locates the canonical curve center and its tangent/up axes orient the canonical x/z axes. `Phase` moves the stable semantic seam around the same periodic family. `Handedness` mirrors the canonical z coordinate; it preserves explicit Trefoil chirality instead of asking an agent to infer it from geometry.

The exact parameterizations use `a = 2πt + Phase`, with periodic `t` and seam `t = 0`:

- Trefoil: `Scale × (sin(a)+2sin(2a), cos(a)-2cos(2a), -sin(3a))` for right-handed authoring; left-handed mirrors z.
- FigureEight: `Scale × ((2+cos(2a))cos(3a), (2+cos(2a))sin(3a), sin(4a))`. This is the mathematical figure-eight knot embedding, not a planar figure-eight curve.
- TorusKnot: `((R+r cos(Qa))cos(Pa), (R+r cos(Qa))sin(Pa), r sin(Qa))`, authored with `MajorRadius: R`, `MinorRadius: r`, and coprime integers `P,Q >= 2`.

`TorusKnot` rejects non-coprime P/Q because those parameters define a multi-component torus link. X2 does not silently label a link as a knot and does not yet materialize multi-component WireForms.

## Tube qualification

A mathematical knot can be a valid centerline while still being impossible to realize with thick wire. Aetheris checks both local curvature and nonlocal strand spacing before sweeping the tube. It reports a conservative sampled nonlocal separation, its approximate parameter pair, the minimum sampled local curvature radius, and:

```text
TubeRadiusLimit = min(MinimumNonlocalDistance / 2, MinimumLocalCurvatureRadius)
```

The requested wire radius must stay below that limit with numeric margin. Excess thickness fails with a typed diagnostic and produces no STEP. This is deterministic approximate tube-admissibility evidence, not a formal proof of geometric reach. Knot identity comes from the authored semantic family; Aetheris does not claim to reconstruct or prove knot type from sampled BRep geometry.

## Closed frames and representation

On a closed 3D path, transporting a section frame around one loop can accumulate rotation. Aetheris measures that closure rotation and distributes a deterministic correction so the swept wire closes without a twist seam. Inspection reports the raw mismatch, applied distributed correction, and final closure error. The method is rotation-minimizing parallel transport rather than a Frenet-only frame, so inflections do not introduce a Frenet flip.

The semantic curve is evaluable and periodic; control points are never authoring authority. Materialization uses stable-seam cubic non-rational B-spline centerline/tube patches, four polynomial quarter-section patches per span, shared closed topology, and face-local pcurves. There are no terminal caps, rational product surfaces, or faceted fallback. Ideal stock length is deterministic numerical integration of the semantic centerline; circular area times length gives volume, and catalog density gives mass. Forming strain and closure joining are not modeled.

Canonical sources are the [Trefoil](../../../fixtures/Canonical/WireForm/Knot/trefoil.firmament), [Figure Eight](../../../fixtures/Canonical/WireForm/Knot/figure-eight.firmament), and [(3,5) Torus Knot](../../../fixtures/Canonical/WireForm/Knot/torus-knot-3-5.firmament). `aetheris inspect source.firmament --json` exposes family identity, P/Q, component count, closure, length, clearance, frame, approximation, and stock evidence. `aetheris build` emits deterministic AP242 STEP.
