# Sheet Metal M7: semantic assembly datums and tolerance-aware fit

## M6 diagnosis and retained regression

The original source remains at `fixtures/FirmamentV2/SheetMetal/m6-network-appliance-product.firmasm`. Its Body and Lid parts are valid, but the `Closure` semantic repeats nominal coordinates independently of the formed geometry:

```firmament
Axis Axis = [80,55,36] -> [0,0,1];
Plane Seat = [80,55,36] normal [0,0,1];
```

The Interface aligns the axis and plane while admitting rotation about the axis. This derives `(-1,-1,36) mm`, but the exact formed Body reaches `Z=41.4 mm`. The relationship is therefore detached from the manufactured closure geometry by 5.4 mm and is not registered in-plane. The M7 regression test reproduces both numbers; it does not tune the old transform.

## Generic datum architecture

`DatumFrameCapability` and `ExactDatumFrameBinding` are generic semantic primitives in `Aetheris.Semantics`. A frame stores an origin and right-handed X/Y/Z basis in definition-local coordinates. Its identity is authored and stable; it is not a BRep face ID. `AssemblyDatumIr` publishes the stable semantic path and binding ID. `DatumMateSolutionIr` records orientation, constrained DOF, status, and the derived transform.

The parser-level syntax is:

```firmament
Semantic Datums {
  DatumFrame LidSeat = [80,55,41.4]
    x [1,0,0] y [0,1,0] z [0,0,1];
}
```

The same syntax is tested by the non-Sheet-Metal `Plate` + `Bracket` fixture in `AssemblyM7DatumTests`. A registered frame constrains 6/6 rigid-body DOF. Existing point, axis, and plane datums remain useful for intentionally partial constraints; the existing underconstraint diagnostics still report their free translations and rotations.

## Canonical M7 source and Mate

The passing source is `fixtures/FirmamentV2/SheetMetal/m7-network-appliance-product.firmasm`. Its closure is:

```firmament
Interface LidClosure {
  Role Body requires DatumFrameCapable, DimensionalCapable;
  Role Lid requires DatumFrameCapable, DimensionalCapable;
  Lower FrameCoincident Lid.BodySeat Body.LidSeat OpposedDirection;
  Fit Body.Width inside Lid.Width per-side;
  ClearancePolicy Minimum 0.5mm Maximum 1.5mm;
  Variation Linear 0.1mm Thickness 0.08mm BendAngle 0.5deg
    BendLocation 0.1mm Coating 0mm CoatingTolerance 0mm Engagement 9mm;
}

Mate Closure: LidClosure {
  Body: ElectronicsEnclosureProduct.Body.Datums;
  Lid: ElectronicsEnclosureProduct.Lid.Datums;
}
```

The stable datum paths are:

- `NetworkAppliance.Product.Body.Datums.LidSeat`
- `NetworkAppliance.Product.Lid.Datums.BodySeat`
- `NetworkAppliance.Product.ClosureFrame`

The Body frame points outward along +Z. The Lid frame points outward along -Z. `OpposedDirection` is explicit and never inferred from role order. Tangent registration fixes in-plane translation and rotation. The solver derives `(-1,-1,41.4) mm`; no `LegacyExplicit` placement exists.

## Nominal and tolerance-aware fit

`InterfaceFitResultIr` separates evidence from classification. `per-side` converts the 2 mm width difference to 1 mm side clearance. Manufacturing variation is parsed into the Interface and evaluated by the generic Assembly compiler; `MakeEnclosureProduct(spec)` exposes that IR result rather than running a second fit engine.

For the canonical NetworkAppliance:

| Evidence | Result |
|---|---:|
| nominal minimum separation | 1.0000 mm |
| minimum possible separation | 0.382916 mm |
| maximum possible separation | 1.617084 mm |
| maximum penetration | 0 mm |
| nominal state | `GuaranteedClearance` |
| variation state | `GuaranteedClearance` |

