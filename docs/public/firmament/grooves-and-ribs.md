# Grooves and manual ribs

Use named Concept guides or Paths to locate a channel or reinforcing rib. The
guide retains layout intent; the feature supplies its section and dimensions.
Both features produce one integrated analytic body, rather than overlapping
tool solids.

## Manual rib

This complete example is
[manual-rib.firmament](../../../fixtures/Canonical/Features/PathFeatures/manual-rib.firmament):

```firmament
Model ManuallyRibbedPlate {
    units: mm
    Concept Struct Layout on XY {
        Point2 Start { position: [-18mm, 0mm] }
        Point2 End { position: [18mm, 0mm] }
        Line2 Spine { from: Start; to: End }
    }
    Box Plate { size: [60mm, 40mm, 6mm] }
    Modify Plate {
        Rib<Rectangular> Stiffener {
            on: face(Plate.Top)
            path: Layout.Spine
            thickness: 3mm
            height: 10mm
            draft: 2deg
            ends: Round
        }
    }
}
```

`Rib` adds material to an existing host. It does not run AutoRib, relocate a guide,
or select a reinforcement network.

- `path` names a line, circular arc, circle, or a Path resolving to one such
  segment. Guide coordinates are XY coordinates local to the support.
- `thickness` is the full base thickness, centered on the guide.
- `height` is measured outward from the support.
- `draft` defaults to `0deg`. Positive draft narrows both sides toward the top:
  top thickness = base thickness − 2 × height × tan(draft).
- `ends` defaults to `Flat`. Straight guides also admit `Round`, giving
  semicircular ends in plan. Flat end planes remain at the guide endpoints:
  side draft does not shorten the guide. Flat end faces therefore have zero
  end draft, even when the band sides have positive draft.
- A closed circular guide has no ends; omit the field.

For a rounded straight rib, the guide endpoints are the centers of its rounded
ends. Overall length is guide length plus thickness at the relevant height.
An [arc rib](../../../fixtures/Canonical/Features/PathFeatures/arc-rib.firmament)
uses exact conical sides when drafted.

## Rib junctions and mixed features

Separate named ribs may meet as T, L or X junctions. Their footprints are
united into one material boundary; internal overlapping walls are removed.
Zero-draft line, arc and circular ribs may have different heights. Drafted
junctions currently require straight Flat-ended ribs of one common height.
The compiler proves the boundary stays affine through every possible section
event; topology-changing taper rejects rather than emitting an approximate loft.

The complete [reinforced mounting plate](../../../fixtures/Canonical/Features/PathFeatures/reinforced-mounting-plate.firmament)
combines two crossing drafted ribs, counterbored and countersunk through holes,
and a top perimeter fillet. Top-entry `ThroughAll` holes and one `+Z` perimeter
Fillet or Chamfer reuse the ordinary stock feature builders. Rib and groove
footprints must clear the holes' largest entry diameter and the perimeter finish.
The holes do not require a perimeter finish. Noninteracting grooves may share the same stock. A hole crossing a rib, or a
groove crossing another path feature, is explicitly rejected.

Build JSON exposes `ribPlanning`: the selected construction, candidate admission
and utility, and checked section intervals. Existing isolated constructions are
preferred to preserve their geometry. A completely buried/redundant named rib
is rejected because no independent feature boundary survives. Feature order does
not change a junction network's generated geometry or semantic names.

## Plate channels and circular face grooves

[plate-channels.firmament](../../../fixtures/Canonical/Features/PathFeatures/plate-channels.firmament)
contains two separate straight channels. Inside `Modify Plate`, write:

```firmament
Groove<Rectangular> Channel {
    on: +Z
    path: Upper
    width: 4mm
    depth: 2mm
    ends: Round
}
```

`Groove<Rectangular>` removes a centered band of the specified width and depth.
`Groove<Semicircular>` takes `radius` instead: opening width is twice the radius
and depth equals the radius. Supplying independent width/depth to a semicircle
is rejected.

For Round-ended straight channels, guide endpoints are cap centers: the opening
extends by half its width beyond each end.

A straight semicircular channel has a cylindrical bottom and, with Round ends,
spherical end patches. A circular or arc channel has a toroidal bottom.
[plate-seal-groove.firmament](../../../fixtures/Canonical/Features/PathFeatures/plate-seal-groove.firmament)
demonstrates a circular face groove.

## Cylindrical grooves

[shaft-seal-groove.firmament](../../../fixtures/Canonical/Features/PathFeatures/shaft-seal-groove.firmament)
declares a coaxial Circle2 with the shaft radius, then uses:

```firmament
Modify Shaft {
    Groove<Rectangular> SealSeat {
        on: face(Shaft.OuterWall)
        path: Layout.Seal
        station: 20mm
        width: 4mm
        depth: 2mm
    }
}
```

`station` is the axial center measured from the stock base (Z = 0). Width is
axial; depth is radial into material. `OuterWall` cuts inward, and `InnerWall`
cuts outward. The latter requires a top-open `Cylinder<Hollow>` host; its guide
circle radius equals the inner radius. See
[bore-seal-groove.firmament](../../../fixtures/Canonical/Features/PathFeatures/bore-seal-groove.firmament).
Both rectangular and semicircular sections are supported. The cut must leave
positive wall and axial end margins.

These constructs define geometry. An O-ring gland's squeeze, fill, clearances,
material compatibility and pressure suitability require separate engineering
requirements; a groove is not an automatic seal-design approval.

## Current limits

- Planar ribs/channels require a direct Box's top support (`+Z`, `Top` or
  `face(Body.Top)`). Other faces, Extrude/Compose and imported-body hosts reject.
- Cylindrical grooves require coaxial full circles on a direct Cylinder or
  top-open `Cylinder<Hollow>`, with no bottom blend. Cylindrical ribs,
  axial/helical cylinder channels and arbitrary surface paths reject.
- Open arcs require Flat ends and a positive sweep of at most 180 degrees.
  Compound Paths and arbitrary sections reject; junctions use separate named ribs.
- Planar footprints must remain strictly inside the support. Rib networks use
  exact line/arc arrangements. Grooves remain separate; their interactions reject.
  Groove-only sets retain conservative disjoint XY envelope admission.
  Cylindrical groove axial envelopes must be disjoint.
- Box ribs/channels may combine with the bounded holes and perimeter finish
  described above. Perforation, Thread and mixed cylindrical-groove operations
  remain unsupported. No requested operation is silently dropped.
- A rib top may not collapse under draft; a groove must leave positive material.
  No base fillet, molding-release certification or automatic rib placement is
  implied. Existing PlasticShell AutoRib retains its separate qualified limits.
- Construction points and guides must have unique names, including across
  Concept Struct scopes. Non-XY construction frames are
  rejected for these support-local feature consumers.
- Feature names and source provenance are retained. This does not introduce
  public selectors such as `face(Stiffener.Top)`.
- Authoritative volume assertions and SDF measurement are not yet qualified for
  these composite bodies. The CLI reports that limitation; topology inspection
  and explicitly numerical mesh comparisons remain available.

Related: [Profiles and construction](../reference/language/profiles.md),
[material offsets](material-offsets.md), [PlasticShell](plastic-shell.md),
and [solids and features](../reference/language/solids-and-features.md).
