using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Surfacing;

/// <summary>One support replacement with retained topology/curves. No housing reconstruction.</summary>
public static class BoundedFaceGraft
{
    public sealed record Result(BrepBody? Body, BrepPcurveEvidence? Pcurves, IReadOnlyList<SculptDiagnostic> Diagnostics);

    public static Result Apply(BrepBody input, FaceId face, BSplineSurfacePatch patch, double tolerance, ResolvedFaceOrientation orientation)
    {
        var diagnostics = patch.Validate();
        if (diagnostics.Count != 0) return new(null, null, diagnostics);
        if (!input.Topology.TryGetFace(face, out _)) return new(null, null, [new("surf-face-unresolved", "Face handle is not in the current body.")]);
        var geometry = new BrepGeometryStore();
        foreach (var item in input.Geometry.Curves) geometry.AddCurve(item.Key, item.Value);
        foreach (var item in input.Geometry.Surfaces) geometry.AddSurface(item.Key, item.Value);
        var surface = new SurfaceGeometryId(input.Geometry.Surfaces.Select(i => i.Key.Value).DefaultIfEmpty().Max() + 1);
        geometry.AddSurface(surface, patch.Support);
        var bindings = new BrepBindingModel();
        foreach (var item in input.Bindings.EdgeBindings) bindings.AddEdgeBinding(item);
        foreach (var item in input.Bindings.FaceBindings)
            bindings.AddFaceBinding(item.FaceId == face ? new(item.FaceId, surface, orientation, item.SourceStepEntityId) : item);
        var points = input.Topology.Vertices.ToDictionary(v => v.Id, v =>
            input.TryGetVertexPoint(v.Id, out var p) ? p : throw new InvalidOperationException("Unbound graft vertex."));
        // A support edit invalidates any cached analytic Boolean decomposition.
        // Shell membership is unchanged; the edited BRep is the new authority.
        var body = new BrepBody(input.Topology, geometry, bindings, points, null, input.ShellRepresentation);
        var built = BoundedPcurveBuilder.Populate(body.Topology, geometry, bindings, tolerance);
        if (!built.IsSuccess) return new(null, null, built.Diagnostics);
        var pcurves = BrepPcurveValidator.Validate(body, tolerance, requireEveryCoedge: true);
        if (!pcurves.IsValid) return new(null, pcurves, [new("surf-pcurve-invalid", string.Join(" | ", pcurves.Diagnostics))]);
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return new(null, pcurves, [new("surf-face-graft-invalid", string.Join(" | ", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => d.Message)))]);
        return new(body, pcurves, []);
    }
}
