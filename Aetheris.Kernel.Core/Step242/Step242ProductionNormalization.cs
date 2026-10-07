using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Step242;

/// <summary>Bounded non-rational authority for production export; never consumes display geometry.</summary>
public static class Step242ProductionNormalization
{
    public static KernelResult<BrepBody> Prepare(BrepBody source, double maximumDeviationMillimetres)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!double.IsFinite(maximumDeviationMillimetres) || maximumDeviationMillimetres is <= 0d or > .1d)
            throw new ArgumentOutOfRangeException(nameof(maximumDeviationMillimetres));
        if (source.ImportQualification is { GeometryStatus: not ImportQualificationStatus.Qualified })
            return Failure("Source engineering geometry is not qualified for production normalization.");
        var geometry = new BrepGeometryStore();
        var changed = new HashSet<SurfaceGeometryId>();
        var diagnostics = new List<KernelDiagnostic>();
        foreach (var curve in source.Geometry.Curves) geometry.AddCurve(curve.Key, curve.Value);
        foreach (var entry in source.Geometry.Surfaces)
        {
            if (entry.Value.BSplineSurfaceWithKnots is not { IsRational: true } rational)
            { geometry.AddSurface(entry.Key, entry.Value); continue; }
            if (!BSplineSurfaceRationalReduction.TryReduce(rational, maximumDeviationMillimetres,
                out var reduced, out var deviation, out var reason) || reduced is null)
                return Failure($"Surface {entry.Key.Value}, source net {rational.ControlPoints.Count}x{rational.ControlPoints[0].Count}: {reason}");
            geometry.AddSurface(entry.Key, SurfaceGeometry.FromRecoveredBSplineSurfaceWithKnots(reduced,
                new("RATIONAL_B_SPLINE_SURFACE", "AdaptiveGreville", maximumDeviationMillimetres, deviation, true, reason)));
            changed.Add(entry.Key);
            diagnostics.Add(new(KernelDiagnosticCode.Unknown, KernelDiagnosticSeverity.Info,
                $"Surface {entry.Key.Value}: production non-rational recovery {deviation:R} mm within {maximumDeviationMillimetres:R} mm; {reason}",
                "Step242.ProductionNormalization"));
        }
        var bindings = new BrepBindingModel();
        foreach (var binding in source.Bindings.EdgeBindings) bindings.AddEdgeBinding(binding);
        foreach (var binding in source.Bindings.FaceBindings) bindings.AddFaceBinding(binding);
        foreach (var binding in source.Bindings.FaceBoundaryRoleBindings) bindings.AddFaceBoundaryRoleBinding(binding);
        foreach (var binding in source.Bindings.VertexLoopParameterBindings) bindings.AddVertexLoopParameterBinding(binding);
        foreach (var binding in source.Bindings.PcurveBindings)
            if (!changed.Contains(binding.SurfaceGeometryId) && binding.Pcurve.RationalWeights is null)
                bindings.AddPcurveBinding(binding);
        var points = source.Topology.Vertices.Where(v => source.TryGetVertexPoint(v.Id, out _))
            .ToDictionary(v => v.Id, v => { source.TryGetVertexPoint(v.Id, out var point); return point; });
        var body = new BrepBody(source.Topology, geometry, bindings, points, source.SafeBooleanComposition,
            source.ShellRepresentation, source.FaceOrientationReport);
        var tolerance = source.ImportQualification?.PcurveToleranceMillimetres ?? 1e-3d;
        body.PcurveRecoveryReport = BrepPcurveRecovery.Populate(body.Topology, geometry, bindings, tolerance);
        var evidence = BrepPcurveValidator.Validate(body, tolerance, requireEveryCoedge: true);
        if (!body.PcurveRecoveryReport.IsSuccess || !evidence.IsValid)
            return Failure("Normalized engineering pcurves failed: " + string.Join("; ",
                body.PcurveRecoveryReport.Diagnostics.Select(d => d.Message).Concat(evidence.Diagnostics).Take(8)));
        if (source.ImportQualification is { } report)
        {
            body.ImportQualification = BrepImportQualification.Evaluate(body, report.ExpectedFaces,
                report.SourceDistanceAccuracyMillimetres, tolerance, evidence);
            if (body.ImportQualification.GeometryStatus != ImportQualificationStatus.Qualified)
                return Failure("Normalized engineering topology did not remain qualified.");
        }
        return KernelResult<BrepBody>.Success(body, diagnostics);
    }

    private static KernelResult<BrepBody> Failure(string message) => KernelResult<BrepBody>.Failure([
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
            "step-production-normalization-unsupported: " + message, "Step242.ProductionNormalization")
    ]);
}
