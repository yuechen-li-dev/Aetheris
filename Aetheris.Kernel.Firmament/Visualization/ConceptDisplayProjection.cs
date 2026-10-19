using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Visualization;

/// <summary>Definition-local millimetre guides, separate from material and exported topology.</summary>
public sealed record ConceptDisplayWire(string Id, string Owner, string Kind, double[] Points, string Provenance);

/// <summary>Projects bound values only. Infinite planes/axes use a finite 40 mm display guide.</summary>
public static class ConceptDisplayProjection
{
    public static IReadOnlyList<ConceptDisplayWire> Project(FirmamentStepExportResult export) =>
        Project(export.ConceptDisplayValues);

    public static IReadOnlyList<ConceptDisplayWire> Project(ConceptIrDocument? document)
        => Project(document?.Structs ?? []);

    public static IReadOnlyList<ConceptDisplayWire> Project(IReadOnlyList<ConceptIrStructInstance> structs)
    {
        var wires = new List<ConceptDisplayWire>();
        foreach (var instance in structs)
            foreach (var (name, value) in instance.Members.OrderBy(p => p.Key, StringComparer.Ordinal))
                Add(value, instance.Name + "." + name, wires);
        return wires;
    }

    public static IReadOnlyList<ConceptDisplayWire> Project(SemanticValue root)
    {
        var wires = new List<ConceptDisplayWire>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(SemanticValue value)
        {
            if (!visited.Add(value.StableIdentity)) return;
            var owner = value.StableIdentity;
            foreach (var binding in value.Bindings)
                switch (binding)
                {
                    case ExactPointBinding p: Point(owner, [p.X, p.Y, p.Z], "Bound semantic point", wires); break;
                    case ExactAxisBinding a: Axis(owner, [a.OriginX, a.OriginY, a.OriginZ], [a.DirectionX, a.DirectionY, a.DirectionZ], "Bound semantic axis", wires); break;
                    case ExactPlaneBinding p: Plane(owner, [p.OriginX, p.OriginY, p.OriginZ], [p.NormalX, p.NormalY, p.NormalZ], null, "Bound semantic plane", wires); break;
                    case ExactDatumFrameBinding f:
                        var o = new[] { f.OriginX, f.OriginY, f.OriginZ };
                        foreach (var (name, direction) in new[] { ("X", new[] { f.XAxisX, f.XAxisY, f.XAxisZ }), ("Y", new[] { f.YAxisX, f.YAxisY, f.YAxisZ }), ("Z", new[] { f.ZAxisX, f.ZAxisY, f.ZAxisZ }) })
                            Line(owner + ":" + name, owner, "Frame", [o, Offset(o, direction, 20)], "Bound semantic frame; 20 mm display axes", wires);
                        break;
                }
            foreach (var member in value.ExposedMembers.OrderBy(p => p.Key, StringComparer.Ordinal)) Visit(member.Value);
        }
        Visit(root);
        return wires;
    }

    private static double[] P(ConceptIrPoint3 p) => [p.X, p.Y, p.Z];
    private static double[] V(ConceptIrVector3 p) => [p.X, p.Y, p.Z];
    private static double[] Offset(double[] o, double[] d, double t) => [o[0] + d[0] * t, o[1] + d[1] * t, o[2] + d[2] * t];
    private static double[] Cross(double[] a, double[] b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    private static double[] Unit(double[] v) { var length = Math.Sqrt(v.Sum(x => x * x)); return v.Select(x => x / length).ToArray(); }
    private static void Line(string id, string owner, string kind, double[][] points, string provenance, List<ConceptDisplayWire> wires)
    {
        var coordinates = points.SelectMany(p => p).ToArray();
        if (coordinates.Length >= 6 && coordinates.All(double.IsFinite)) wires.Add(new(id, owner, kind, coordinates, provenance));
    }
    private static void Point(string owner, double[] point, string provenance, List<ConceptDisplayWire> wires)
    {
        for (var axis = 0; axis < 3; axis++) { var d = new double[3]; d[axis] = 1; Line(owner + ":" + axis, owner, "Point", [Offset(point, d, -1), Offset(point, d, 1)], provenance, wires); }
    }
    private static void Axis(string owner, double[] origin, double[] direction, string provenance, List<ConceptDisplayWire> wires) =>
        Line(owner, owner, "Axis", [Offset(origin, Unit(direction), -20), Offset(origin, Unit(direction), 20)], provenance, wires);
    private static void Plane(string owner, double[] origin, double[] normal, double[]? hint, string provenance, List<ConceptDisplayWire> wires)
    {
        var n = Unit(normal);
        var u = Unit(Cross(hint ?? (Math.Abs(n[2]) < .9 ? [0, 0, 1] : [0, 1, 0]), n));
        var v = Cross(n, u);
        Line(owner, owner, "Plane", new[] { (-1, -1), (1, -1), (1, 1), (-1, 1), (-1, -1) }
            .Select(p => Offset(Offset(origin, u, p.Item1 * 20), v, p.Item2 * 20)).ToArray(), provenance + "; finite display guide", wires);
    }
    private static void Add(ConceptIrValue value, string owner, List<ConceptDisplayWire> wires)
    {
        switch (value)
        {
            case ConceptIrBox3Value b:
                var corners = Enumerable.Range(0, 8).Select(i => new[] { (i & 1) == 0 ? b.Min.X : b.Max.X, (i & 2) == 0 ? b.Min.Y : b.Max.Y, (i & 4) == 0 ? b.Min.Z : b.Max.Z }).ToArray();
                for (var i = 0; i < 8; i++) foreach (var bit in new[] { 1, 2, 4 }) if ((i & bit) == 0)
                    Line(owner + $":{i}:{bit}", owner, "Box3", [corners[i], corners[i | bit]], value.Provenance, wires);
                break;
            case ConceptIrPlaneValue p: Plane(owner, P(p.Origin), V(p.Normal), p.OrientationHint is null ? null : V(p.OrientationHint), value.Provenance, wires); break;
            case ConceptIrAxisValue a: Axis(owner, P(a.Origin), V(a.Direction), value.Provenance, wires); break;
            case ConceptIrRegion2Value r:
                Line(owner, owner, "Region2", new[] { (r.MinU, r.MinV), (r.MaxU, r.MinV), (r.MaxU, r.MaxV), (r.MinU, r.MaxV), (r.MinU, r.MinV) }
                    .Select(p => Offset(Offset(P(r.Center), V(r.U), p.Item1), V(r.V), p.Item2)).ToArray(), value.Provenance, wires); break;
            case ConceptIrPoint3Value p: Point(owner, P(p.Point), value.Provenance, wires); break;
            case ConceptIrPointSetValue set: foreach (var p in set.Points) Point(owner + ":" + p.StableId, P(p.Point), p.Provenance, wires); break;
            case ConceptIrKeyedPointSetValue set:
                foreach (var p in set.Points.Where(p => p.Coordinates.Count == 3)) Point(owner + ":" + p.Key, p.Coordinates.ToArray(), p.Provenance, wires);
                break;
        }
    }
}
