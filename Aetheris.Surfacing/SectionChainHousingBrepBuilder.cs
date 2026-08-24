using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Surfacing;

/// <summary>
/// SectionChain-specific shared-topology construction for the admitted X3b planar housing lane.
/// It never asks the generic Boolean classifier to rediscover attachment or penetration intent.
/// </summary>
internal static class SectionChainHousingBrepBuilder
{
    internal sealed record BuildResult(BrepBody? Body, SectionChain? RealizedChain, SectionChainPcurveEvidence? ToolPcurves,
        IReadOnlyList<SculptDiagnostic> Diagnostics);

    public static BuildResult BuildAddEast(HousingConstruction construction, SectionChain authored)
    {
        var placed = PlaceRelativeToEast(authored, construction.Width / 2d);
        var diagnostics = ValidateCommon(construction, placed).ToList();
        if (placed.StartTermination != SectionTermination.Open || placed.EndTermination != SectionTermination.Cap)
            diagnostics.Add(new("section-chain-add-termination-invalid", "The admitted east-support additive lane requires Open at the attached start and Cap at the free end."));
        if (diagnostics.Count == 0)
        {
            var expected = new[]
            {
                new Point3D(construction.Width / 2d, -construction.Depth / 2d, 0d),
                new Point3D(construction.Width / 2d, construction.Depth / 2d, 0d),
                new Point3D(construction.Width / 2d, construction.Depth / 2d, construction.BaseHeight),
                new Point3D(construction.Width / 2d, -construction.Depth / 2d, construction.BaseHeight),
            };
            if (!Matches(Points(placed.Sections[0]), expected))
                diagnostics.Add(new("section-chain-add-not-attached", "The attached terminal profile must exactly correspond to the complete HousingSideEast boundary in the admitted lane."));
            if (placed.Sections.Skip(1).SelectMany(Points).Any(point => point.X <= construction.Width / 2d + 1e-8d))
                diagnostics.Add(new("section-chain-add-remote-intersection", "Every non-terminal additive section must remain strictly outside HousingSideEast; a second body intersection was detected."));
        }
        return diagnostics.Count > 0 ? new(null, placed, null, diagnostics) : Build(construction, placed, additive: true);
    }

    public static BuildResult BuildRemoveThroughX(HousingConstruction construction, SectionChain chain)
    {
        var diagnostics = ValidateCommon(construction, chain).ToList();
        if (chain.StartTermination != SectionTermination.Open || chain.EndTermination != SectionTermination.Open)
            diagnostics.Add(new("section-chain-remove-termination-invalid", "The admitted through-duct lane requires Open terminations at both housing support faces."));
        if (diagnostics.Count == 0)
        {
            var sections = chain.Sections.Select(Points).ToArray();
            var west = -construction.Width / 2d; var east = construction.Width / 2d;
            if (sections[0].Any(point => Math.Abs(point.X - west) > 1e-6d)
                || sections[^1].Any(point => Math.Abs(point.X - east) > 1e-6d))
                diagnostics.Add(new("section-chain-remove-not-through", "RemoveSectionChain must terminate exactly on HousingSideWest and HousingSideEast."));
            if (sections.SelectMany(points => points).Any(point => point.Y <= -construction.Depth / 2d + 1e-6d
                || point.Y >= construction.Depth / 2d - 1e-6d || point.Z <= 1e-6d || point.Z >= construction.BaseHeight - 1e-6d))
                diagnostics.Add(new("section-chain-remove-disconnects-body", "The through-duct profile must remain strictly inside the housing Y/Z boundary so the result stays connected."));
            if (chain.Sections.Zip(chain.Sections.Skip(1), (a, b) => b.Frame.Origin.X - a.Frame.Origin.X).Any(delta => delta <= 1e-8d))
                diagnostics.Add(new("section-chain-remove-order-invalid", "Through-duct sections must progress monotonically from HousingSideWest to HousingSideEast."));
            foreach (var hole in construction.Holes)
            {
                var radius = hole.Diameter / 2d;
                if (sections.SelectMany(points => points).Any(point => Math.Abs(point.Y - hole.CenterY) <= radius + 1e-6d)
                    && sections.SelectMany(points => points).Min(point => point.Z) <= construction.BaseHeight)
                    diagnostics.Add(new("bodystate-preserved-region-modified", $"RemoveSectionChain intersects preserved mounting hole '{hole.StableId}'.", hole.StableId));
            }
        }
        return diagnostics.Count > 0 ? new(null, chain, null, diagnostics) : Build(construction, chain, additive: false);
    }

