using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Surfacing;

internal static class SculptedHousingBrepBuilder
{
    internal sealed record BuildResult(BrepBody? Body, IReadOnlyList<SculptDiagnostic> Diagnostics);

    public static BuildResult Build(HousingConstruction construction)
    {
        var diagnostics = Validate(construction);
        if (diagnostics.Count > 0) return new(null, diagnostics);
        if (construction.ReplacementPatch is not null) return BuildReplacement(construction);

        var sections = construction.HasCrown
            ? new[] { Section(construction.Width, construction.Depth, 0d), Section(construction.Width, construction.Depth, construction.BaseHeight), Section(construction.CrownWidth, construction.CrownDepth, construction.FinalHeight) }
            : new[] { Section(construction.Width, construction.Depth, 0d), Section(construction.Width, construction.Depth, construction.BaseHeight) };
        var b = new TopologyBuilder();
        var vertices = new VertexId[sections.Length, 4];
        var points = new Dictionary<VertexId, Point3D>();
        for (var s = 0; s < sections.Length; s++)
        for (var i = 0; i < 4; i++) { vertices[s, i] = b.AddVertex(); points[vertices[s, i]] = sections[s][i]; }

        var profileEdges = new EdgeId[sections.Length, 4];
        for (var s = 0; s < sections.Length; s++)
        for (var i = 0; i < 4; i++) profileEdges[s, i] = b.AddEdge(vertices[s, i], vertices[s, (i + 1) % 4]);
        var risers = new EdgeId[sections.Length - 1, 4];
        for (var s = 0; s < sections.Length - 1; s++)
        for (var i = 0; i < 4; i++) risers[s, i] = b.AddEdge(vertices[s, i], vertices[s + 1, i]);

        var bottomHoleVertices = new VertexId[construction.Holes.Count];
        var topHoleVertices = new VertexId[construction.Holes.Count];
        var bottomHoleEdges = new EdgeId[construction.Holes.Count];
        var topHoleEdges = new EdgeId[construction.Holes.Count];
        var seamEdges = new EdgeId[construction.Holes.Count];
        for (var i = 0; i < construction.Holes.Count; i++)
        {
            var hole = construction.Holes[i];
            bottomHoleVertices[i] = b.AddVertex(); topHoleVertices[i] = b.AddVertex();
            var seamX = hole.CenterX + hole.Diameter / 2d;
            points[bottomHoleVertices[i]] = new(seamX, hole.CenterY, 0d);
            points[topHoleVertices[i]] = new(seamX, hole.CenterY, construction.FinalHeight);
            bottomHoleEdges[i] = b.AddEdge(bottomHoleVertices[i], bottomHoleVertices[i]);
            topHoleEdges[i] = b.AddEdge(topHoleVertices[i], topHoleVertices[i]);
            seamEdges[i] = b.AddEdge(bottomHoleVertices[i], topHoleVertices[i]);
        }

        var bottomLoops = new List<LoopId> { AddLoop(b, Enumerable.Range(0, 4).Select(i => new Use(profileEdges[0, i], false)).ToArray()) };
        for (var i = 0; i < construction.Holes.Count; i++) bottomLoops.Add(AddLoop(b, [new(bottomHoleEdges[i], true)]));
        var faces = new List<(FaceId Face, SurfaceGeometry Surface)>();
        faces.Add((b.AddFace(bottomLoops), SurfaceGeometry.FromPlane(new PlaneSurface(Point3D.Origin, Dir(0, 0, -1), Dir(1, 0, 0)))));

        var last = sections.Length - 1;
        var topLoops = new List<LoopId> { AddLoop(b, Enumerable.Range(0, 4).Select(i => new Use(profileEdges[last, i], true)).Reverse().ToArray()) };
        for (var i = 0; i < construction.Holes.Count; i++) topLoops.Add(AddLoop(b, [new(topHoleEdges[i], false)]));
        faces.Add((b.AddFace(topLoops), SurfaceGeometry.FromPlane(new PlaneSurface(new(0, 0, construction.FinalHeight), Dir(0, 0, 1), Dir(1, 0, 0)))));

        for (var s = 0; s < sections.Length - 1; s++)
        for (var i = 0; i < 4; i++)
        {
            var n = (i + 1) % 4;
            var loop = AddLoop(b, [new(profileEdges[s, i], false), new(risers[s, n], false), new(profileEdges[s + 1, i], true), new(risers[s, i], true)]);
            var p0 = sections[s][i]; var p1 = sections[s][n]; var p3 = sections[s + 1][i];
            var edge = p1 - p0; var rise = p3 - p0; var normal = edge.Cross(rise);
            faces.Add((b.AddFace([loop]), SurfaceGeometry.FromPlane(new PlaneSurface(p0, Direction3D.Create(normal), Direction3D.Create(edge)))));
        }

        for (var i = 0; i < construction.Holes.Count; i++)
        {
            var loop = AddLoop(b, [new(bottomHoleEdges[i], false), new(seamEdges[i], false), new(topHoleEdges[i], true), new(seamEdges[i], true)]);
            var h = construction.Holes[i];
            faces.Add((b.AddFace([loop]), SurfaceGeometry.FromCylinder(new CylinderSurface(new(h.CenterX, h.CenterY, 0), Dir(0, 0, 1), h.Diameter / 2d, Dir(1, 0, 0)))));
        }

        var shell = b.AddShell(faces.Select(x => x.Face).ToArray()); b.AddBody([shell]);
        var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var curveId = 1;
        foreach (var edge in b.Model.Edges.OrderBy(x => x.Id.Value))
        {
            var p0 = points[edge.StartVertexId]; var p1 = points[edge.EndVertexId];
            var holeIndex = Array.IndexOf(bottomHoleEdges, edge.Id); if (holeIndex < 0) holeIndex = Array.IndexOf(topHoleEdges, edge.Id);
            CurveGeometry curve; ParameterInterval interval;
            if (holeIndex >= 0)
            {
                var h = construction.Holes[holeIndex]; curve = CurveGeometry.FromCircle(new Circle3Curve(new(h.CenterX, h.CenterY, p0.Z), Dir(0, 0, 1), h.Diameter / 2d, Dir(1, 0, 0))); interval = new(0, 2 * double.Pi);
            }
            else { var vector = p1 - p0; curve = CurveGeometry.FromLine(new Line3Curve(p0, Direction3D.Create(vector))); interval = new(0, vector.Length); }
            var id = new CurveGeometryId(curveId++); geometry.AddCurve(id, curve); bindings.AddEdgeBinding(new(edge.Id, id, interval));
        }
        var surfaceId = 1;
        foreach (var (face, surface) in faces) { var id = new SurfaceGeometryId(surfaceId++); geometry.AddSurface(id, surface); bindings.AddFaceBinding(new(face, id)); }
        var body = new BrepBody(b.Model, geometry, bindings, points);
        var pcurves = BoundedPcurveBuilder.Populate(b.Model, geometry, bindings);
        if (!pcurves.IsSuccess) return new(null, pcurves.Diagnostics);
        var binding = BrepBindingValidator.Validate(body, true);
        if (!binding.IsSuccess) return new(null, binding.Diagnostics.Select(x => new SculptDiagnostic("sculpt-brep-binding-invalid", x.Message)).ToArray());
        var pcurveEvidence = BrepPcurveValidator.Validate(body, 1e-5, requireEveryCoedge: true);
        if (!pcurveEvidence.IsValid) return new(null, pcurveEvidence.Diagnostics.Select(message => new SculptDiagnostic("surf-pcurve-invalid", message)).ToArray());
        return new(body, []);
    }

