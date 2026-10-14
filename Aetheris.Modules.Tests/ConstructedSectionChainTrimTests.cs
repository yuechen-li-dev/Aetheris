using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

/// <summary>
/// BUILD-PERF-M4: SectionChain transition faces carry trims constructed with their patches (each edge
/// is an isoparametric boundary of its own patch) instead of trims recovered by inverse projection.
/// The old qualification density (8193 lifted samples per spline trim) is the oracle here.
/// </summary>
public sealed class ConstructedSectionChainTrimTests
{
    public static TheoryData<string> Chains() => new() { "smooth", "ruled" };

    private static SectionChain Chain(string lane) => lane == "smooth"
        ? SectionChainTemplates.ErgonomicFairingG1()
        : SectionChainTemplates.ErgonomicFairing("g0", smooth: false);

    [Theory]
    [MemberData(nameof(Chains))]
    public void TransitionTrimsAreConstructedAndExactAtTheOldQualificationDensity(string lane)
    {
        var result = SectionChainMaterializer.Materialize(Chain(lane));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        var body = result.Body!;
        var splineTrims = body.Bindings.PcurveBindings
            .Where(binding => body.Geometry.GetSurface(binding.SurfaceGeometryId).Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots)
            .ToArray();
        Assert.NotEmpty(splineTrims);
        Assert.All(splineTrims, binding => Assert.Equal(PcurveBindingOrigin.ConstructedBoundary, binding.Qualification!.Origin));
        foreach (var binding in splineTrims)
        {
            var spline = body.Geometry.GetSurface(binding.SurfaceGeometryId).BSplineSurfaceWithKnots!;
            var coedge = body.Topology.GetCoedge(binding.CoedgeId);
            var edge = body.Bindings.GetEdgeBinding(coedge.EdgeId);
            var curve = body.Geometry.GetCurve(edge.CurveGeometryId);
            var interval = edge.TrimInterval!.Value;
            var worst = 0d;
            for (var sample = 0; sample < 8193; sample++)
            {
                var t = interval.Start + (interval.End - interval.Start) * sample / 8192d;
                var onEdge = curve.Kind == CurveGeometryKind.Line3 ? curve.Line3!.Value.Evaluate(t) : curve.BSpline3!.Value.Evaluate(t);
                var uv = binding.Pcurve.Evaluate(t);
                worst = double.Max(worst, (spline.Evaluate(uv.U, uv.V) - onEdge).Length);
            }
            Assert.True(worst <= 1e-7, $"{lane}: coedge {binding.CoedgeId.Value} lifts {worst:R} mm off its edge.");
        }
    }

    [Theory]
    [MemberData(nameof(Chains))]
    public void ConstructedTrimsRoundTripThroughStepAsSourceTrims(string lane)
    {
        var body = SectionChainMaterializer.Materialize(Chain(lane)).Body!;
        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess, string.Join(Environment.NewLine, step.Diagnostics.Select(item => item.Message)));
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(item => item.Message)));
        var splineTrims = imported.Value.Bindings.PcurveBindings
            .Where(binding => imported.Value.Geometry.GetSurface(binding.SurfaceGeometryId).Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots);
        Assert.All(splineTrims, binding => Assert.Equal(PcurveBindingOrigin.SourceValidated, binding.Qualification!.Origin));
    }
}