    private static BuildResult Build(HousingConstruction construction, SectionChain chain, bool additive)
    {
        var tool = SectionChainMaterializer.Materialize(chain);
        if (!tool.IsSuccess)
            return new(null, chain, tool.Pcurves, tool.Diagnostics.Select(item => new SculptDiagnostic(item.Code, item.Message, chain.StableId)).ToArray());

        var builder = new TopologyBuilder();
        var points = new Dictionary<VertexId, Point3D>();
        var curves = new Dictionary<EdgeId, (CurveGeometry Curve, ParameterInterval Interval)>();
        var faces = new List<(FaceId Face, SurfaceGeometry Surface)>();
        var bottom = HousingSection(construction.Width, construction.Depth, 0d);
        var top = HousingSection(construction.Width, construction.Depth, construction.BaseHeight);
        var vb = bottom.Select(point => AddVertex(point)).ToArray();
        var vt = top.Select(point => AddVertex(point)).ToArray();
        var eb = Enumerable.Range(0, 4).Select(index => AddLine(vb[index], vb[(index + 1) % 4])).ToArray();
        var et = Enumerable.Range(0, 4).Select(index => AddLine(vt[index], vt[(index + 1) % 4])).ToArray();
        var ev = Enumerable.Range(0, 4).Select(index => AddLine(vb[index], vt[index])).ToArray();

        var bottomHoleEdges = new EdgeId[construction.Holes.Count];
        var topHoleEdges = new EdgeId[construction.Holes.Count];
        var seamEdges = new EdgeId[construction.Holes.Count];
        for (var index = 0; index < construction.Holes.Count; index++)
        {
            var hole = construction.Holes[index]; var seamX = hole.CenterX + hole.Diameter / 2d;
            var bottomSeam = AddVertex(new(seamX, hole.CenterY, 0d));
            var topSeam = AddVertex(new(seamX, hole.CenterY, construction.BaseHeight));
            bottomHoleEdges[index] = AddCircle(bottomSeam, hole, 0d);
            topHoleEdges[index] = AddCircle(topSeam, hole, construction.BaseHeight);
            seamEdges[index] = AddLine(bottomSeam, topSeam);
        }

        var chainPoints = chain.Sections.Select(Points).ToArray();
        var chainVertices = new VertexId[chain.Sections.Count][];
        var chainEdges = new DirectedEdge[chain.Sections.Count][];
        for (var section = 0; section < chain.Sections.Count; section++)
        {
            if (additive && section == 0)
            {
                chainVertices[section] = [vb[1], vb[2], vt[2], vt[1]];
                chainEdges[section] = [new(eb[1], true), new(ev[2], true), new(et[1], false), new(ev[1], false)];
                continue;
            }
            chainVertices[section] = chainPoints[section].Select(AddVertex).ToArray();
            chainEdges[section] = Enumerable.Range(0, 4).Select(index =>
                new DirectedEdge(AddLine(chainVertices[section][index], chainVertices[section][(index + 1) % 4]), true)).ToArray();
        }

        var longitudinal = new EdgeId[chain.Sections.Count - 1][];
        for (var transition = 0; transition < longitudinal.Length; transition++)
            longitudinal[transition] = Enumerable.Range(0, 4).Select(index => AddLine(chainVertices[transition][index], chainVertices[transition + 1][index])).ToArray();

        var bottomLoops = new List<LoopId> { AddLoop(eb.Select(edge => new Use(edge, false)).ToArray()) };
        var topLoops = new List<LoopId> { AddLoop(et.Select(edge => new Use(edge, true)).Reverse().ToArray()) };
        for (var index = 0; index < construction.Holes.Count; index++)
        {
            bottomLoops.Add(AddLoop([new(bottomHoleEdges[index], true)]));
            topLoops.Add(AddLoop([new(topHoleEdges[index], false)]));
        }
        faces.Add((builder.AddFace(bottomLoops), SurfaceGeometry.FromPlane(new PlaneSurface(Point3D.Origin, Dir(0, 0, -1), Dir(1, 0, 0)))));
        faces.Add((builder.AddFace(topLoops), SurfaceGeometry.FromPlane(new PlaneSurface(new(0, 0, construction.BaseHeight), Dir(0, 0, 1), Dir(1, 0, 0)))));

        for (var side = 0; side < 4; side++)
        {
            if (additive && side == 1) continue;
            var next = (side + 1) % 4;
            var loops = new List<LoopId> { AddLoop([new(eb[side], false), new(ev[next], false), new(et[side], true), new(ev[side], true)]) };
            if (!additive && side == 3)
                loops.Add(AddLoop(chainEdges[0].Select(edge => new Use(edge.Edge, !edge.Forward)).ToArray()));
            if (!additive && side == 1)
                loops.Add(AddLoop(chainEdges[^1].Reverse().Select(edge => new Use(edge.Edge, edge.Forward)).ToArray()));
            var edge = bottom[next] - bottom[side]; var rise = top[side] - bottom[side];
            faces.Add((builder.AddFace(loops), SurfaceGeometry.FromPlane(new PlaneSurface(bottom[side], Direction3D.Create(edge.Cross(rise)), Direction3D.Create(edge)))));
        }

        for (var transition = 0; transition < longitudinal.Length; transition++)
        for (var span = 0; span < 4; span++)
        {
            var next = (span + 1) % 4; IReadOnlyList<Use> uses;
            if (additive)
                uses = [Cycle(chainEdges[transition][span], true), new(longitudinal[transition][next], false),
                    Cycle(chainEdges[transition + 1][span], false), new(longitudinal[transition][span], true)];
            else
                uses = [Cycle(chainEdges[transition][span], false), new(longitudinal[transition][span], false),
                    Cycle(chainEdges[transition + 1][span], true), new(longitudinal[transition][next], true)];
            faces.Add((builder.AddFace([AddLoop(uses)]), TransitionSurface(chainPoints[transition][span], chainPoints[transition][next],
                chainPoints[transition + 1][span], chainPoints[transition + 1][next])));
        }

        if (additive)
        {
            var capUses = chainEdges[^1].Select(edge => Cycle(edge, true)).ToArray();
            var frame = chain.Sections[^1].Frame;
            faces.Add((builder.AddFace([AddLoop(capUses)]), SurfaceGeometry.FromPlane(new PlaneSurface(frame.Origin, frame.Normal, frame.XAxis))));
        }

        for (var index = 0; index < construction.Holes.Count; index++)
        {
            var loop = AddLoop([new(bottomHoleEdges[index], false), new(seamEdges[index], false), new(topHoleEdges[index], true), new(seamEdges[index], true)]);
            var hole = construction.Holes[index];
            faces.Add((builder.AddFace([loop]), SurfaceGeometry.FromCylinder(new CylinderSurface(new(hole.CenterX, hole.CenterY, 0d), Dir(0, 0, 1), hole.Diameter / 2d, Dir(1, 0, 0)))));
        }

        var shell = builder.AddShell(faces.Select(face => face.Face).ToArray()); builder.AddBody([shell]);
        var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var curveId = 1; var surfaceId = 1;
        foreach (var pair in curves.OrderBy(pair => pair.Key.Value))
        {
            var id = new CurveGeometryId(curveId++); geometry.AddCurve(id, pair.Value.Curve); bindings.AddEdgeBinding(new(pair.Key, id, pair.Value.Interval));
        }
        foreach (var face in faces)
        {
            var id = new SurfaceGeometryId(surfaceId++); geometry.AddSurface(id, face.Surface); bindings.AddFaceBinding(new(face.Face, id));
        }
        var body = new BrepBody(builder.Model, geometry, bindings, points);
        var pcurveBuild = BoundedPcurveBuilder.Populate(builder.Model, geometry, bindings, 1e-5d);
        if (!pcurveBuild.IsSuccess) return new(null, chain, tool.Pcurves, pcurveBuild.Diagnostics);
        var binding = BrepBindingValidator.Validate(body, true);
        if (!binding.IsSuccess) return new(null, chain, tool.Pcurves, binding.Diagnostics.Select(item => new SculptDiagnostic("section-chain-brep-invalid", item.Message)).ToArray());
        var pcurves = BrepPcurveValidator.Validate(body, 1e-5d, requireEveryCoedge: true);
        if (!pcurves.IsValid) return new(null, chain, tool.Pcurves, pcurves.Diagnostics.Select(message => new SculptDiagnostic("section-chain-pcurve-error", message)).ToArray());
        return new(body, chain, tool.Pcurves, []);

        VertexId AddVertex(Point3D point) { var vertex = builder.AddVertex(); points[vertex] = point; return vertex; }
        EdgeId AddLine(VertexId start, VertexId end)
        {
            var edge = builder.AddEdge(start, end); var vector = points[end] - points[start];
            curves[edge] = (CurveGeometry.FromLine(new Line3Curve(points[start], Direction3D.Create(vector))), new(0d, vector.Length)); return edge;
        }
        EdgeId AddCircle(VertexId seam, HousingHole hole, double z)
        {
            var edge = builder.AddEdge(seam, seam);
            curves[edge] = (CurveGeometry.FromCircle(new Circle3Curve(new(hole.CenterX, hole.CenterY, z), Dir(0, 0, 1), hole.Diameter / 2d, Dir(1, 0, 0))), new(0d, 2d * Math.PI)); return edge;
        }
        LoopId AddLoop(IReadOnlyList<Use> uses)
        {
            var loop = builder.AllocateLoopId(); var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
            for (var index = 0; index < coedges.Length; index++) builder.AddCoedge(new(coedges[index], uses[index].Edge, loop,
                coedges[(index + 1) % coedges.Length], coedges[(index + coedges.Length - 1) % coedges.Length], uses[index].Reversed));
            builder.AddLoop(new Loop(loop, coedges)); return loop;
        }
    }

