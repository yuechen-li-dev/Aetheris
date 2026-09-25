using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242LocalRationalWitnessTests
{
    [Fact]
    public void McMasterThreadedBoltImportsTessellatesAndRoundTripsWhenSupplied()
    {
        var path = Environment.GetEnvironmentVariable("AETHERIS_STEP_NURBS_WITNESS");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        var imported = Step242Importer.ImportBody(File.ReadAllText(path));
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => d.Message)));
        var body = imported.Value;
        Assert.Equal(147, body.Topology.Faces.Count());
        Assert.Equal(405, body.Topology.Edges.Count());
        var recoveredCurves = body.Geometry.Curves.Count(pair => pair.Value.RecoveryProvenance is not null);
        var recoveredSurfaces = body.Geometry.Surfaces.Count(pair => pair.Value.RecoveryProvenance is not null);
        Assert.True(recoveredCurves > 0);
        Assert.True(recoveredSurfaces > 0);
        Assert.NotNull(body.PcurveRecoveryReport);
        Assert.True(body.PcurveRecoveryReport.IsSuccess, string.Join("\n", body.PcurveRecoveryReport.Diagnostics.Select(d => d.Message)));
        Assert.Equal(body.Topology.Coedges.Count(), body.Bindings.PcurveBindings.Count());
        Assert.Equal(810, body.Bindings.PcurveBindings.Count());
        Assert.All(body.Bindings.PcurveBindings, binding => Assert.NotNull(binding.Qualification));
        var pcurveEvidence = BrepPcurveValidator.Validate(body, 1e-3d, requireEveryCoedge: true);
        Assert.True(pcurveEvidence.IsValid,
            $"worst={pcurveEvidence.MaximumReconstructionDeviation:R}; closure={pcurveEvidence.MaximumUvLoopClosure:R}; "
            + string.Join("\n", pcurveEvidence.Diagnostics.Take(12)));
        Assert.All(body.Geometry.Curves.Where(pair => pair.Value.RecoveryProvenance is not null),
            pair => Assert.InRange(pair.Value.RecoveryProvenance!.MeasuredMaxDeviationMillimetres, 0d, 0.1d));

        var tessellation = BrepDisplayTessellator.Tessellate(body);
        Assert.True(tessellation.IsSuccess, string.Join("\n", tessellation.Diagnostics.Select(d => d.Message)));
        Assert.NotEmpty(tessellation.Value.FacePatches);

        var exported = Step242Exporter.ExportBody(body);
        Assert.True(exported.IsSuccess, string.Join("\n", exported.Diagnostics.Select(d => d.Message)));
        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join("\n", reimported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(body.Topology.Faces.Count(), reimported.Value.Topology.Faces.Count());
        Assert.Equal(body.Topology.Edges.Count(), reimported.Value.Topology.Edges.Count());
        Assert.Equal(body.Bindings.PcurveBindings.Count(), reimported.Value.Bindings.PcurveBindings.Count());
        var worstRoundTripCurveDeviation = 0d;
        foreach (var edge in body.Topology.Edges)
        {
            Assert.True(body.TryGetEdgeCurveGeometry(edge.Id, out var originalCurve));
            Assert.True(reimported.Value.TryGetEdgeCurveGeometry(edge.Id, out var roundTripCurve));
            if (originalCurve?.BSpline3 is not { } originalSpline || roundTripCurve?.BSpline3 is not { } roundTripSpline)
                continue;
            for (var sample = 0; sample <= 16; sample++)
            {
                var parameter = originalSpline.DomainStart +
                    (originalSpline.DomainEnd - originalSpline.DomainStart) * sample / 16d;
                worstRoundTripCurveDeviation = double.Max(worstRoundTripCurveDeviation,
                    (originalSpline.Evaluate(parameter) - roundTripSpline.Evaluate(parameter)).Length);
            }
        }
        Assert.InRange(worstRoundTripCurveDeviation, 0d, 0.01d);
    }
}