    private static BuildResult BuildReplacement(HousingConstruction c)
    {
        var patch = c.ReplacementPatch!;
        var patchDiagnostics = patch.Validate();
        if (patchDiagnostics.Count > 0) return new(null, patchDiagnostics);

        var builder = new TopologyBuilder();
        var points = new Dictionary<VertexId, Point3D>();
        var curves = new Dictionary<EdgeId, (CurveGeometry Curve, ParameterInterval Interval)>();
        var faces = new List<(FaceId Face, SurfaceGeometry Surface, bool Reversed)>();
        var bottom = Section(c.Width, c.Depth, 0d);
        var top = Section(c.Width, c.Depth, c.BaseHeight);
        var vb = bottom.Select(p => { var id = builder.AddVertex(); points[id] = p; return id; }).ToArray();
        var vt = top.Select(p => { var id = builder.AddVertex(); points[id] = p; return id; }).ToArray();
        var eb = Enumerable.Range(0, 4).Select(i => AddLine(builder, curves, vb[i], vb[(i + 1) % 4], points)).ToArray();
        var et = Enumerable.Range(0, 4).Select(i => AddLine(builder, curves, vt[i], vt[(i + 1) % 4], points)).ToArray();
        var ev = Enumerable.Range(0, 4).Select(i => AddLine(builder, curves, vb[i], vt[i], points)).ToArray();

        var corners = new[]
        {
            patch.Evaluate(patch.ParameterDomain.UMin, patch.ParameterDomain.VMin),
            patch.Evaluate(patch.ParameterDomain.UMax, patch.ParameterDomain.VMin),
            patch.Evaluate(patch.ParameterDomain.UMax, patch.ParameterDomain.VMax),
            patch.Evaluate(patch.ParameterDomain.UMin, patch.ParameterDomain.VMax),
        };
        var coversWholeTop = corners.Zip(top).All(pair => (pair.First - pair.Second).Length <= 1e-6d);
        var vi = coversWholeTop ? vt : corners.Select(p => { var id = builder.AddVertex(); points[id] = p; return id; }).ToArray();
        var ei = coversWholeTop ? et : Enumerable.Range(0, 4).Select(i => AddLine(builder, curves, vi[i], vi[(i + 1) % 4], points)).ToArray();
        if (coversWholeTop && patch is BSplineSurfacePatch wholeSpline)
            BindExactSplineBoundaryCurves(curves, et, wholeSpline.Spline);

        var bhv = new VertexId[c.Holes.Count]; var thv = new VertexId[c.Holes.Count];
        var bhe = new EdgeId[c.Holes.Count]; var the = new EdgeId[c.Holes.Count]; var seams = new EdgeId[c.Holes.Count];
        for (var i = 0; i < c.Holes.Count; i++)
        {
            var h = c.Holes[i]; var seamX = h.CenterX + h.Diameter / 2d;
            bhv[i] = builder.AddVertex(); thv[i] = builder.AddVertex();
            points[bhv[i]] = new(seamX, h.CenterY, 0d); points[thv[i]] = new(seamX, h.CenterY, c.BaseHeight);
            bhe[i] = AddCircle(builder, curves, bhv[i], h, 0d); the[i] = AddCircle(builder, curves, thv[i], h, c.BaseHeight);
            seams[i] = AddLine(builder, curves, bhv[i], thv[i], points);
        }

        var bottomLoops = new List<LoopId> { AddLoop(builder, eb.Select(x => new Use(x, false)).ToArray()) };
        for (var i = 0; i < c.Holes.Count; i++) bottomLoops.Add(AddLoop(builder, [new(bhe[i], true)]));
        faces.Add((builder.AddFace(bottomLoops), SurfaceGeometry.FromPlane(new PlaneSurface(Point3D.Origin, Dir(0, 0, -1), Dir(1, 0, 0))), false));

        if (!coversWholeTop)
        {
            var frameLoops = new List<LoopId>
            {
                AddLoop(builder, et.Select(x => new Use(x, true)).Reverse().ToArray()),
                AddLoop(builder, ei.Select(x => new Use(x, false)).ToArray()),
            };
            for (var i = 0; i < c.Holes.Count; i++) frameLoops.Add(AddLoop(builder, [new(the[i], false)]));
            faces.Add((builder.AddFace(frameLoops), SurfaceGeometry.FromPlane(new PlaneSurface(new(0, 0, c.BaseHeight), Dir(0, 0, 1), Dir(1, 0, 0))), false));
        }

        for (var i = 0; i < 4; i++)
        {
            var n = (i + 1) % 4;
            var loop = AddLoop(builder, [new(eb[i], false), new(ev[n], false), new(et[i], true), new(ev[i], true)]);
            var edge = bottom[n] - bottom[i]; var rise = top[i] - bottom[i];
            faces.Add((builder.AddFace([loop]), SurfaceGeometry.FromPlane(new PlaneSurface(bottom[i], Direction3D.Create(edge.Cross(rise)), Direction3D.Create(edge))), false));
        }

        var patchLoops = new List<LoopId> { AddLoop(builder, ei.Select(x => new Use(x, true)).Reverse().ToArray()) };
        if (coversWholeTop)
            for (var i = 0; i < c.Holes.Count; i++) patchLoops.Add(AddLoop(builder, [new(the[i], false)]));
        faces.Add((builder.AddFace(patchLoops), patch.Support, patch.ReversedOrientation));
        for (var i = 0; i < c.Holes.Count; i++)
        {
            var loop = AddLoop(builder, [new(bhe[i], false), new(seams[i], false), new(the[i], true), new(seams[i], true)]);
            var h = c.Holes[i];
            faces.Add((builder.AddFace([loop]), SurfaceGeometry.FromCylinder(new CylinderSurface(new(h.CenterX, h.CenterY, 0d), Dir(0, 0, 1), h.Diameter / 2d, Dir(1, 0, 0))), false));
        }

        var shell = builder.AddShell(faces.Select(x => x.Face).ToArray()); builder.AddBody([shell]);
        var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var curveId = 1;
        foreach (var pair in curves.OrderBy(x => x.Key.Value))
        {
            var id = new CurveGeometryId(curveId++); geometry.AddCurve(id, pair.Value.Curve); bindings.AddEdgeBinding(new(pair.Key, id, pair.Value.Interval));
        }
        var surfaceId = 1;
        foreach (var face in faces)
        {
            var id = new SurfaceGeometryId(surfaceId++); geometry.AddSurface(id, face.Surface); bindings.AddFaceBinding(new(face.Face, id, face.Reversed));
        }
        var body = new BrepBody(builder.Model, geometry, bindings, points);
        var pcurves = BoundedPcurveBuilder.Populate(builder.Model, geometry, bindings);
        if (!pcurves.IsSuccess) return new(null, pcurves.Diagnostics);
        var validation = BrepBindingValidator.Validate(body, true);
        if (!validation.IsSuccess) return new(null, validation.Diagnostics.Select(x => new SculptDiagnostic("sculpt-brep-binding-invalid", x.Message)).ToArray());
        var pcurveEvidence = BrepPcurveValidator.Validate(body, 1e-5, requireEveryCoedge: true);
        return pcurveEvidence.IsValid ? new(body, []) : new(null, pcurveEvidence.Diagnostics.Select(message => new SculptDiagnostic("surf-pcurve-invalid", message)).ToArray());
    }

