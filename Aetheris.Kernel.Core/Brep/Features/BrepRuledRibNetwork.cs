using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>Corresponding polygon vertices certified by the arrangement planner.
/// Each side retains the authored rib which supplies its supporting plane.</summary>
public sealed record RibLoftLoop(IReadOnlyList<ProfilePoint2D> Lower, IReadOnlyList<ProfilePoint2D> Upper,
    IReadOnlyList<string> SideOwners);
public sealed record RibLoftRegion(IReadOnlyList<RibLoftLoop> Loops);
public sealed record RibLoftBody(BrepBody Body, IReadOnlyDictionary<FaceId, string> SideOwners);

public static class BrepRuledRibNetwork
{
    public static KernelResult<RibLoftBody> Build(IReadOnlyList<RibLoftRegion> regions, double height)
    {
        KernelResult<RibLoftBody> Fail(string message) => KernelResult<RibLoftBody>.Failure([
            new(Diagnostics.KernelDiagnosticCode.ValidationFailed,Diagnostics.KernelDiagnosticSeverity.Error,"rib-loft-invalid: "+message,"Brep.RibNetwork")]);
        if (!double.IsFinite(height) || height <= 0 || regions.Count == 0) return Fail("Positive height and nonempty regions are required.");
        var b = new TopologyBuilder(); var g = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>(); var faces = new List<FaceId>(); var owners = new Dictionary<FaceId, string>();
        var curve = 0; var surface = 0;
        VertexId Vertex(ProfilePoint2D p, double z)
        {
            var point = new Point3D(p.X, p.Y, z);
            if (!double.IsFinite(p.X + p.Y)) throw new ArgumentException("Non-finite loft point.");
            var id = b.AddVertex(); points[id] = point; return id;
        }
        EdgeId Line(VertexId a, VertexId c)
        {
            var delta = points[c] - points[a]; var edge = b.AddEdge(a, c); var id = new CurveGeometryId(++curve);
            g.AddCurve(id, CurveGeometry.FromLine(new(points[a], Direction3D.Create(delta))));
            bindings.AddEdgeBinding(new(edge, id, new(0, delta.Length))); return edge;
        }
        LoopId Loop(IReadOnlyList<(EdgeId Edge, bool Reverse)> uses)
        {
            var id = b.AllocateLoopId(); var coedges = uses.Select(_ => b.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < coedges.Length; i++) b.AddCoedge(new(coedges[i], uses[i].Edge, id, coedges[(i + 1) % coedges.Length], coedges[(i + coedges.Length - 1) % coedges.Length], uses[i].Reverse));
            b.AddLoop(new Loop(id, coedges)); return id;
        }
        FaceId Face(IReadOnlyList<LoopId> loops, PlaneSurface plane)
        {
            var f = b.AddFace(loops); faces.Add(f); var id = new SurfaceGeometryId(++surface); g.AddSurface(id, SurfaceGeometry.FromPlane(plane)); bindings.AddFaceBinding(new(f, id));
            for (var i = 0; i < loops.Count; i++) bindings.AddFaceBoundaryRoleBinding(new(f, loops[i], i == 0 ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner));
            return f;
        }
        foreach (var region in regions)
        {
            var lowerCaps = new List<LoopId>(); var upperCaps = new List<LoopId>();
            foreach (var loop in region.Loops)
            {
                var n = loop.Lower.Count;
                if (n < 3 || loop.Upper.Count != n || loop.SideOwners.Count != n) return Fail("Section correspondence is incomplete.");
                var lo = loop.Lower.Select(p => Vertex(p, 0)).ToArray(); var hi = loop.Upper.Select(p => Vertex(p, height)).ToArray();
                var lower = Enumerable.Range(0, n).Select(i => Line(lo[i], lo[(i + 1) % n])).ToArray();
                var upper = Enumerable.Range(0, n).Select(i => Line(hi[i], hi[(i + 1) % n])).ToArray();
                var links = Enumerable.Range(0, n).Select(i => Line(lo[i], hi[i])).ToArray();
                lowerCaps.Add(Loop(lower.Select(e => (e, true)).Reverse().ToArray())); upperCaps.Add(Loop(upper.Select(e => (e, false)).ToArray()));
                for (var i = 0; i < n; i++)
                {
                    var j = (i + 1) % n; var origin = points[lo[i]]; var u = points[lo[j]] - origin;
                    var normal = Direction3D.Create(u.Cross(points[hi[i]] - origin));
                    if (double.Abs((points[hi[j]] - origin).Dot(normal.ToVector())) > 1e-7) return Fail("A ruled side is not planar.");
                    var f = Face([Loop([(lower[i], false), (links[j], false), (upper[i], true), (links[i], true)])], new(origin, normal, Direction3D.Create(u)));
                    owners[f] = loop.SideOwners[i];
                }
            }
            Face(lowerCaps, new(new(0, 0, 0), Direction3D.Create(new(0, 0, -1)), BrepPlanarPathFeature.X));
            Face(upperCaps, new(new(0, 0, height), BrepPlanarPathFeature.Z, BrepPlanarPathFeature.X));
        }
        b.AddBody([b.AddShell(faces)]);
        var body = new BrepBody(b.Model, g, bindings, points);
        var valid = BrepBindingValidator.Validate(body, true); if (!valid.IsSuccess) return KernelResult<RibLoftBody>.Failure(valid.Diagnostics);
        return KernelResult<RibLoftBody>.Success(new(body, owners));
    }
}
