# MOLD-X0a — Exact Molded-Feature B-rep

## Executive verdict

**Accepted for the bounded analytic-frustum CAD route, with an explicit zero-release-draft qualification.**

PlasticShell standoffs and the selected AutoRib network now lower to a compact, exact B-rep product boundary. The `96 × 48` polar height field has been removed from production and retained only as an experimental, non-manufacturing art export.

```text
PlasticShellIr
→ eligibility-first AutoRib judgment
→ analytic standoffs and constant-thickness wall ribs
→ exact shared-edge one-body B-rep
→ evidence and face-associated AP242 notes
→ rational-free STEP
```

## Product construction

The product path builds no mesh, feature solid, or arbitrary Boolean. The planar cavity floor contains exact footprint loops. Annular standoff cylinders and rib faces reuse those boundary edges, so there are no retained internal or coincident interface faces.

The flagship contains:

- four analytic annular standoffs, `7 mm` outer diameter, `3 mm` core hole, and `8 mm` height;
- three selected fan ribs, each exactly `2.2 mm` thick from base to top and `5 mm` high;
- two planar side faces and one flat planar top per rib;
- exact chord-to-cylinder rib/standoff junctions;
- the original analytic inner/outer cone, bottom, and rim supports.

The protected-exterior pre/post fingerprints remain equal at `5ab4561aee043362815686e09b457c1768244112cd9748005c56519522491512`; recorded maximum deviation is `0 mm`.

## Draft qualification

Constant-width vertical walls and positive release draft are geometrically incompatible: adding draft tapers the horizontal section. X0a follows the requested constant-section geometry and reports ribs and annular standoffs at `0°` release draft. A positive-draft model emits the warning `plastic-shell-constant-section-feature-zero-draft`; it is never presented as passing the requested `3°`.

The additions are +Z single-valued with no reverse undercut or side action in this bounded classification. That does not remove the molding-process decision created by zero release draft.

## AutoRib and junctions

With the canonical `ThicknessRatio: 1.0`, the gate-oriented fan remains the semantic winner:

| Candidate | Eligible | Utility | Length |
| --- | ---: | ---: | ---: |
| perimeter-network | yes | `0.6602834811` | `200 mm` |
| gate-oriented-fan | yes | `0.6739452689` | `170.710678 mm` |

The selected edges are `PcbC→PcbA`, `PcbC→PcbB`, and `PcbC→PcbD`. Each junction has accumulation proxy `1.0`:

```text
max(standoff radial wall, rib thickness) / nominal shell wall
```

This is a bounded thick-section check, not sink, cooling, shrinkage, warpage, or moldflow analysis.

## Reimported product artifact

Real CLI build and STEP reinspection report:

- one body and one closed shell;
- `enclosed-manifold` structural assessment;
- 36 faces, 58 edges, and 40 vertices;
- 20 planes, 14 cylinders, and 2 cones;
- no B-spline or rational product surfaces;
- four distinct standoff face associations and one distinct AutoRib face association;
- bounds `[-57,-57,0] .. [57,57,20] mm`.

Artifact: `artifacts/local/mold-x0a/mold-x0a-materialized-enclosure.step`  
SHA-256: `060f2316429a60e648b840c98f068bf60df492b420fe179f6ee4cd826ce5153c`

## Happy little accident

The old faceted generator now has exactly one explicit route:

```powershell
dotnet run --project Aetheris.CLI -- experimental heightfield-art fixtures/Canonical/PlasticShell/plastic-shell-enclosure.firmament --out artifacts/local/heightfield-art/happy-little-accident.step --json
```

It exports 9,124 faces and carries the AP242 annotation `height-field-art:happy-little-accident` targeting `NonManufacturingArtwork`, with text stating that it is not manufacturable CAD or a PlasticShell product definition. Normal `build` never invokes it.

Art artifact: `artifacts/local/heightfield-art/happy-little-accident.step`  
SHA-256: `ecf965ee2858c3d06f89177a695a4e63a90b2268f5156e0f54dd008a26ec2b3f`

## Remaining bounded limits

X0a still supports only the admitted analytic frustum and simple non-overlapping selected rib graphs. It does not provide general freeform shell offsets, arbitrary Boolean grafting, smooth fillets, mold blocks, side actions, moldflow, or automatic ejector relocation.