    private static EdgeId AddLine(TopologyBuilder builder, IDictionary<EdgeId, (CurveGeometry, ParameterInterval)> curves, VertexId start, VertexId end, IReadOnlyDictionary<VertexId, Point3D> points)
    {
        var edge = builder.AddEdge(start, end); var vector = points[end] - points[start];
        curves[edge] = (CurveGeometry.FromLine(new Line3Curve(points[start], Direction3D.Create(vector))), new(0d, vector.Length));
        return edge;
    }

    private static EdgeId AddCircle(TopologyBuilder builder, IDictionary<EdgeId, (CurveGeometry, ParameterInterval)> curves, VertexId seam, HousingHole hole, double z)
    {
        var edge = builder.AddEdge(seam, seam);
        curves[edge] = (CurveGeometry.FromCircle(new Circle3Curve(new(hole.CenterX, hole.CenterY, z), Dir(0, 0, 1), hole.Diameter / 2d, Dir(1, 0, 0))), new(0d, 2d * double.Pi));
        return edge;
    }

    private static void BindExactSplineBoundaryCurves(IDictionary<EdgeId, (CurveGeometry, ParameterInterval)> curves, IReadOnlyList<EdgeId> edges, BSplineSurfaceWithKnots spline)
    {
        var south = spline.ControlPoints.Select(row => row[0]).ToArray();
        var east = spline.ControlPoints[^1].ToArray();
        var north = spline.ControlPoints.Select(row => row[^1]).Reverse().ToArray();
        var west = spline.ControlPoints[0].Reverse().ToArray();
        curves[edges[0]] = (CurveGeometry.FromBSpline(new BSpline3Curve(spline.DegreeU, south, spline.KnotMultiplicitiesU, spline.KnotValuesU, "UNSPECIFIED", false, false, spline.KnotSpec)), new(spline.DomainStartU, spline.DomainEndU));
        curves[edges[1]] = (CurveGeometry.FromBSpline(new BSpline3Curve(spline.DegreeV, east, spline.KnotMultiplicitiesV, spline.KnotValuesV, "UNSPECIFIED", false, false, spline.KnotSpec)), new(spline.DomainStartV, spline.DomainEndV));
        curves[edges[2]] = (CurveGeometry.FromBSpline(new BSpline3Curve(spline.DegreeU, north, spline.KnotMultiplicitiesU.Reverse().ToArray(), ReverseKnots(spline.KnotValuesU), "UNSPECIFIED", false, false, spline.KnotSpec)), new(spline.DomainStartU, spline.DomainEndU));
        curves[edges[3]] = (CurveGeometry.FromBSpline(new BSpline3Curve(spline.DegreeV, west, spline.KnotMultiplicitiesV.Reverse().ToArray(), ReverseKnots(spline.KnotValuesV), "UNSPECIFIED", false, false, spline.KnotSpec)), new(spline.DomainStartV, spline.DomainEndV));
    }

