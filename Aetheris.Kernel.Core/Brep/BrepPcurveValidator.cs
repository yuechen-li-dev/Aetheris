using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep;

public sealed record BrepPcurveEvidence(
    bool IsValid,
    int EdgeCount,
    int PcurveCount,
    double MaximumReconstructionDeviation,
    bool DomainValid,
    bool OrientationConsistent,
    IReadOnlyList<string> Diagnostics);

/// <summary>Independently samples face-local pcurves against their shared 3D edge geometry.</summary>
public static class BrepPcurveValidator
{
    public static BrepPcurveEvidence Validate(BrepBody body, double tolerance = 1e-6, bool requireEveryCoedge = false, int samples = 67)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!double.IsFinite(tolerance) || tolerance <= 0d) throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (samples < 3) throw new ArgumentOutOfRangeException(nameof(samples));
        var diagnostics = new List<string>();
        var maximum = 0d;
        var domainValid = true;
        var orientationValid = true;

        foreach (var coedge in body.Topology.Coedges.OrderBy(item => item.Id.Value))
        {
            var coedgeMaximum = 0d;
            if (!body.Bindings.TryGetPcurveBinding(coedge.Id, out var binding))
            {
                if (requireEveryCoedge) diagnostics.Add($"surf-pcurve-missing:coedge={coedge.Id.Value}");
                continue;
            }
            var face = body.Topology.Faces.SingleOrDefault(candidate => candidate.LoopIds.Contains(coedge.LoopId));
            if (face is null || face.Id != binding.FaceId)
            {
                diagnostics.Add($"surf-pcurve-invalid:coedge={coedge.Id.Value}:owning-face-mismatch");
                continue;
            }
            if (!body.Bindings.TryGetFaceBinding(face.Id, out var faceBinding) || faceBinding.SurfaceGeometryId != binding.SurfaceGeometryId
                || !body.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface) || surface is null
                || !body.Bindings.TryGetEdgeBinding(coedge.EdgeId, out var edgeBinding)
                || !body.Geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve) || curve is null)
            {
                diagnostics.Add($"surf-pcurve-invalid:coedge={coedge.Id.Value}:binding-reference");
                continue;
            }
            var interval = edgeBinding.TrimInterval ?? binding.Pcurve.Domain;
            if (!FiniteOrdered(binding.Pcurve.Domain) || binding.Pcurve.Points.Count == 0)
            {
                domainValid = false;
                diagnostics.Add($"surf-pcurve-invalid:coedge={coedge.Id.Value}:domain");
                continue;
            }
            for (var index = 0; index < samples; index++)
            {
                var fraction = index / (double)(samples - 1);
                var parameter = interval.Start + ((interval.End - interval.Start) * fraction);
                var curveParameter = binding.SameSense ? parameter : interval.End - ((parameter - interval.Start));
                var uv = binding.Pcurve.Evaluate(curveParameter);
                var onSurface = Evaluate(surface, uv);
                var onCurve = Evaluate(curve, parameter);
                if (onSurface is null || onCurve is null)
                {
                    diagnostics.Add($"surf-pcurve-invalid:coedge={coedge.Id.Value}:unsupported-evaluator");
                    break;
                }
                coedgeMaximum = double.Max(coedgeMaximum, (onSurface.Value - onCurve.Value).Length);
            }
            maximum = double.Max(maximum, coedgeMaximum);
            if (coedgeMaximum > tolerance)
                diagnostics.Add($"surf-pcurve-invalid:coedge={coedge.Id.Value}:face={face.Id.Value}:surface={surface.Kind}:curve={curve.Kind}:pcurve={binding.Pcurve.Kind}:deviation={coedgeMaximum:R}:tolerance={tolerance:R}");

            if (body.TryGetVertexPoint(coedge.IsReversed ? body.Topology.GetEdge(coedge.EdgeId).EndVertexId : body.Topology.GetEdge(coedge.EdgeId).StartVertexId, out var start))
            {
                var parameter = coedge.IsReversed ? interval.End : interval.Start;
                var uv = binding.Pcurve.Evaluate(binding.SameSense ? parameter : interval.End - (parameter - interval.Start));
                var reconstructed = Evaluate(surface, uv);
                if (reconstructed is null || (reconstructed.Value - start).Length > tolerance)
                {
                    orientationValid = false;
                    diagnostics.Add($"surf-pcurve-invalid:coedge={coedge.Id.Value}:orientation");
                }
            }
        }

        var count = body.Bindings.PcurveBindings.Count();
        var valid = diagnostics.Count == 0 && maximum <= tolerance && domainValid && orientationValid;
        return new(valid, body.Topology.Edges.Count(), count, maximum, domainValid, orientationValid, diagnostics);
    }

    private static bool FiniteOrdered(ParameterInterval interval) => double.IsFinite(interval.Start) && double.IsFinite(interval.End) && interval.End >= interval.Start;

    private static Point3D? Evaluate(SurfaceGeometry surface, SurfaceParameterPoint uv) => surface.Kind switch
    {
        SurfaceGeometryKind.Plane => surface.Plane!.Value.Evaluate(uv.U, uv.V),
        SurfaceGeometryKind.Cylinder => surface.Cylinder!.Value.Evaluate(uv.U, uv.V),
        SurfaceGeometryKind.BSplineSurfaceWithKnots => surface.BSplineSurfaceWithKnots!.Evaluate(uv.U, uv.V),
        _ => null
    };

    private static Point3D? Evaluate(CurveGeometry curve, double parameter) => curve.Kind switch
    {
        CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(parameter),
        CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(parameter),
        CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate(parameter),
        CurveGeometryKind.Ellipse3 => curve.Ellipse3!.Value.Evaluate(parameter),
        CurveGeometryKind.Hyperbola3 => curve.Hyperbola3!.Value.Evaluate(parameter),
        _ => null
    };
}
