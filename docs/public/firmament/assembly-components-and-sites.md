# Components and mounting sites

Organize an assembly around complete components: a knob, selector or tuner owns
its children, local dimensions and published mounting frame. Pattern component
occurrences rather than separate categories of their constituent parts.

The guitar demonstrates this in directly authored
[`knob.firmament`](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/knob.firmament),
[`selector.firmament`](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/selector.firmament)
and [`tuner.firmament`](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/tuner.firmament)
modules. Its composition root remains
[`guitar.firmasm`](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm).
A component is an ordinary reusable subassembly:

```firmament
Subassembly GuitarTuner {
 <Assembly GuitarTuner>
  <Part TunerWasher = Drum<R:7mm,H:1.5mm>>
   Placement { From: BottomSeat.Frame; To: World; }
  </Part>
  <Part TunerPost = Drum<R:4mm,H:8mm>>
   Placement { From: BottomSeat.Frame; To: World; }
  </Part>
  <Part TunerButton = Panel<L:15mm,W:19mm,H:6mm,R:5mm>>
   Placement { From: BottomSeat.Frame; To: World; TranslateLocal: [-22mm,0mm,-7mm]; }
  </Part>
 </Assembly>
 Anchor: GuitarTuner;
 Expose { DatumFrame Mount = TunerWasher.BottomSeat.Frame; }
}
```

Here `Drum` and `Panel` publish `BottomSeat.Frame`. `Mount` is the component's
public frame. External placement uses `From: Mount`, while child placements are
solved once in the component's own local world. One occurrence placement moves
the whole component. AP242 export uses these solved definition-local child
transforms when emitting shared product usages. It does not recompute them from
each posed world occurrence: numerical roundoff in that recomputation could
otherwise produce duplicate component children after STEP reimport.
The washer and post are overlapping decorative placeholders;
this does not claim detailed tuner mechanics or washer bore manufacture.

## Rows and reflected mounting sites

Assembly sources now admit two finite site recipes:

```firmament
Linear Sites BassTunerSites {
 Keys: [Low, Middle, High];
 Start: [-27mm,58mm,0mm];
 Step: [0mm,36mm,0mm];
}
Mirrored Sites TrebleTunerSites From BassTunerSites { Across: YZ; }
```

`Keys` gives ordered semantic identities. For key ordinal `i`, the point is
`Start + i * Step`. `Start` and `Step` are finite three-component mm vectors;
`Step` must be nonzero. Each recipe admits 1–1024 unique identifier keys.
Changing pitch preserves occurrence identities. Reordering keys changes which
ordinal each key occupies, so it changes their positions deliberately.

`Mirrored Sites` preserves keys and reflects points across the local origin
plane `YZ`, `XZ` or `XY`. Its source must be an earlier site recipe; chains are
allowed. These are coordinate planes **in the consuming placement's target
frame**. This bounded spelling does not yet accept arbitrary named Concept
planes, transform geometry, infer orientation or manufacture reflected rigid
matrices. Unknown fields, duplicate fields, bad units, invalid keys, forward
references and nonfinite/overflowing coordinates fail at the authoring boundary.

Both recipes produce ordinary checked `Set` records with `X`, `Y`, `Z` Length
fields. Consume them through the existing keyed assembly pattern:

```firmament
Pattern BassRow Over BassTunerSites {
 site => <Assembly Tuner = GuitarTuner>
  Placement { From: Mount; To: World; TranslateLocal: [site.X,site.Y,site.Z]; }
 </Assembly>
}
Pattern TrebleRow Over TrebleTunerSites {
 site => <Assembly Tuner = GuitarTuner>
  Placement { From: Mount; To: World; TranslateLocal: [site.X,site.Y,site.Z];
              RotateLocal: { Axis: Z; Angle: 180deg } }
 </Assembly>
}
```

The explicit 180deg clocking turns the same tuner's button outward. Both rows
keep their mounting normal and proper right-handed frames. Actual handed hardware
can use separate component definitions. No negative scale reaches occurrence
placement. Existing pattern limits and the prohibition on nested pattern bodies
remain in force; patterns may instantiate reusable subassemblies.

Inspection retains ordinary expanded keyed paths, shared geometry definitions,
row values and associations. Patterns derived from site recipes also carry
`siteRecipe`, the ordered authored derivation chain. Existing source offsets
refer to expanded source; per-file source-map reconstruction remains separate.

## Seating the guitar's entire tuner group

Inside the neck assembly:

```firmament
<Assembly Tuners = GuitarTuners>
 Placement { From: Origin; To: GuitarNeck.Headstock.Front.Frame;
             TranslateLocal: [0mm,0mm,1mm]; }
</Assembly>
```

`Headstock.Front.Frame` is a definition-owned mathematical datum published at
the 14mm headstock front. The 1mm offset seats the washers on the veneer top.
Row spacing and component orientation are headstock-local, so the compiler
composes the tilt once. No general non-planar surface projection is implied.

The four knobs use an explicit keyed `KnobSeats` Set named `NeckVolume`,
`BridgeVolume`, `NeckTone`, `BridgeTone`. Their distinct seat heights on the
carved body remain visible engineering data. They share `ControlKnob`; the
selector is one `PickupSelector` occurrence. Internal component offsets occur
only in their defining modules.

The guitar generator preserves these directly authored modules. It still computes
fret spacing and WireForm routes; string endpoints follow the corrected tuner
post seats. The nut bend radius is 4mm, with matching tangent setbacks, so the
revised leads pass the existing WireForm clearance checks at every string gauge.
Those route calculations are not an assembly placement authority.

Ground truth:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm inspect fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm --json --profile
```
