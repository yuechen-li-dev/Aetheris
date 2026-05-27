using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

internal sealed record ProfileHoleLoop2D(double CenterX, double CenterY, double Radius);

internal sealed record ProfileHoleExtrudeRequest(
    double Width,
    double Depth,
    double Height,
    IReadOnlyList<ProfileHoleLoop2D> Holes);

internal enum ProfileHoleExtrudeStatus
{
    Succeeded,
    Rejected,
    Failed
}

internal sealed record ProfileHoleExtrudeResult(
    ProfileHoleExtrudeStatus Status,
    BrepBody? Body,
    IReadOnlyList<string> Diagnostics);

internal static class ProfileHoleExtrudeEmitter
{
    public static ProfileHoleExtrudeResult TryEmit(ProfileHoleExtrudeRequest request)
    {
        var d = new List<string> { "v2-v1-profile-hole-extrude-attempted" };
        if (!TryValidate(request, d))
        {
            return new(ProfileHoleExtrudeStatus.Rejected, null, d);
        }

        d.Add("v2-v1-profile-hole-extrude-accepted");
        d.Add("v2-v1-profile-hole-extrude-no-3d-boolean-subtract");
        var built = BuildBody(request);
        if (!built.IsSuccess || built.Body is null)
        {
            d.Add($"v2-v1-profile-hole-extrude-failed:{built.Diagnostic}");
            return new(ProfileHoleExtrudeStatus.Failed, null, d);
        }

        d.Add("v2-v1-profile-hole-extrude-succeeded");
        return new(ProfileHoleExtrudeStatus.Succeeded, built.Body, d);
    }

    private static bool TryValidate(ProfileHoleExtrudeRequest req, List<string> d)
    {
        const double tol = 1e-9;
        if (!double.IsFinite(req.Width) || !double.IsFinite(req.Depth) || !double.IsFinite(req.Height) || req.Width <= tol || req.Depth <= tol || req.Height <= tol)
        {
            d.Add("v2-v1-profile-hole-extrude-rejected:invalid-outer-or-height");
            return false;
        }

        var hw = req.Width / 2d;
        var hh = req.Depth / 2d;
        for (var i = 0; i < req.Holes.Count; i++)
        {
            var h = req.Holes[i];
            if (!double.IsFinite(h.CenterX) || !double.IsFinite(h.CenterY) || !double.IsFinite(h.Radius) || h.Radius <= tol)
            {
                d.Add($"v2-v1-profile-hole-extrude-rejected:invalid-hole-radius[{i}]");
                return false;
            }

            if (h.CenterX - h.Radius <= -hw + tol || h.CenterX + h.Radius >= hw - tol || h.CenterY - h.Radius <= -hh + tol || h.CenterY + h.Radius >= hh - tol)
            {
                d.Add($"v2-v1-profile-hole-extrude-rejected:hole-outside-or-touches-boundary[{i}]");
                return false;
            }

            for (var j = i + 1; j < req.Holes.Count; j++)
            {
                var k = req.Holes[j];
                var dx = h.CenterX - k.CenterX;
                var dy = h.CenterY - k.CenterY;
                if ((dx * dx) + (dy * dy) <= Math.Pow(h.Radius + k.Radius, 2d) + tol)
                {
                    d.Add($"v2-v1-profile-hole-extrude-rejected:holes-overlap[{i},{j}]");
                    return false;
                }
            }
        }

        return true;
    }

