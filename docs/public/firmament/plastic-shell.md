# PlasticShell

`PlasticShell` is Aetheris's manufacturing-first domain for injection-molded product definitions. Wall thickness, tooling direction, parting, and reinforcement intent live in the domain model rather than as a sequence of cleanup features.

```firmament
PlasticShell EnclosureHalf {
  Exterior: Frustum
  BottomRadius: 55 mm
  TopRadius: 57 mm
  Height: 20 mm
  Material: ABS
  ToolingDirection: [0, 0, 1]
  MinimumDraftAngle: 3 deg
  Preserve: [ExteriorDesignSurface]

  WallPolicy {
    NominalThickness: 2.2 mm
    MinimumThickness: 2.1 mm
    MaximumThickness: 2.3 mm
    ThicknessTolerance: 0.05 mm
  }

  PartingPlane MainParting {
    Origin: [0, 0, 20]
    Normal: [0, 0, 1]
  }
}
```

## Bounded X0a geometry

The accepted exterior is an open coaxial frustum. Its inner cone and bottom plane are exact analytic offsets. Aetheris reports exact wall thickness and cone-draft evidence, plus bounded +Z core/cavity/parting classification. This is not a general arbitrary-B-rep visibility or freeform-offset certificate.

Standoffs become analytic annular cylinders with retained core holes. Selected AutoRib edges become explicit B-rep walls:

- thickness equals `WallPolicy.NominalThickness` from base to top;
- each rib has two parallel planar side faces and one flat planar top;
- rib ends meet standoff cylinders through shared chord and vertical edges;
- the cavity floor owns the exact connected feature-footprint loops;
- shell, standoffs, and ribs form one closed product boundary with no mesh, feature solids, or coincident interface faces.

`RibPolicy.ThicknessRatio` must be `1.0` for this constant-shell-thickness realization. `BaseBlendRadius` remains retained intent but the current exact wall route does not claim a fillet or base flare.

There is an unavoidable molding tradeoff: parallel vertical rib sides and cylindrical standoff walls have **0° release draft**. The compiler keeps the requested clean constant sections and emits `plastic-shell-constant-section-feature-zero-draft` as a warning when the model requests positive draft. The geometry is +Z single-valued and has no reverse undercut, but tooling release remains an explicit process decision. A future tapered-wall construction may satisfy positive draft, but it would not be constant thickness in horizontal section.

## AutoRib and manufacturing evidence

AutoRib evaluates manufacturing eligibility before the shared Judgment Engine scores support connectivity, gate-flow compatibility, a bounded sink proxy, material length, and graph complexity. The sidecar retains all candidates, metrics, rejections, the selected network, and deterministic tie-break basis.

Each realized feature reports exact face associations, its authorized envelope, height, base/top thickness, nominal-wall ratio, and draft. Junction accumulation is bounded by the largest wall section meeting at the shared-edge junction:

```text
max(standoff radial wall, rib wall thickness) / nominal shell wall
```

This is a simple thick-section guard, not sink, cooling, shrinkage, warpage, or moldflow simulation. Ejector checks cover core-floor containment and exact selected-rib/standoff planar clearance; ejectors are never silently relocated.

## The happy little accident

The retired `96 × 48` polar height-field experiment is preserved as mathematical computer art. It is deliberately isolated from normal PlasticShell build and manufacturing evidence:

```powershell
dotnet run --project Aetheris.CLI -- experimental heightfield-art fixtures/Canonical/PlasticShell/plastic-shell-enclosure.firmament --out artifacts/local/heightfield-art/happy-little-accident.step --json
```

That command emits an AP242 note classifying the artifact as non-manufacturing artwork. A normal `build` never calls the height-field generator.

## Build and inspect

```powershell
dotnet run --project Aetheris.CLI -- build fixtures/Canonical/PlasticShell/plastic-shell-enclosure.firmament --output artifacts/local/mold-x0a/mold-x0a-materialized-enclosure.step --json
dotnet run --project Aetheris.CLI -- inspect fixtures/Canonical/PlasticShell/plastic-shell-enclosure.firmament --json
```

X0a does not generate mold blocks, runners, sprues, cooling channels, slides, lifters, or moldflow results. Freeform PlasticShell exteriors, arbitrary rib graphs, smooth junction blends, and automatic ejector relocation remain outside this bounded milestone.
