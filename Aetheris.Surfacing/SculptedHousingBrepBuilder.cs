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
        var binding = BrepBindingValidator.Validate(body, true);
        if (!binding.IsSuccess) return new(null, binding.Diagnostics.Select(x => new SculptDiagnostic("sculpt-brep-binding-invalid", x.Message)).ToArray());
        return new(body, []);
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
            if (!Positive(h.Diameter) || Math.Abs(h.CenterX) + r >= c.CrownWidth / 2d || Math.Abs(h.CenterY) + r >= c.CrownDepth / 2d)
                d.Add(new("sculpt-hole-pattern-outside-crown", $"Mounting hole '{h.StableId}' must remain inside the bounded crown support.", h.StableId));
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
