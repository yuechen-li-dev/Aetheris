using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// M06 scope: topology reference integrity + topology-to-geometry binding reference integrity.
/// Geometric consistency checks are intentionally deferred.
/// </summary>
public static class BrepBindingValidator
{
    public static KernelResult<bool> Validate(BrepBody body, bool requireAllEdgeAndFaceBindings = true)
    {
        var diagnostics = new List<KernelDiagnostic>();

        var topologyResult = TopologyGraphValidator.Validate(body.Topology);
        diagnostics.AddRange(topologyResult.Diagnostics);

        foreach (var edgeBinding in body.Bindings.EdgeBindings)
        {
            if (!body.Topology.TryGetEdge(edgeBinding.EdgeId, out _))
            {
                diagnostics.Add(Error($"Edge binding references missing edge {edgeBinding.EdgeId.Value}."));
            }

            if (!body.Geometry.TryGetCurve(edgeBinding.CurveGeometryId, out _))
            {
                diagnostics.Add(Error($"Edge {edgeBinding.EdgeId.Value} binding references missing curve geometry {edgeBinding.CurveGeometryId.Value}."));
            }

            if (edgeBinding.TrimInterval is { } trimInterval)
            {
                if (!double.IsFinite(trimInterval.Start) || !double.IsFinite(trimInterval.End) || trimInterval.End < trimInterval.Start)
                {
                    diagnostics.Add(Error($"Edge {edgeBinding.EdgeId.Value} has invalid trim interval [{trimInterval.Start}, {trimInterval.End}]."));
                }
            }
        }

        foreach (var faceBinding in body.Bindings.FaceBindings)
        {
            if (!body.Topology.TryGetFace(faceBinding.FaceId, out _))
            {
                diagnostics.Add(Error($"Face binding references missing face {faceBinding.FaceId.Value}."));
            }

            if (!body.Geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out _))
            {
                diagnostics.Add(Error($"Face {faceBinding.FaceId.Value} binding references missing surface geometry {faceBinding.SurfaceGeometryId.Value}."));
            }
        }

        if (requireAllEdgeAndFaceBindings)
        {
            foreach (var edge in body.Topology.Edges)
            {
                if (!body.Bindings.TryGetEdgeBinding(edge.Id, out _))
                {
                    diagnostics.Add(Error($"Edge {edge.Id.Value} is missing a geometry binding."));
                }
            }

            foreach (var face in body.Topology.Faces)
            {
                if (!body.Bindings.TryGetFaceBinding(face.Id, out _))
                {
                    diagnostics.Add(Error($"Face {face.Id.Value} is missing a geometry binding."));
                }
            }
        }

        return diagnostics.Any(d => d.Severity == KernelDiagnosticSeverity.Error)
            ? KernelResult<bool>.Failure(diagnostics)
            : KernelResult<bool>.Success(true, diagnostics);
    }

    private static KernelDiagnostic Error(string message) =>
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message);
}