    private static IEnumerable<SculptDiagnostic> ValidateCommon(HousingConstruction construction, SectionChain chain)
    {
        if (construction.HasCrown || construction.ReplacementPatch is not null)
            yield return new("section-chain-housing-base-unsupported", "The admitted X3b composition lane requires a planar uncrowned Housing base; replay fails closed for upstream crown/patch changes.");
        if (chain.Sections.Count < 2) yield return new("section-chain-section-count-invalid", "SectionChain requires at least two sections.");
        if (chain.TransitionPolicy != SectionTransitionPolicy.Ruled) yield return new("section-chain-transition-policy-invalid", "Only Ruled transitions are admitted.");
        foreach (var section in chain.Sections)
        {
            if (section.Profile.Spans.Count != 4 || section.Profile.Spans.Any(span => span.Curve is not SectionProfileCurve.Line))
                yield return new("section-chain-profile-topology-unsupported", $"Section '{section.SectionId}' must contain four ordered line spans in the bounded housing composition lane.", section.SectionId);
            if (Math.Abs(section.Frame.Normal.ToVector().Dot(new Vector3D(1, 0, 0)) - 1d) > 1e-8d)
                yield return new("section-chain-frame-unsupported", $"Section '{section.SectionId}' must use +X progression for the admitted housing lane.", section.SectionId);
        }
    }

