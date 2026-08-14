# Corner and relief kernel

| Policy | Authored semantics | Flat topology | Formed topology | Typed failure mode |
|---|---|---|---|---|
| Open | Adjacent flanges retain deterministic clearance | Exact composed line blank when graph is admitted | Bend/flange axis length is shortened at the shared end | Consumed edge or unresolved point-touch |
| Mitered | Both neighbors request `Corner: Miter` | Symmetric setback participates in exact composition | Same setback shortens both bend/flange ends | Edge consumption / invalid pairing |
| RectangularRelief | Width/depth explicit or Auto | Exact diagonal four-line removal, subtracted before extrusion | Same corner record controls bend-end clearance | width < thickness, depth < radius+thickness, arrangement rejection |
| RoundRelief | Width/depth explicit or Auto; radius = width/2 | Exact round-ended line/arc removal; arcs survive STEP/SVG | Same corner record controls bend-end clearance | collapsed radius, invalid contour, arrangement rejection |

Auto values are width at least thickness and depth `inside radius + thickness`; derived values are retained in `SheetMetalReliefIr` evidence. Flat relief contours are separate `FlatReliefLoop` values and also participate in the composed exact blank.

The formed body remains a closed analytic BRep because bend/flange ends use the same corner setback. Curved relief wall faces are not yet cut through the formed planar skins. Consequently “same semantics” is true, while exact formed round-wall parity is deferred and not claimed.

One rectangular or round relief case passes exact blank validation and physical flat STEP. Four simultaneous relief removals in the legacy M3 tray and CTC-03 currently expose dangling/angular-order diagnostics. They retain valid compatibility artifacts but fail the new exact-blank DFM rule.
