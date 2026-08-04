# AIR-LATTICE-PRIMITIVE-M9R — standalone exact CubicTruss

M9R is Aetheris's bounded pre-release additive proof. It compiles one standalone lattice body; it does not lightweight an existing part.

## Admitted Firmament

```firmament
model CubicLatticeSample {
  units mm
  template<Additive> PolymerLattice {
    concept MinimumStrutDiameter: 1.0mm
    concept MinimumNodeDiameter: 2.0mm
    concept MinimumFeatureSpacing: 0.5mm
  }
  region Domain { box { size: [26.4mm, 26.4mm, 26.4mm] } }
  fill Domain {
    pattern: CubicTruss {
      cells: [3, 3, 3]
      cellSize: 8mm
      strutRadius: 0.8mm
      nodeRadius: 1.2mm
    }
    placement: MaterialBounds
  }
}
```

`Region` is the actual material-bounds declaration. `Fill` has no `Host`; it produces `StandaloneBody` materialization. The parser accepts one axis-aligned box domain, one `CubicTruss`, positive integral cell counts, and exact `MaterialBounds` only. The declared size must equal `N * CellSize + 2 * NodeRadius` on each axis—there is no rescaling or clipping.

## AIR, graph, and plan

`CubicLatticeFeature` preserves the host-null intent, box domain, cells, uniform radii, `MaterialBounds`, additive context, and source provenance. `CubicLatticeConstruction` is the hierarchical construction AIR used by `LatticeBodyBRepPlan`: cell domain → deterministic node graph → member graph → junction valence → seam instances.

For `Nx, Ny, Nz`, nodes are `(i,j,k)` for `0..Nx`, `0..Ny`, and `0..Nz`; their centres are spaced by `CellSize`. Members are only positive-X, positive-Y, and positive-Z nearest-neighbour pairs. IDs are `cubic:node:i:j:k` and `cubic:member:axis:canonical-start:canonical-end`, so construction does not depend on traversal order.

The canonical 3×3×3 result has 27 cells, 64 nodes, 144 members, and 288 endpoint seams. Its node classes are 8 valence-3 corners, 24 valence-4 edge nodes, 24 valence-5 face nodes, and 8 valence-6 interior nodes.

`LatticeBodyBRepPlan` is authoritative and immutable. It owns one exact spherical face per node, one exact cylindrical lateral face per member, every circular seam/edge/loop/coedge, and a deterministic SHA-256 signature. It does not Boolean-union separate solids, emit cylinder caps, or emit a planar domain skin.

## Exact junction geometry

For strut radius `Rs` and node radius `Rn`, each sphere-cylinder seam lies `d = sqrt(Rn² - Rs²)` from the node centre. A cylinder has exposed length `CellSize - 2d`; it is admitted only when positive. The spherical cap opening has `alpha = asin(Rs/Rn)`. M9R requires `Rn > sqrt(2) * Rs`, so openings on orthogonal cubic directions cannot overlap.

The canonical parameters give `d = sqrt(0.8) mm` and positive exposed members. Every seam is a full exact `CIRCLE`, used exactly once by its sphere and once by its cylinder. The resulting BRep is one connected, closed, manifold body.

## DFM and verification

`Template<Additive>` is a typed, inspectable manufacturing context. Before BRep/STEP emission M9R enforces minimum strut diameter, node diameter, non-incident spacing, cap-overlap admission, positive exposed length, and exact material-bounds fit. Examples include `minimum-strut-diameter-violation`, `minimum-node-diameter-violation`, `minimum-feature-spacing-violation`, `node-radius-too-small-for-struts`, `member-consumed-by-nodes`, and `material-bounds-mismatch`.

The M8 verifier operates on emitted BRep topology and bindings. For this multi-loop spherical topology it has an exact sphere-cylinder-seam integration path: retained sphere volume after each spherical cap removal plus exposed cylinder volume. It consumes the materialized BRep supports and circles, not the construction plan. STEP reimport preserves the one body, 208 faces, 288 edges, 288 vertices, 64 `SPHERICAL_SURFACE`s, and 144 `CYLINDRICAL_SURFACE`s.

The canonical artifact verified on 2026-08-04 has analytic and BRep volume `2168.779690804 mm³`, surface area `4990.344609935 mm²`, and centroid approximately `(0,0,0)`. Its 229,831-byte STEP has SHA-256 `e06fe7f345ce9e694c40a816e6061b2c019a9cf3d81a54e284509f530b30aeac` and contains 64 `SPHERICAL_SURFACE`, 144 `CYLINDRICAL_SURFACE`, 288 `CIRCLE`, and 288 `TRIMMED_CURVE` entities. External CAD inspection is `ExternalInspectionPending`—no CAD Assistant visual claim is made.

## Scope and post-release roadmap

M9R does **not** support host replacement, skins, cavities, through-hole coexistence, bonding, OctetTruss materialization, imported STEP hosts, curved regions, graded lattices, TPMS/SDF extraction, FEA, or toolpaths. The earlier M9 host-fill semantics remain in the codebase with their `lattice-fill-brep-plan-not-materialized` safety gate.

## Post-Release Lattice Roadmap

Fitting a lattice into an explicit region, OctetTruss, retained-host replacement, imported STEP lightweighting, boundary bonding, graded fields, FEA-driven distribution, implicit/TPMS backends, and additive toolpath lowering are all **deferred until after the initial Aetheris release**. They are not current support and are not required for M9R admission. Pre-release work returns to CNC and general mechanical CAD after M9R.
