# SDF occupied-volume qualification

Exercises the public bounded estimator on ten independent analytic volume references:
primitives, a nonuniformly transformed sphere, through holes, intersecting ribs,
a thin shell, and overlapping-sphere union/intersection. Expected volumes set the
requested absolute uncertainty and check containment; the estimator receives no
reference volume or primitive-specific volume formula.

The standard probe requests 0.1%; use `.0001` for a 0.01% precision-limit probe.
An honest exhausted result is expected on some of the harder cases at that target.

```powershell
dotnet build tools/Aetheris.SdfVolumeQualification -c Release -m:1 -nr:false -p:UseSharedCompilation=false
$env:DOTNET_TieredCompilation = '0'
dotnet tools/Aetheris.SdfVolumeQualification/bin/Release/net10.0/Aetheris.SdfVolumeQualification.dll 1000001 .001 3 > artifacts/local/sdf-volume/qualification.json
Remove-Item Env:DOTNET_TieredCompilation
```

Arguments are maximum visited regions, relative uncertainty and timed repetitions.
Both paths receive a full-workload warmup, followed by alternating execution order;
JSON reports median elapsed time and managed allocations for that run. Timing is
evidence, not a portable performance assertion. Exit code 1 means a computed interval
excluded its reference. `BudgetExhausted` is reported separately: an inconclusive
enclosure is not silently counted as meeting the requested uncertainty.

`IntervalOnlyControl.cs` freezes the former production estimator from `e1310ce2`
for a controlled comparison with identical tape intervals, bounds, error and budget.
It is executable qualification evidence, not another selectable production strategy.

`--imported-occupancy-probe path.step` exercises the existing analytic ray-consensus
query at 125 deterministic points, reusing one query context. It reports unknown
classifications, runtime and complete-field admission separately. Vertex extents
only choose a sampling domain; they are not certified bounds. This experiment
never estimates a volume or grants authority from point samples.