    private static IReadOnlyList<double> ReverseKnots(IReadOnlyList<double> knots)
    {
        var sum = knots[0] + knots[^1];
        return knots.Reverse().Select(value => sum - value).ToArray();
    }

    private static List<SculptDiagnostic> Validate(HousingConstruction c)
    {
        var d = new List<SculptDiagnostic>();
        if (!Positive(c.Width) || !Positive(c.Depth) || !Positive(c.BaseHeight)) d.Add(new("sculpt-housing-invalid", "Housing width, depth, and base height must be positive and finite."));
        if (!Positive(c.CrownWidth) || !Positive(c.CrownDepth) || c.CrownWidth > c.Width || c.CrownDepth > c.Depth) d.Add(new("sculpt-target-domain-invalid", "Crown region must be positive and contained by the housing footprint."));
        if (!double.IsFinite(c.CrownOffset) || c.FinalHeight <= 0d) d.Add(new("sculpt-self-intersection", "Offset crosses or collapses the bottom mounting plane."));
        foreach (var h in c.Holes)
        {
            var r = h.Diameter / 2d;
            if (!Positive(h.Diameter) || Math.Abs(h.CenterX) + r >= c.Width / 2d || Math.Abs(h.CenterY) + r >= c.Depth / 2d)
                d.Add(new("sculpt-hole-pattern-outside-body", $"Mounting hole '{h.StableId}' must remain inside the housing footprint.", h.StableId));
            if (c.ReplacementPatch is null && (Math.Abs(h.CenterX) + r >= c.CrownWidth / 2d || Math.Abs(h.CenterY) + r >= c.CrownDepth / 2d))
                d.Add(new("sculpt-hole-pattern-outside-crown", $"Mounting hole '{h.StableId}' must remain inside the bounded crown support.", h.StableId));
            if (c.ReplacementPatch is not null && (c.CrownWidth < c.Width - 1e-9d || c.CrownDepth < c.Depth - 1e-9d)
                && Math.Abs(h.CenterX) - r < c.CrownWidth / 2d && Math.Abs(h.CenterY) - r < c.CrownDepth / 2d)
                d.Add(new("surf-patch-intersects-protected-hole", $"Replacement patch intersects protected mounting hole '{h.StableId}'. Move the hole or reduce the patch boundary.", h.StableId));
        }
        for (var i = 0; i < c.Holes.Count; i++) for (var j = i + 1; j < c.Holes.Count; j++)
        { var a = c.Holes[i]; var h = c.Holes[j]; if (Math.Sqrt(Math.Pow(a.CenterX - h.CenterX, 2) + Math.Pow(a.CenterY - h.CenterY, 2)) <= (a.Diameter + h.Diameter) / 2d) d.Add(new("sculpt-self-intersection", $"Mounting holes '{a.StableId}' and '{h.StableId}' overlap.")); }
        return d;
    }
    private static bool Positive(double value) => double.IsFinite(value) && value > 1e-9;
    private static Point3D[] Section(double width, double depth, double z) { var x = width / 2d; var y = depth / 2d; return [new(-x, -y, z), new(x, -y, z), new(x, y, z), new(-x, y, z)]; }
    private static Direction3D Dir(double x, double y, double z) => Direction3D.Create(new Vector3D(x, y, z));
    private static LoopId AddLoop(TopologyBuilder b, IReadOnlyList<Use> uses)
    {
        var loop = b.AllocateLoopId(); var ids = uses.Select(_ => b.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < ids.Length; i++) b.AddCoedge(new(ids[i], uses[i].Edge, loop, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reversed));
        b.AddLoop(new Loop(loop, ids)); return loop;
    }
    private readonly record struct Use(EdgeId Edge, bool Reversed);
}
