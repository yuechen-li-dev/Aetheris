# Linear-elastic FEA

Preview 3's production FEA path is `Analysis LinearElastic` with the `LinearElasticIsotropic` material model. It supports a native body or bounded canonical `inlineSTEP`, catalog material lookup, `Fixed` component constraints, total-resultant `Force`, requested displacement/strain/stress/reaction results, and an explicit vector-lattice resolution.

Run the qualified aluminum cantilever:

```powershell
aetheris fea fixtures/Canonical/FEA/cantilever.firmament --out-dir artifacts/cantilever --json
```

The complete canonical shape is:

```firmament
Model CantileverWitness {
    Units: mm
    Box Beam { Size: [120mm, 20mm, 20mm] }

    Analysis LinearElastic Cantilever {
        Body: Beam
        Material: Standard.Materials.Aluminum.6061_T6
        Fixed Root {
            Region: Beam.face(-X)
            Components: [X, Y, Z]
        }
        Force Tip {
            Region: Beam.face(+X)
            Vector: [0N, -100N, 0N]
        }
        Results: [Displacement, Strain, Stress, ReactionForce]
        Lattice: [12, 2, 2]
    }
}
```

`Force`'s `Vector` is the total resultant distributed across the selected boundary, not a force per node. Native FEA consumes ordinary Model geometry such as `Box Beam { Size: [...] }`; it does not require a separate `solid` declaration dialect. The solver uses a cut-cell/vector-lattice formulation over the occupied body; it is not a conventional user-authored finite-element mesh. The fixture's simple cantilever provides a physically interpretable sanity witness, but it does not imply general solver qualification for nonlinear, anisotropic, plastic, contact, thermal, or dynamic physics.

The A36 witness [`material-resolved-cantilever.firmament`](../../../fixtures/Canonical/FEA/material-resolved-cantilever.firmament) is a 100 × 30 × 15 mm beam under a 500 N tip load. At `Lattice: [16, 2, 2]`, Preview 3 reports `25.0619 µm`; Euler–Bernoulli beam theory with the catalog's 200 GPa modulus predicts about `24.7 µm`. This is a narrow sanity comparison, not a general accuracy claim.

Native selectors preserve the body identifier: the example uses `Beam.face(-X)` and `Beam.face(+X)`. Imported selectors use AP242 identity such as `body.face(#170)` after bounded inline import. [`inline-step-cantilever.firmament`](../../../fixtures/Canonical/FEA/inline-step-cantilever.firmament) is the qualified imported example. Arbitrary imported containment is not promised; affected bodies fail loudly with `firmament-analysis-inline-step-containment-unsupported`.

Native Box analysis also consumes one canonical face-local `Hole<Shaft>` with `On: +Z` or `On: -Z` and `End: ThroughAll`. The analysis compiler uses the parsed diameter and center, so the opening removes occupied material. It maps the Box-centered CAD coordinates into the existing native analysis `[0, Size]` coordinate frame. Multiple semantic holes, side holes, counterbores, blind holes, construction-plane placements, and mixed hole/edge-finish modifications require the exported STEP analysis route and fail with `firmament-analysis-native-feature-unsupported` in this native lane. The complete [investor plate witness](../../../fixtures/InvestorPitch/loaded-plate-native.firmament) exercises this path.

The `build` command consumes geometry sources, while `fea` consumes sources containing `Analysis`. For a shared geometry and analysis demonstration, export the geometry file first and analyze that exact file with `inlineSTEP`; the report records the geometry hash. See [the reproducible investor demonstration](../investor-pitch.md).

## Experimental thin-wall analysis of native Sheet Metal

`Mode: ExperimentalShell` is a separate research lane. For an authored native `SheetMetal` body it derives thickness, material, planar panels, finite-radius cylindrical bends, openings, and adjacency from semantic Sheet Metal authority. Do not repeat thickness or construct a midsurface in the `Analysis` block. Imported/reconstructed BRep is not accepted as native thin-wall authority.

```firmament
Analysis LinearElastic NativeBracketLoad {
    Body: LBracket
    Mode: ExperimentalShell
    MasterGrid: [2, 2]
    Order: 2
    Fixed Clamp { Region: LBracket.Main.r-max }
    Force Tip {
        Region: LBracket.Wall.s-max
        Vector: [0N, 0N, -10N]
    }
    Results: [Displacement, Strain, Stress, ReactionForce, StrainEnergy]
}
```

Native patch boundaries use `<Body>.<Patch>.r-min|r-max|s-min|s-max`. Adjacent conforming panel/bend traces share global displacement degrees of freedom; opposite edge orientation is handled explicitly. Unsupported partial-edge interfaces and openings crossing a bend fail rather than being approximated.

Uniform gravity-like loading is available in this experimental lane when the material has density:

```firmament
BodyForce Gravity {
    Acceleration: [0m/s2, 0m/s2, -9.81m/s2]
}
```

The default experimental solver uses symmetric diagonal equilibration and bounded shifted IC(0). These are solver policy, not ordinary authoring choices. CLI-only diagnostic overrides can compare `identity`, `jacobi`, `block-jacobi`, and `ic0`. Output remains labeled `Experimental - not production-qualified` and includes the midsurface patch graph, selected solver policy, residual trace, and deterministic SVG/JSON diagnostics.