Worst-case clearance reductions are ordered deterministically:

- bend location: 0.200000 mm;
- sheet thickness: 0.160000 mm;
- bend angle (`2 × 9 mm × tan(0.5°)`): 0.157084 mm;
- linear dimension: 0.100000 mm.

Coating uses two fit surfaces. A 0.25 ± 0.05 mm coating changes the same nominal Body/Lid STEP hashes from `GuaranteedClearance` to `PossibleInterference`. A sufficiently thick coating fixture reaches `GuaranteedInterference`. Nominal geometry is never perturbed.

An engineering noise floor of 1e-6 mm prevents microscopic numerical residue from becoming an interference verdict. The tolerance values remain engineering allowances, separate from geometry-query epsilon.

## Contact, overlap, and query boundary

The registered frame certifies the intended seating relationship. Contact area remains `null` because the current body/body query surface cannot certify finite face area. Exact nominal positive-volume overlap is checked by the existing `BrepSolidInterference` closed-body predicate, which reports penetration and a contained-tetrahedron witness. A non-interfering result establishes a zero certified overlap lower bound, not an exact arbitrary-BRep intersection volume.

`ClosestPointQuery`, `ContactQuery`, and `IntersectionQuery` retain their curve/patch evidence contracts and topology firewall. M7 does not pretend those APIs perform body/body clearance. The enclosure fit interval is an analytic, conservative semantic-dimension proof; witnesses never become topology authority. General Boolean and CIR architectures are unchanged.

## Diagnostics and product API

Regression coverage includes:

- the old M6 detached relationship;
- missing/wrong datum member (`assembly-mate-invalid-participant`);
- axis/plane underconstraint (`assembly-placement-underconstrained`);
- conflicting frame constraints (`assembly-placement-overconstrained`);
- generic non-Sheet-Metal frame mating;
- guaranteed clearance, possible interference, and guaranteed interference;
- coating changing fit while nominal STEP hashes remain identical.

`ManufacturedEnclosureProduct` now returns Assembly datums, resolved Datum Mate IR, `EnclosureFitEvidence`, tolerance contributors, product DFM, and normal Body/Lid manufacturing results. Export adds `fit-report.json` beside formed/flat STEP, flat SVG, Assembly STEP, and `product-dfm.json`.

CLI `asm inspect` prints stable datums and concise Datum Mate solutions. The canonical evidence is under `artifacts/m7`, including before/after inspections and the complete manufacturing package.

## AP242 assembly interoperability

The Assembly STEP exporter follows the complex occurrence relationship emitted by
the repository's OCCT `as1.step` reference. Each child-to-parent relationship
combines the base representation relationship, its item-defined transform, and
the shape-representation facet. A plain transformed relationship is insufficient:
OCCT can recover its product tree while discarding the occurrence placement.

The regenerated `artifacts/m7/NetworkAppliance.step` was imported independently
with FreeCAD 1.0.2 / OCCT. It recovered the nested
`NetworkAppliance -> ElectronicsEnclosureProduct -> Body, Lid` hierarchy. The
Body world bounds are `Z=0..41.4 mm`; the Lid occurrence placement is
`(-1,-1,41.4) mm` and its world bounds are `Z=30.9..42.6 mm`. This confirms the
lid overlaps the enclosure walls from above by its authored skirt depth rather
than appearing below the box.

## Performance and certification boundary

On the canonical fixture, local datum solve plus Assembly analysis measured about 35 ms in the recorded run; ordering, IDs, transforms, classifications, contributors, and geometry hashes are deterministic. The ordinary CLI path materializes two exact definitions and two occurrences.

The largest remaining blocker to production fit certification is certified body/body minimum-distance/contact-area/overlap evidence over the full formed BRep, correlated to real process capability data. M7 provides honest analytic enclosure envelopes and exact positive-volume rejection, but it does not claim general surface-pair certification, GD&T, springback, or coating physics.