    private static (bool IsSuccess, BrepBody? Body, string Diagnostic) BuildBody(ProfileHoleExtrudeRequest request)
    {
        var minX = -request.Width / 2d;
        var maxX = request.Width / 2d;
        var minY = -request.Depth / 2d;
        var maxY = request.Depth / 2d;
        var z0 = -request.Height / 2d;
        var z1 = request.Height / 2d;

        var b = new TopologyBuilder();
        var ob = new[] { b.AddVertex(), b.AddVertex(), b.AddVertex(), b.AddVertex() };
        var ot = new[] { b.AddVertex(), b.AddVertex(), b.AddVertex(), b.AddVertex() };
        var holeCount = request.Holes.Count;
        var hb = new VertexId[holeCount];
        var ht = new VertexId[holeCount];
        for (var i = 0; i < holeCount; i++) { hb[i] = b.AddVertex(); ht[i] = b.AddVertex(); }

        var be = new[] { b.AddEdge(ob[0], ob[1]), b.AddEdge(ob[1], ob[2]), b.AddEdge(ob[2], ob[3]), b.AddEdge(ob[3], ob[0]) };
        var te = new[] { b.AddEdge(ot[0], ot[1]), b.AddEdge(ot[1], ot[2]), b.AddEdge(ot[2], ot[3]), b.AddEdge(ot[3], ot[0]) };
        var se = new[] { b.AddEdge(ob[0], ot[0]), b.AddEdge(ob[1], ot[1]), b.AddEdge(ob[2], ot[2]), b.AddEdge(ob[3], ot[3]) };
        var hbe = new EdgeId[holeCount]; var hte = new EdgeId[holeCount]; var hse = new EdgeId[holeCount];
        for (var i = 0; i < holeCount; i++) { hbe[i] = b.AddEdge(hb[i], hb[i]); hte[i] = b.AddEdge(ht[i], ht[i]); hse[i] = b.AddEdge(hb[i], ht[i]); }

        var bottomLoops = new List<LoopId> { AddLoop(b, [Use.F(be[0]), Use.F(be[1]), Use.F(be[2]), Use.F(be[3])]) };
        for (var i = 0; i < holeCount; i++) bottomLoops.Add(AddLoop(b, [Use.R(hbe[i])]));
        var bottomFace = b.AddFace(bottomLoops);

        var topLoops = new List<LoopId> { AddLoop(b, [Use.R(te[0]), Use.R(te[1]), Use.R(te[2]), Use.R(te[3])]) };
        for (var i = 0; i < holeCount; i++) topLoops.Add(AddLoop(b, [Use.F(hte[i])]));
        var topFace = b.AddFace(topLoops);

        var faces = new List<FaceId> { bottomFace, topFace };
        for (var i = 0; i < 4; i++)
        {
            var n = (i + 1) % 4;
            faces.Add(b.AddFace([AddLoop(b, [Use.F(be[i]), Use.F(se[n]), Use.R(te[i]), Use.R(se[i])])]));
        }
        for (var i = 0; i < holeCount; i++) faces.Add(b.AddFace([AddLoop(b, [Use.F(hbe[i]), Use.F(hse[i]), Use.R(hte[i]), Use.R(hse[i])])]));

        var shell = b.AddShell(faces);
        b.AddBody([shell]);

        var g = new BrepGeometryStore();
        var bind = new BrepBindingModel();
        var map = new Dictionary<VertexId, Point3D>
        {
            [ob[0]] = new(minX, minY, z0), [ob[1]] = new(maxX, minY, z0), [ob[2]] = new(maxX, maxY, z0), [ob[3]] = new(minX, maxY, z0),
            [ot[0]] = new(minX, minY, z1), [ot[1]] = new(maxX, minY, z1), [ot[2]] = new(maxX, maxY, z1), [ot[3]] = new(minX, maxY, z1)
        };
        for (var i = 0; i < holeCount; i++)
        {
            var hole = request.Holes[i];
            map[hb[i]] = new(hole.CenterX, hole.CenterY, z0);
            map[ht[i]] = new(hole.CenterX, hole.CenterY, z1);
        }

        var cid = 1;
        foreach (var e in b.Model.Edges.OrderBy(x => x.Id.Value))
        {
            var p0 = map[e.StartVertexId];
            var p1 = map[e.EndVertexId];
            CurveGeometry curve;
            if (e.StartVertexId == e.EndVertexId)
            {
                var hIdx = Array.FindIndex(hb, v => v == e.StartVertexId);
                if (hIdx < 0) hIdx = Array.FindIndex(ht, v => v == e.StartVertexId);
                curve = CurveGeometry.FromCircle(new Circle3Curve(p0, Direction3D.Create(new Vector3D(0, 0, 1)), request.Holes[hIdx].Radius, Direction3D.Create(new Vector3D(1, 0, 0))));
                bind.AddEdgeBinding(new EdgeGeometryBinding(e.Id, new CurveGeometryId(cid), new ParameterInterval(0, 2 * Math.PI)));
            }
            else
            {
                curve = CurveGeometry.FromLine(new Line3Curve(p0, Direction3D.Create(p1 - p0)));
                bind.AddEdgeBinding(new EdgeGeometryBinding(e.Id, new CurveGeometryId(cid), new ParameterInterval(0, (p1 - p0).Length)));
            }
            g.AddCurve(new CurveGeometryId(cid++), curve);
        }

        g.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0, 0, z0), Direction3D.Create(new Vector3D(0, 0, -1)), Direction3D.Create(new Vector3D(1, 0, 0)))));
        g.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0, 0, z1), Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)))));
        g.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(minX, minY, z0), Direction3D.Create(new Vector3D(0, -1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)))));
        g.AddSurface(new SurfaceGeometryId(4), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(maxX, minY, z0), Direction3D.Create(new Vector3D(1, 0, 0)), Direction3D.Create(new Vector3D(0, 0, 1)))));
        g.AddSurface(new SurfaceGeometryId(5), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(maxX, maxY, z0), Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)))));
        g.AddSurface(new SurfaceGeometryId(6), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(minX, maxY, z0), Direction3D.Create(new Vector3D(-1, 0, 0)), Direction3D.Create(new Vector3D(0, 0, 1)))));
        for (var i = 0; i < holeCount; i++)
        {
            var h = request.Holes[i];
            g.AddSurface(new SurfaceGeometryId(7 + i), SurfaceGeometry.FromCylinder(new CylinderSurface(new Point3D(h.CenterX, h.CenterY, z0), Direction3D.Create(new Vector3D(0, 0, 1)), h.Radius, Direction3D.Create(new Vector3D(1, 0, 0)))));
        }

        bind.AddFaceBinding(new FaceGeometryBinding(faces[0], new SurfaceGeometryId(1)));
        bind.AddFaceBinding(new FaceGeometryBinding(faces[1], new SurfaceGeometryId(2)));
        bind.AddFaceBinding(new FaceGeometryBinding(faces[2], new SurfaceGeometryId(3)));
        bind.AddFaceBinding(new FaceGeometryBinding(faces[3], new SurfaceGeometryId(4)));
        bind.AddFaceBinding(new FaceGeometryBinding(faces[4], new SurfaceGeometryId(5)));
        bind.AddFaceBinding(new FaceGeometryBinding(faces[5], new SurfaceGeometryId(6)));
        for (var i = 0; i < holeCount; i++) bind.AddFaceBinding(new FaceGeometryBinding(faces[6 + i], new SurfaceGeometryId(7 + i)));
        return (true, new BrepBody(b.Model, g, bind, map), string.Empty);
    }

    private static LoopId AddLoop(TopologyBuilder b, IReadOnlyList<Use> uses)
    {
        var lid = b.AllocateLoopId();
        var cids = uses.Select(_ => b.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < uses.Count; i++)
        {
            var n = cids[(i + 1) % cids.Length];
            var p = cids[(i + cids.Length - 1) % cids.Length];
            b.AddCoedge(new Coedge(cids[i], uses[i].Edge, lid, n, p, uses[i].Rev));
        }
        b.AddLoop(new Loop(lid, cids));
        return lid;
    }

    private readonly record struct Use(EdgeId Edge, bool Rev)
    {
        public static Use F(EdgeId e) => new(e, false);
        public static Use R(EdgeId e) => new(e, true);
    }
}
