# Assembly authoring foundation

Implemented slice: readable rigid placement, named frames, definition-owned ports,
keyed finite occurrence patterns, and Fixed seating. These extend the existing
semantic binder, finite Record/Set frontend, exact materializer, and frame solver.
No user-defined Lower language or second placement solver is introduced.

## Readable placement

```firmament
<Part Base = Post<H:14mm>>
 Placement { From: Origin; To: World; TranslateLocal: [10mm,20mm,30mm];
             RotateLocal: { Axis: Z; Angle: 90deg } }
</Part>
FrameTransform Tilt {
 From: Demo.Base.Top.Frame;
 TranslateLocal: [0mm,0mm,2mm];
 RotateLocal: { Axis: X; Angle: 15deg }
}
<Part Child = Post<H:14mm>>
 Placement { From: Bottom.Frame; To: Tilt.Frame; }
</Part>
```

Place Parts inside the assembly tree and FrameTransform declarations in the
owning assembly. `Origin` is the current occurrence origin, `Parent` the immediate
parent origin, and `World` the current assembly root. Relative ports resolve on
the current occurrence, then its parent; qualified paths obey public boundaries.
Named transforms are referenced as `Name.Frame`.

Translations require finite literal mm; rotation requires X/Y/Z and finite deg.
Optional paired Normal/Up vectors are dimensionless and expressed in the target
frame. Normal supplies Z, projected Up supplies Y, and Y cross Z supplies X.
Zero/parallel vectors fail. Rotation applies in that constructed basis,
translation in the referenced frame, then the frame composes with its owner's
resolved transform. Translation is not rotated by RotateLocal.

AuthoredFrame placement lowers to compiler-owned exact frame constraints through
the existing solver, with no mechanical joint. Anchoring, explicit placement and
a moving Interface assignment cannot also drive authored placement. Unresolved
dependencies and cycles fail. LegacyExplicit/ImportedOccurrence remain supported.

## Definition-owned ports

```firmament
Template<H: Length> Struct Post {
 Circle2 Outline { Center: [0mm,0mm]; Radius: 4mm }
 Profile P { Loop Outer { Outline |> TraceLoop } }
 Extrude Body { Profile: P; From: 0mm; To: H }
 Expose {
  Semantic Bottom { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }
  Semantic Top { DatumFrame Frame = [0mm,0mm,H] x [1,0,0] y [0,1,0] z [0,0,1]; }
 }
}
```

Ports specialize through the geometry's typed template frontend. Occurrences
share exact definitions. Publication creates no new body and infers no arbitrary
BRep face name. Existing DatumFrame/Axis/Plane/Point/Dimension members are admitted;
malformed and duplicate members fail.

SectionChain-file Models can publish their authored section frames:

```firmament
Expose { Semantic NutMount { DatumFrame Frame = Neck.Section.S5.Frame; } }
```

The chain and section must exist. This aliases the section's exact frame rather
than a tessellation sample. Concrete authored part ports are now published before
M0 reusable local solving, through typed template specialization and the existing
SectionChain binder without body materialization. M1 still owns exact geometry.
See [the multi-file guitar](../demos/guitar-subassemblies.md) for the motivating case.

## Keyed finite patterns

```firmament
Record Site { X: Length }
Static Sites: Set<Site> {
 Left => Site { X: -5mm }
 Right => Site { X: 5mm }
}
Assembly Demo {
 <Assembly Demo>
  Pattern Posts Over Sites {
   site => <Part Post = Post<H:14mm>>
    Placement { From: Origin; To: World; TranslateLocal: [site.X,0mm,0mm]; }
   </Part>
  }
 </Assembly>
 Anchor: Demo;
}
```

The existing checked Set/Record frontend binds fields and units. Patterns yield
ordinary Parts/Assemblies, not a public text macro facility. Paths include pattern,
key and member: `Demo.Posts.Left.Post`. Reordering rows preserves IDs/placements.
Inspection retains expanded occurrences, associations, keys, values and source-row
provenance; geometry definitions remain shared.

Limits: 1024 keys per pattern, 4096 expanded keys per source, unique pattern names,
no nested assembly patterns or dynamic collections. Group-node identity placement
is compiler-owned. Independent arbitrary Mate-pattern bodies are outside this slice.

