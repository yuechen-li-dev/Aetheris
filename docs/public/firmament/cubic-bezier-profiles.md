# Direct cubic profile guides

`CubicBezier2` supplies one bounded, non-rational polynomial span to an ordinary `Profile`. Four named `Point2` guides define its start, two controls, and end:

```firmament
Point2 Start { Position: [0mm,0mm] }
Point2 StartHandle { Position: [0mm,10mm] }
Point2 EndHandle { Position: [20mm,20mm] }
Point2 End { Position: [30mm,20mm] }
CubicBezier2 Flank { From: Start; Control1: StartHandle; Control2: EndHandle; To: End }
```

Inside a `Profile`, consume the complete span with `Segment FlankEdge { Trace: Flank; From: Start; To: End }`. Its endpoints must be the authored endpoints; no subspan, automatic fit, or rational weights are accepted. Other named spans must close the profile. Ordinary Profile validation checks winding, closure, and self-intersection before extrusion.

The [lamp base intent fixture](../../../fixtures/Canonical/ThreeDm/lamp-base-intent.firmament) uses two mirrored cubic flanks and a circular end. The source 3DM is measurement evidence; the polynomial controls in that fixture are intentionally new design parameters.
