# Linear-elastic FEA

Preview 3's production FEA path is `Analysis LinearElastic` with the `LinearElasticIsotropic` material model. It supports a native body or bounded canonical `inlineSTEP`, catalog material lookup, `Fixed` component constraints, total-resultant `Force`, requested displacement/strain/stress/reaction results, and an explicit vector-lattice resolution.

Run the qualified aluminum cantilever:

```powershell
aetheris fea fixtures/FEA/cantilever.firmament --out-dir artifacts/cantilever --json
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

The public-only A36 witness [`ai-fea-a36-cantilever.firmament`](../../../fixtures/PublicDogfood/ai-fea-a36-cantilever.firmament) is a 100 × 30 × 15 mm beam under a 500 N tip load. At `Lattice: [16, 2, 2]`, Preview 3 reports `25.0619 µm`; Euler–Bernoulli beam theory with the catalog's 200 GPa modulus predicts about `24.7 µm`. This is a narrow sanity comparison, not a general accuracy claim.

Native selectors preserve the body identifier: the example uses `Beam.face(-X)` and `Beam.face(+X)`. Imported selectors use AP242 identity such as `body.face(#170)` after bounded inline import. [`inline-step-through-hole.firmament`](../../../fixtures/FEA/inline-step-through-hole.firmament) is the qualified imported example. Arbitrary imported containment is not promised; affected bodies fail loudly with `firmament-analysis-inline-step-containment-unsupported`.
