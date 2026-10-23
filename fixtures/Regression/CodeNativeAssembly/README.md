# Local code-native assembly rehearsal

This fixture extends the parametric L-bracket demo with a separate threadless countersunk fastener. It uses actual analytic BRep, a named `Mate`, and AP242 product structure. It does not publish to the showcase repository.

- `bracket.firmament`: reusable bracket Record and Template. The two Rect2 guides establish a six-edge profile; Compose extrudes it and Hole<Countersink> cuts the seat.
- `fastener-library.firmament`: reusable Fastener Record/Template; Compose joins a Cylinder shaft and Frustum head, then EdgeFinish<Chamfer> cuts the **0.5 mm × 45° tip chamfer**. Shaft Ø5.7, head Ø12 × 90°, overall length 16 mm.
- `fastener.firmament`: standalone instance of that same Template.
- `assembly.firmasm`: bracket hole at (76, 10) mm; `InstallFastener` mates exposed seating frames.
- `relocated.firmasm`: only hole X changes to 60 mm. The fastener follows without a manual Placement, and its definition geometry remains unchanged.
- `solid-fastener-revolve.firmament`: the formerly rejected solid-axis meridian now builds the same solid through the bounded flat-ended monotone full-turn lane.
- `fastener-sections.firmament`: retains the previous circular SectionChain version as geometry-parity/regression evidence; it is not the preferred fastener authoring recipe.

Build the CLI, then run the repeatable rehearsal from the repository root:

```powershell
dotnet build Aetheris.CLI -c Release -m:1 -nr:false -p:UseSharedCompilation=false
python fixtures/Regression/CodeNativeAssembly/rehearse.py
# Optional visual review of the actual GLB (requires numpy and matplotlib):
python fixtures/Regression/CodeNativeAssembly/rehearse.py --preview
```

The script writes JSON evidence, STEP and GLB to ignored `artifacts/local/code-native-assembly/primitive-rehearsal/`. It checks normal validate/build admission, named Mate satisfaction, moved placement, unchanged fastener definition, separate assembly components and STEP import/reexport qualification, transforms, topology and bounds. It also checks the admitted full-turn solid Revolve and truthful rejection of an unsupported partial axis-touching turn. The optional preview checks exported mesh bounds before rendering; its exploded view never changes the engineering artifact. A failed run exits nonzero and does not retain a previous successful qualification summary.

`CodeNativeAssemblyRehearsalTests` independently checks actual cone/cylinder carriers, circle radii and stations, enclosure, export preflight, bounded numerical volume versus the analytic frustum formula, and the nominal coaxial seat. Primitive Compose, full Revolve and the retained SectionChain witness meet the same shape checks. Unsupported placements, mismatched joining radii, fields and finishes reject in both validate and build. Assembly PMI tests prove that Part PMI is not inherited and that an explicit assembly Note changes annotations without changing geometry.

## Engineering scope

The nominal head is flush at bracket Z=12 mm. The tip extends to Z=-4 mm. The Ø5.7 shaft in a Ø6 bore gives 0.3 mm diametral clearance; independently proposed ±0.1 mm size limits give 0.1–0.5 mm diametral clearance. Those size limits alone do not qualify angular/location tolerance stacks, conical contact under manufacturing variation, strength or retention. There are no threads or drive recess: this is a fastener blank for an assembly demonstration, not a released screw specification.

PMI ownership is intentional: the bracket Template's part-level PMI stays with that Part and is not promoted into assembly requirements. Declare assembly PMI explicitly in the root `.firmasm`; the admitted assembly lane currently supports notes/release metadata, not inferred GD&T or tolerance constraints. The fixture has no assembly PMI declaration and therefore exports none. The existing standalone bracket drawing is separate; this rehearsal does not generate an assembly manufacturing drawing.

Coaxial primitive Compose admits matching-radius positive-height stacks and exposed terminal Cylinder chamfers. It rejects arbitrary placements, shoulders, internal-rim finishes, Fillet, holes and unsupported PMI/tolerance consumers. Solid axis-touching Revolve admits full turns with one axis closure, radial flat caps and a strictly monotone positive-radius line chain. Pointed tips, radial shoulders, curved outer chains outside the existing sphere case, nonmonotone profiles and axis-touching partial turns remain rejected.
