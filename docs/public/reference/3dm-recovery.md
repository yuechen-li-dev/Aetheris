# 3DM geometry recovery evidence

`aetheris inspect-3dm model.3dm --json` reads Rhino/OpenNURBS file metadata and BRep inventory. `aetheris recover-3dm model.3dm --json` measures bounded analytic and non-rational recovery candidates. Use `--body N --edge N` or `--body N --face N` with `recover-3dm` to inspect one source entity. The file's unit metadata determines conversion to Aetheris millimetres.

The recovery report is a **review aid**. It lists source object UUIDs, body/face/edge indices, degree and control-net sizes, candidate dimensions, RMS/p95/max deviations, angular deviations, rejected alternatives, and topology status. It also groups repeated radii as hypotheses. The command does not rebuild trims, create a canonical Aetheris BRep, infer Firmament authoring history, or export STEP.

Reported BRep bounds use 129 deterministic samples on each trimmed edge. Rhino's BRep bounding box can include the untrimmed support surfaces and substantially overstate a part's envelope. Edge-sampled bounds are useful for navigating this file but are not certified face-interior extrema.

Rational NURBS are source evidence. A simpler analytic carrier or non-rational spline may be proposed only with measured residuals and a stated qualification. A source geometric tolerance is not a manufacturing tolerance. Numerical agreement on sampled support geometry does not qualify face boundaries, edge uses, orientation, or shell enclosure. Production promotion needs those checks and an explicit caller policy. `TrustedProductionRoute` STEP export rejects rational B-spline surfaces and pcurves; legacy interchange output can retain them for compatibility and debugging.

The private Cartesian Product Design `.3dm` used in the [X1 qualification report](../../release/3DM-RECOVERY-X1.md) is local-only and excluded from Git. Automation should keep detailed JSON under ignored `artifacts/local/`.
