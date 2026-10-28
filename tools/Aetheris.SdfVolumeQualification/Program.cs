using System.Diagnostics;
using System.Text.Json;
using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

var budget = args.Length > 0 ? int.Parse(args[0]) : 100001;
var relativeError = args.Length > 1 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : .001;
var repetitions = args.Length > 2 ? int.Parse(args[2]) : 3;
if (budget < 1 || !double.IsFinite(relativeError) || relativeError <= 0 || repetitions < 1)
    throw new ArgumentException("Expected positive region budget, relative error and repetition count.");
var cases = new List<(string Name, SdfNode Node, double Expected)>();
cases.Add(("sphere", new SdfSphereNode(10), 4 * Math.PI * 1000 / 3));
cases.Add(("scaled-rotated-translated-sphere", new SdfTransformNode(new SdfSphereNode(10),
    Transform3D.CreateScale(new Vector3D(2, .5, 1.5)) * Transform3D.CreateRotationY(.37) * Transform3D.CreateTranslation(new(53, -21, 12))), 4 * Math.PI * 1000 / 3 * 1.5));
cases.Add(("cylinder", new SdfCylinderNode(5, 20), Math.PI * 25 * 20));
cases.Add(("frustum", new SdfConeNode(6, 2, 12), Math.PI * 12 * (36 + 12 + 4) / 3));
cases.Add(("torus", new SdfTorusNode(10, 2), 2 * Math.PI * Math.PI * 10 * 4));
SdfNode plate = new SdfBoxNode(60, 40, 6);
foreach (var x in new[] { -15, 0, 15 }) plate = new SdfSubtractNode(plate, new SdfTransformNode(new SdfCylinderNode(3, 8), Transform3D.CreateTranslation(new(x, 0, 0))));
cases.Add(("three-through-hole-plate", plate, 60 * 40 * 6 - 3 * Math.PI * 9 * 6));
var ribs = new SdfUnionNode(new SdfBoxNode(40, 30, 4), new SdfUnionNode(
    new SdfTransformNode(new SdfBoxNode(24, 3, 8), Transform3D.CreateTranslation(new(0, 0, 6))),
    new SdfTransformNode(new SdfBoxNode(3, 18, 8), Transform3D.CreateTranslation(new(0, 0, 6)))));
cases.Add(("crossed-ribs", ribs, 40 * 30 * 4 + 8 * (24 * 3 + 3 * 18 - 3 * 3)));
cases.Add(("thin-sphere-shell", new SdfSubtractNode(new SdfSphereNode(10), new SdfSphereNode(9.5)), 4 * Math.PI * (1000 - Math.Pow(9.5, 3)) / 3));
var left = new SdfTransformNode(new SdfSphereNode(5), Transform3D.CreateTranslation(new(-3, 0, 0)));
var right = new SdfTransformNode(new SdfSphereNode(5), Transform3D.CreateTranslation(new(3, 0, 0)));
var lens = Math.PI * (20 + 6) * 16 / 12;
cases.Add(("sphere-union", new SdfUnionNode(left, right), 2 * 4 * Math.PI * 125 / 3 - lens));
cases.Add(("sphere-intersection", new SdfIntersectNode(left, right), lens));
var results = new List<object>();
var success = true;
foreach (var item in cases)
{
    var tape = SdfTapeLowerer.Lower(item.Node);
    var error = item.Expected * relativeError;
    SdfBoundedVolumeResult Run(bool control) => control
        ? IntervalOnlyControl.EstimateBounded(tape, item.Node.Bounds, error, budget)
        : SdfAdaptiveVolumeEstimator.EstimateBounded(tape, item.Node.Bounds, error, budget);
    // Full-workload warmup for each path. Alternate timed order to reduce drift.
    _ = Run(true); _ = Run(false);
    var measurements = new List<(bool Control, double Milliseconds, long AllocatedBytes, SdfBoundedVolumeResult Result)>();
    for (var repeat = 0; repeat < repetitions; repeat++)
    foreach (var control in repeat % 2 == 0 ? new[] { true, false } : new[] { false, true })
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew(); var measured = Run(control); watch.Stop();
        measurements.Add((control, watch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated, measured));
        success &= measured.LowerBound <= item.Expected && item.Expected <= measured.UpperBound;
    }
    object Summarize(bool control)
    {
        var runs = measurements.Where(m => m.Control == control).OrderBy(m => m.Milliseconds).ToArray();
        var result = runs[runs.Length / 2].Result;
        return new { result.LowerBound, result.UpperBound, result.EstimatedVolume, result.ErrorBound,
            result.RegionsVisited, result.BudgetExhausted, result.AffineCutRegions, result.IntervalFallbackRegions,
            MedianMilliseconds = runs[runs.Length / 2].Milliseconds,
            AllocatedBytes = runs[runs.Length / 2].AllocatedBytes,
            ReferenceContained = result.LowerBound <= item.Expected && item.Expected <= result.UpperBound };
    }
    results.Add(new { item.Name, ExpectedMm3 = item.Expected, RequestedErrorMm3 = item.Expected * relativeError,
        IntervalOnly = Summarize(true), Affine = Summarize(false) });
}
Console.WriteLine(JsonSerializer.Serialize(new { budget, relativeError, repetitions, ControlRevision = "e1310ce2", success, results }, new JsonSerializerOptions { WriteIndented = true }));
return success ? 0 : 1;
