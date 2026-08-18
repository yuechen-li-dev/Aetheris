# CTC-03 formed comparison

Status: `NeedsReview`.

- Thickness residual: 0.002540 mm (accepted 1.905 mm nominal versus measured 1.90754 mm).
- Source -> generated boundary RMS/p95/max: 45.8066 / 114.7866 / 128.1174 mm.
- Generated -> source boundary RMS/p95/max: 11.1246 / 18.1023 / 51.5356 mm.
- All seven bends pass graph, axis direction, angle, and radius policy. Axis-line residuals range from effectively zero to 0.002541 mm; angle residuals are below 2.4e-12° and radius residuals below 2.6e-11 mm.
- Both vent cuts pass: center residual 0.001270 mm and size residual below 4e-10 mm.

The large boundary residual is localized to simplified authored wall/corner/mounting-flange trims versus the historical STEP's non-rectangular local outlines. It is not concealed by source geometry or an inflated tolerance.
