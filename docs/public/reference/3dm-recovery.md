# 3DM geometry recovery evidence

3DM support is an optional tool because Rhino/OpenNURBS interoperability requires the native Rhino3dm library. Install it separately:

```powershell
dotnet tool install --global Aetheris.ThreeDm.CLI --prerelease
aetheris-3dm inspect model.3dm --json
aetheris-3dm recover model.3dm --json
```

The default `aetheris` compiler does not include Rhino3dm. Its old `inspect-3dm` / `recover-3dm` commands report how to install and invoke the companion tool.

From a source checkout, use `dotnet run --project Aetheris.ThreeDm.CLI -- inspect model.3dm --json` (or `recover`). Pack the optional tool with `dotnet pack Aetheris.ThreeDm.CLI -c Release -o artifacts/local/packages`; it is intentionally separate from `Aetheris.CLI`.

`aetheris-3dm inspect model.3dm --json` reads Rhino/OpenNURBS file metadata and BRep inventory. `aetheris-3dm recover model.3dm --json` measures bounded analytic and non-rational recovery candidates. Use `--body N --edge N` or `--body N --face N` with `recover-3dm` to inspect one source entity. `--recovery-tolerance-mm N` sets the generic spline budget (default 0.1 mm). The file's unit metadata determines conversion to Aetheris millimetres.

The recovery report is a **review aid**. It lists source object UUIDs, body/face/edge indices, degree and control-net sizes, candidate dimensions, RMS/p95/max deviations, angular deviations, rejected alternatives, and topology status. It also groups repeated radii as hypotheses. For unrecognized rational supports, `genericRecovery` reports the bounded non-rational candidate, its method, control-net complexity, and deviation against both the shared reducer and independent Rhino evaluation. The analysis mode does not build a BRep or STEP.

`aetheris-3dm recover model.3dm --bind-step --out-dir artifacts/local/3dm-bind --json` runs the separate topology-binding path. It preserves Rhino edge reuse, vertices, loops, and trim direction, retains source UV pcurves when qualified, and asks the shared recovered-pcurve binder for the rest. It exports only bodies that pass shell, pcurve, trusted STEP preflight, reimport, and sampled bounding-box checks. STEP files and the JSON report should stay under ignored `artifacts/local/` for private source models. Add `--body N` to bind just one source BRep.

Add `--combined-step` for a whole-file AP242 assembly when every source BRep qualifies. This emits `recovered-assembly.step` alongside deterministic per-object files; a source BRep with disconnected closed components contributes one STEP solid per component. Singular Rhino trims remain vertex semantics in the bound BRep. On mixed singular loops, the reference STEP omits pcurve associations that cannot yet roundtrip reliably, while preserving the 3D edges, faces, and source binding evidence. Explicit source vertex tolerances are capped locally at 1 mm and reported separately from the default 0.1 mm spline recovery budget. A near-coincident planar inner loop can be reimported under the explicit `AllowBoundedNearCoincidentInnerLoop` import policy; the default strict STEP importer continues to reject crossings.

Reported BRep bounds use 129 deterministic samples on each trimmed edge. Rhino's BRep bounding box can include the untrimmed support surfaces and substantially overstate a part's envelope. Edge-sampled bounds are useful for navigating this file but are not certified face-interior extrema.

Rational NURBS are source evidence. A simpler analytic carrier or non-rational spline may be proposed only with measured residuals and a stated qualification. A source geometric tolerance is not a manufacturing tolerance. Numerical agreement on sampled support geometry does not qualify face boundaries, edge uses, orientation, or shell enclosure. Production promotion needs those checks and an explicit caller policy. `TrustedProductionRoute` STEP export rejects rational B-spline surfaces and pcurves; legacy interchange output can retain them for compatibility and debugging.

The private Cartesian Product Design `.3dm` used in the [qualification work](../../development/history/eras/08-domain-expansion.md) is local-only and excluded from Git. Automation should keep detailed JSON under ignored `artifacts/local/`.
