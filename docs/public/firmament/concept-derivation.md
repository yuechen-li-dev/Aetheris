# Derived Concept frames and reusable boundaries

Concept values derive from authored semantic geometry and supply explicit placement
contracts to material consumers. They create no solids or product occurrences.
Profile, SectionChain and assembly placement keep their existing geometry owners.

## Published-frame-derived assembly planes

```firmament
Concept Struct Layout {
 Plane Deck { From: Test.Base.Top.Frame; Offset: 2mm; Clocking: 90deg }
 DatumFrame Mount { On: Deck; At: [4mm,0mm]; X: [1,0] }
}
```

The source is a published exact DatumFrame on an independently placed direct Part
in the owning assembly. Explicit transforms and Placement to World are admitted;
Placement may start at Origin or a published local frame. Offset moves along the
source normal; Clocking rotates its in-plane axes. Both default to zero and require
finite mm/deg values. A derived plane cannot also declare Origin/Normal/Up.
Same-scope Concept planes may derive from one another, with cycles rejected.

The guitar uses a definition-owned publication:

```firmament
Concept Struct GuitarLayout {
 Plane HardwareDeck {
  From: SectionChainFile<"CarvedMaple.firmament">.TopSeat.Frame
  Offset: 0mm
  Clocking: 0deg
 }
 DatumFrame BridgeSeat { On: HardwareDeck; At: [0mm,-35mm]; X: [1,0] }
}
```

SectionChainFile/LoftFile sources bind without materialization through the existing
publication binder. Definition frames import into the owning assembly's local
coordinates; they do not follow an occurrence's world transform. CarvedMaple
publishes TopSeat from its actual S4 section frame, so the 53mm height has one
authored owner. Existing Fixed seating and material-contact checks remain.

Derivation from the occurrence being placed by the datum is rejected. Arbitrary
references through moved/private subassemblies, mate-dependent source placement,
tangent-surface evaluation and directional projection remain outside this lane.
An offset datum is design geometry; it does not itself prove contact with its source.

## Reusable named profile boundaries

```firmament
Include "body-outline.firmament";
Model CarvedMaple {
 Units: mm
 Concept Struct SectionLayout {
  Curve2 Outline {
   From: BodyOutline
   On: XY
   Translate: [0mm,0mm]
   Rotate: 0deg
   Scale: 0.72
   Pivot: [0mm,-25mm]
  }
 }
 Profile Crown Using SectionLayout { Loop Outer { Outline |> TraceLoop } }
 // Crown can now be consumed by an ordinary Extrude or SectionChain.
}
```

From names an existing single-loop Profile value, optionally supplied by a shared
source module. It never selects an unnamed BRep edge or walks a material part's
private geometry. On is XY or an authored Concept plane, qualified when necessary.
Curve references can also be qualified as SectionLayout.Outline.

The transform applies positive uniform Scale about Pivot, then Rotate about Pivot,
then Translate. Defaults are scale one, zero pivot/translation/rotation. Scaling
was admitted for the guitar's tapered carve sections; it preserves the supported
exact curve families. This is placement of local boundary coordinates, not spatial
projection or a geometric curve offset. Nonuniform scaling is not admitted.

Lines, circular arcs, circles, ellipses and cubic Beziers retain their exact resolved
representations. Profiles have new identities while retaining segment names, source
segment provenance and Concept derivation. Closure, winding and supported
intersection checks remain with ordinary Profile validation; surfacing validity
remains with SectionChain. This operation does not claim a stronger intersection
proof. Cyclic derivations, unresolved planes/sources, duplicate fields and invalid
units fail with diagnostics.

## Files, caching and inspection

SectionChainFile/LoftFile resources use the existing Include source-graph loader for
both authored ports and materialization. Compilation-session keys include expanded
transitive source. Missing files and Include cycles cannot reuse cached geometry.
Snapshot Includes follow existing project-root-relative semantics; filesystem
Includes resolve relative to their source document.

SectionChainAuthoringParser.CompileFile and CLI section-chain operations use the
same loader. Compile(source) remains an already-loaded-source API. CLI section-chain
inspection exposes profileDerivations; assembly definition provenance retains
concept-boundary-placement, and instance provenance retains concept-datum-derivation.

The guitar has one body-outline module and nine small Concept placements for the
back, binding and carved top. Editing the shared outline rebuilds those three
definitions and reuses the other 50. Datum-only edits reuse geometry and recompute
placement and seating validation. Dependency evaluation is bounded and recursive.

## Guitar qualification

The three body sources plus their shared outline dropped from 901 to 193 lines.
The actual bound SectionChain comparison preserves section frames, seam identities,
span identities, degrees and knots. The maximum control-coordinate difference is
0.000000664mm, from replacing the legacy generator's independently rounded station
coordinates with shared authored controls. Exact BRep topology counts and reported
body bounds are unchanged; STEP hashes differ, so byte-identical geometry is not
claimed across the migration.

The Release solution build, fast core lane (1,004 tests), and full serial solution
lane (4,077 tests) passed. Sixteen derivation tests passed, including
published source geometry rotated 90 degrees about Y, offset seating, clocking,
cache reuse, invalid dependencies, duplicate-datum diagnostics and transitive
Include failures. Fresh CLI
inspection reports nine profile derivations and all four material seats passing.

Fresh-process cold compilation measured 11.056 seconds, warm uncached 4.785 seconds,
and populated-session recompilation 0.868 seconds. The cached run reused all 53
definitions and produced byte-identical USD to the same-run uncached baseline.
Display preparation measured 0.411 / 0.314 / 0.227 seconds respectively. Process
startup and dotnet build are excluded. This is one host run, not a benchmark against
another CAD product. No browser camera-interaction qualification was repeated.

Evidence and fresh exports are under ignored `artifacts/local/guitar-concept-derivation/`:
`final-build.log`, `final-fast.log`, `final-full.log`, `final-focused.log`,
`final-inspection.json`, `final-body-inspection.json`, `geometry-parity.json`,
`timings.json`, `guitar.step`, `step-export.json`, `guitar.usda`, and `usd-evidence.json`.
STEP export reports 126 occurrences excluding the root and 60 definitions; USD
reports 53 mesh definitions and 127 total occurrences including the root.

STEP reimport also succeeded with 53 geometry definitions and 126 occurrences,
including 35 subassembly occurrences. `step-reimport.json` records the round-trip.
The refreshed 1,800 by 1,800 Cycles side render was visually inspected and preserves
bridge contact; it took 66.2 seconds at 64 samples. `deck-side.png`, `render.json`
and `guitar-studio.blend` are in the same artifact directory. Render coloration is
downstream; body geometry comes from the Firmament compilation.
