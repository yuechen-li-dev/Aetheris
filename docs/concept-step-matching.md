# Concept-to-STEP matching (M5)

`aetheris match part.step concept.firmament --json` compares resolved compile-time Concept IR with bounded analytic evidence imported from STEP/AP242. It is read-only.

> Aetheris does not reconstruct the original feature tree.
>
> It tests whether a declared semantic Concept Struct explains or conforms to the observed STEP artifact.

The M5 matcher supports `Box3` body bounds, `Plane` analytic planar faces, `Axis` analytic cylindrical axes, and `Point3[]` declared as hole centers. A bare `Point3` is deliberately `Unverifiable`; vertices are not semantic evidence by default.

Declare the semantic role separately from the value:

```firmament
Match {
    MountPoints As HoleCenters {
        Diameter: 8.5mm
        Axis: +Z
        Kind: Through
    }
}
```

The role is compile-time conformance metadata and never lowers to AIR/BRep. The engine uses deterministic nearest unused-candidate assignment for small point sets. Hole evidence comes from analytic cylindrical faces, with center, axis, radius, span, and a bounded through classification; it is `DerivedAnalytic`. Planes and cylinder axes are `ExactAnalytic`; body bounds are `TopologySupported` vertex evidence.

Defaults are conservative: 0.01 mm linear/dimension and 0.1 degree angular tolerance. Override linear and angular tolerances with `--linear-tolerance` and `--angular-tolerance`. Every member result includes its measured deviation and allowed tolerance.

Member statuses are `Matched`, `WithinTolerance`, `Candidate`, `Ambiguous`, `Conflicted`, `Missing`, `Unverifiable`, or `Unsupported`. Equivalent coplanar planar faces remain `Ambiguous`; the matcher never chooses one silently. Overall `Matched` means every requested contract matches, `Partial` means useful evidence exists without direct contradiction, and `Conflicted` means an expected required observation is contradicted. `Matched` and `Partial` exit 0; conflicts and invalid source/STEP exit 1.

STEP face IDs may occur in diagnostic report evidence, but are not part of Firmament syntax or the language contract. This bounded evidence workflow supports partial semantic refactoring: a concept may specify only bounds and a plane, rather than reconstructing all historical features. It does not support freeform/tessellated proof, generic point roles, feature-tree reconstruction, or automatic rewriting.