## Fixed seating

```firmament
Interface<Fixed> Seat {
 A: Demo.Base.Top.Frame;
 B: Demo.Child.Bottom.Frame;
 Gap: 2mm;
 Clocking: 90deg;
 Orientation: OpposedDirection;
}
```

Gap moves the seated origin along A's positive Z; Clocking rotates about A's
positive Z. OpposedDirection also flips B by 180 degrees around local X.
SameDirection and zero Gap/Clocking are defaults. This remains one zero-DOF
relationship. Placement, world-space residual checks and zero-pose kinematics use
the same transform, including reversed anchoring.

Seating is Fixed-only. Roles must be declared, unique, complete, reachable and
capability-compatible; seating requires an exact frame binding. Bad units,
nonfinite values, duplicate fields and invalid orientation fail diagnostically.

## Guitar reassessment

The fixture has zero authored LegacyExplicit matrices. Neck NutMount/HeelMount
publish section ports; NutWorld/HeadTilt place the headstock; VeneerSeat seats the
veneer. Seven keyed patterns cover pickup seats/coils/poles, saddles and tuner
posts/washers/buttons. Hardware positions preserve the previous witness, with
107 visible parts, 55 shared definitions, six WireForms and four SectionChains.

Python still generates spline controls/sections, computes fret spacing, and
derives WireForm lengths, bend angles and bases from endpoints. Decorative
hardware still uses absolute coordinates where mounting ports are unauthored.
Profile-valued templates, section patterns and point-driven routes are the next
useful authoring slice, deferred pending review. No surfacing-kernel change is
needed to take that step. Appearance remains downstream Cycles shading.

Ground truth:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm inspect fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm --json --profile
./scripts/qualify-guitar-x0.ps1 -OutDir artifacts/local/guitar-foundation -Render -Viewport
```

Generated reports, STEP/USD and Cycles artifacts go under ignored artifacts/local.

## Qualification on 2026-09-30

Verdict: **success for this bounded foundation slice**. The motivating guitar
now uses the implemented constructs through M1, exact STEP export, shared USD
prototypes and downstream rendering. Authoring D has not been started.

- Release solution build: passed (existing WebAssembly interop warnings).
- Core fast lane: 1005 passed; serial full solution gate: 4034 passed.
- New authoring foundation cases: 22 passed, covering specialization, shared
  geometry, keyed reorder stability, local reusable scopes, seating/zero pose,
  unknown roles/members, malformed fields, cycles and placement authority conflicts.
- Fresh-process cold compilation: 9.653 s; same-process warm recompilation: 4.603 s.
  These include JIT/library initialization as applicable, exclude process startup
  and dotnet build, and use no persistent model cache.
- Display preparation: 0.470 s cold / 0.375 s warm; USD serialization: 0.079 / 0.077 s.
- All 55 definition position/normal/index buffers are exactly equal to the prior
  guitar export. All 107 visible transforms match within 0.000001 mm; maximum
  component delta is 0.000000496067, from authored coordinate rounding.
- usdchecker passes; external USD composition has no errors, with 55 shared
  prototypes and maximum world-transform error 1.23e-15.
- usdview: 36 orbits, identical before/after geometry hashes, zero stage changes,
  compiler calls and geometry rebuilds; mean orbit plus readback 20.49 ms.
  This is evidence for the standalone downstream viewer; it does not qualify
  Helios browser interaction.
- Blender Cycles qualification: five refreshed images and a saved scene; imported
  product topology remains unchanged. Hero rendering took 205.22 s on this run,
  versus 26.17 s in the prior artifact. That offline rendering slowdown is not
  diagnosed here and must not be represented as a compiler or viewport timing.

Evidence: `artifacts/local/guitar-x0/foundation-{build,fast,full}.log` and
`artifacts/local/guitar-foundation/` containing `timings.json`, `display.json`,
`baseline-comparison.json`, `usd-validation.json`, `viewport/viewer.json`,
`guitar.step`, `guitar.usda`, and the Cycles scene/images.

The guitar remains suitable as a presentation witness against the supplied Zoo
example. This qualification establishes Aetheris behavior and preserves its
visual geometry; it does not measure Zoo's build times or certify an independent
head-to-head performance ratio.
