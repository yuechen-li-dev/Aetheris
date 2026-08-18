# AIR-LATTICE-FILL-M9 — explicit-region lattice-fill foundation

> Status after M9R: this is a deferred host-replacement design, not a materialized STEP route. The completed lattice capability is the standalone CubicTruss proof documented in [AIR-LATTICE-PRIMITIVE-M9R](air-lattice-primitive-m9r.md).

> Aetheris does not guess which material is expendable.
>
> The FillRegion is an explicit engineering authority boundary.

M9 introduces the semantic foundation for a production lattice-fill route.  It is deliberately bounded to one history-known, axis-aligned Box host with one Z-axis through-hole, one closed axis-aligned box FillRegion, `OctetTruss`, and `BoundaryPolicy: Bond`.  Imported STEP hosts are **deferred**: this change does not claim imported-body lightweighting.

## Firmament V2 source shape

The existing lower-case V2 parser convention is used (length units are accepted in the Fill declarations):

```firmament
model LightweightBracket {
  units mm
  template<Additive> PolymerPrototype {
    concept MinimumWallThickness: 1.2mm
    concept MinimumStrutDiameter: 1.0mm
    concept MinimumBondDiameter: 1.2mm
    concept MinimumHoleDiameter: 2.0mm
  }
  solid Body: Box { size: [80, 50, 20] }
  modify Body {
    hole<Shaft> MountHole { on: face(+Z) center: [0, 0] diameter: 12 end: throughAll }
  }
  region LightweightCore {
    box { size: [24mm, 24mm, 16mm] center: [22mm, 0mm, 0mm] }
  }
  fill LightweightCore {
    host: Body
    pattern: OctetTruss { cellSize: 8mm strutRadius: 0.8mm }
    boundaryPolicy: Bond
  }
}
```

The 24 × 24 × 16 mm region is intentional: the prompt's 48 × 26 × 12 example cannot be both fully internal and clear of a centered Ø12 hole in the stated 80 × 50 host. This admitted fixture has a three-by-three-by-two cell domain, 2 mm top/bottom retained walls, and a 4 mm direct clearance to the hole.

## AIR and construction contract

`LatticeFillFeature` preserves host identity, explicit region bounds, `OctetTruss` parameters, `Bond`, typed additive context, and source provenance. `LatticeFillM9.Construct` turns that into `LatticeFillConstruction` with deterministic cell indices, shared node instances, canonical-endpoint member instances, boundary incidents, and bond witnesses.

An octet cell contains eight corner nodes and six face-centre nodes. Each face-centre connects to its four face corners. Shared corners and shared face-centres are deduplicated from coordinates/roles; members are deduplicated by lexically canonical endpoint pairs. The finite cell domain is the floor of each declared region extent divided by cell size. This is mathematical graph generation, not source-level repetition.

Every boundary incident produces a `LatticeAttachmentWitness` with member identity, planar boundary name, intersection/node point, contact diameter (`2 × StrutRadius`), and retained-host owner. The chosen node-junction contract is a future exact spherical junction with radius at least the strut radius; it is not a claim of G1 blending.

## Additive DFM

The parser captures `Template<Additive>` concepts. Before emission the M9 validator requires and checks:

- `MinimumWallThickness` against directly derived Box/FillRegion clearance;
- `MinimumStrutDiameter` against `2 × StrutRadius`;
- `MinimumBondDiameter` against terminal contact diameter;
- `MinimumHoleDiameter` against the semantic through-hole.

The diagnostics are `additive-minimum-wall-thickness-violation`, `additive-minimum-strut-diameter-violation`, `additive-minimum-bond-diameter-violation`, and `additive-minimum-hole-diameter-violation`, with template, feature, actual, and required values where applicable. Region parsing additionally rejects missing/invalid boxes, unknown regions/hosts, non-positive cell size and radius, unsupported patterns/policies, and more than one fill.

## Current authoritative-BRep boundary — honest status

The exact graph and DFM contracts are implemented and tested. The final `LatticeFilledBodyBRepPlan` and STEP result are **not** implemented, so no hash, size, topology count, material volume, mass-reduction percentage, or M8 verification evidence is reported. The build/export path deliberately returns `lattice-fill-brep-plan-not-materialized` after DFM validation rather than exporting the unmodified host.

The concrete blocker is the current single-body BRep pipeline: it cannot construct one valid authoritative topology containing (1) the retained Box material around an internal rectangular cavity, (2) the pre-existing cylindrical through-hole, and (3) exact analytic cylindrical struts, spherical nodes, and planar bonded attachments. The current STEP exporter only accepts one BRep body; exporting independently built lattice solids would violate the required final material semantics. This is why no automatic skinning, Boolean spray, mesh/SDF extraction, or assembly workaround is claimed.

## Deferred work

The next convergent implementation should add one plan-driven exact merge family for this bounded topology: retained Box-minus-rectangular-region plus one preserved Z through-hole, then analytic cylinder/sphere/bond attachments merged into one closed shell with explicit face/edge/loop ownership. Only after that should the compiler emit a STEP artifact and run M8 independent volume, topology, serialization, and external CAD checks.