    private static SectionChain PlaceRelativeToEast(SectionChain chain, double supportX) => chain with
    {
        Sections = chain.Sections.Select(section => section with
        {
            Frame = section.Frame with { Origin = new(section.Frame.Origin.X + supportX, section.Frame.Origin.Y, section.Frame.Origin.Z) }
        }).ToArray()
    };

    private static Point3D[] Points(Section section) => section.Profile.Spans.Select(span =>
        section.Frame.Transform(((SectionProfileCurve.Line)span.Curve).Start)).ToArray();
    private static bool Matches(IReadOnlyList<Point3D> actual, IReadOnlyList<Point3D> expected) => actual.Count == expected.Count
        && actual.Zip(expected).All(pair => (pair.First - pair.Second).Length <= 1e-6d);
    private static Point3D[] HousingSection(double width, double depth, double z)
    {
        var x = width / 2d; var y = depth / 2d; return [new(-x, -y, z), new(x, -y, z), new(x, y, z), new(-x, y, z)];
    }
    private static SurfaceGeometry TransitionSurface(Point3D a, Point3D b, Point3D c, Point3D d)
    {
        var normal = (b - a).Cross(c - a);
        if (normal.Length > 1e-10d && Math.Abs((d - a).Dot(normal / normal.Length)) <= 1e-8d)
            return SurfaceGeometry.FromPlane(new PlaneSurface(a, Direction3D.Create(normal), Direction3D.Create(b - a)));
        var spline = new BSplineSurfaceWithKnots(1, 1, [[a, b], [c, d]], "UNSPECIFIED", false, false, false,
            [2, 2], [2, 2], [0d, 1d], [0d, 1d], "UNSPECIFIED");
        return SurfaceGeometry.FromBSplineSurfaceWithKnots(spline);
    }
    private static Use Cycle(DirectedEdge edge, bool forward) => new(edge.Edge, forward ? !edge.Forward : edge.Forward);
    private static Direction3D Dir(double x, double y, double z) => Direction3D.Create(new Vector3D(x, y, z));
    private readonly record struct DirectedEdge(EdgeId Edge, bool Forward);
    private readonly record struct Use(EdgeId Edge, bool Reversed);
}
