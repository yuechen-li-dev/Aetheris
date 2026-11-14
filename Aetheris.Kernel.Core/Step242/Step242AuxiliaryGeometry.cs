using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Step242;

/// <summary>
/// Points of the auxiliary geometry a PMI record may be attached to instead of a face: datum target points, isolated
/// curves (centre lines, edges drawn for a dimension), placements. They locate a callout; they are not part of the
/// body's topology. Coordinates are in millimetres.
/// </summary>
internal static class Step242AuxiliaryGeometry
{
    /// <summary>Representative points of one geometry item, or none when its kind is not read.</summary>
    public static IReadOnlyList<Point3D> Points(Step242ParsedDocument document, int id) =>
        Points(id, document.Entities.ToDictionary(e => e.Id), document.SourceMillimetresPerUnit, 0);

    public static IReadOnlyList<Point3D> Points(int id, IReadOnlyDictionary<int, Step242ParsedEntity> entities, double scale, int depth)
    {
        if (depth > 8 || !entities.TryGetValue(id, out var entity)) return [];
        var args = entity.Arguments;
        IReadOnlyList<Point3D> Of(Step242Value? value) => value is Step242EntityReference r ? Points(r.TargetId, entities, scale, depth + 1) : [];
        switch (entity.Name)
        {
            case "CARTESIAN_POINT":
                return args.ElementAtOrDefault(1) is Step242ListValue { Items: var c } && c.Count >= 2
                    ? [new Point3D(N(c[0]) * scale, N(c[1]) * scale, c.Count > 2 ? N(c[2]) * scale : 0)] : [];
            case "VERTEX_POINT": return Of(args.ElementAtOrDefault(1));
            case "AXIS2_PLACEMENT_3D" or "AXIS1_PLACEMENT" or "AXIS2_PLACEMENT_2D": return Of(args.ElementAtOrDefault(1));
            case "LINE": return Of(args.ElementAtOrDefault(1));
            case "CIRCLE" or "ELLIPSE": return Of(args.ElementAtOrDefault(1));
            case "POLYLINE":
                return args.ElementAtOrDefault(1) is Step242ListValue list ? list.Items.SelectMany(Of).ToArray() : [];
            case "EDGE_CURVE": return Of(args.ElementAtOrDefault(1)).Concat(Of(args.ElementAtOrDefault(2))).ToArray();
            case "TRIMMED_CURVE":
            {
                // Trims given as points are the curve's ends; parameter trims are evaluated on lines and circles.
                var ends = new List<Point3D>();
                foreach (var trim in new[] { args.ElementAtOrDefault(2), args.ElementAtOrDefault(3) })
                {
                    if (trim is not Step242ListValue select) continue;
                    var point = select.Items.OfType<Step242EntityReference>().SelectMany(r => Points(r.TargetId, entities, scale, depth + 1)).FirstOrDefault();
                    if (select.Items.OfType<Step242EntityReference>().Any()) { if (point != default) ends.Add(point); continue; }
                    var parameter = select.Items.OfType<Step242TypedValue>().FirstOrDefault(t => t.Name == "PARAMETER_VALUE")?.Arguments.FirstOrDefault();
                    if (parameter is not null && Evaluate(args.ElementAtOrDefault(1), N(parameter), entities, scale) is { } evaluated) ends.Add(evaluated);
                }
                return ends.Count > 0 ? ends : Of(args.ElementAtOrDefault(1));
            }
            default: return [];
        }
    }

    private static double N(Step242Value value) => value switch
    {
        Step242NumberValue n => n.Value,
        Step242TypedValue t when t.Arguments.FirstOrDefault() is Step242NumberValue inner => inner.Value,
        _ => 0
    };

    private static Point3D? Evaluate(Step242Value? basis, double t, IReadOnlyDictionary<int, Step242ParsedEntity> entities, double scale)
    {
        if (basis is not Step242EntityReference r || !entities.TryGetValue(r.TargetId, out var curve)) return null;
        Vector3D? Direction(Step242Value? value) => value is Step242EntityReference d && entities.TryGetValue(d.TargetId, out var e) && e.Name == "DIRECTION"
            && e.Arguments.ElementAtOrDefault(1) is Step242ListValue { Items: var c } && c.Count >= 3 ? new Vector3D(N(c[0]), N(c[1]), N(c[2])) : null;
        switch (curve.Name)
        {
            case "LINE" when Points(r.TargetId, entities, scale, 1) is [var origin]
                && curve.Arguments.ElementAtOrDefault(2) is Step242EntityReference vr && entities.TryGetValue(vr.TargetId, out var vector):
                return Direction(vector.Arguments.ElementAtOrDefault(1)) is { } d
                    ? origin + d * (N(vector.Arguments.ElementAtOrDefault(2) ?? new Step242NumberValue(1)) * t * scale) : null;
            case "CIRCLE" when curve.Arguments.ElementAtOrDefault(1) is Step242EntityReference pr && entities.TryGetValue(pr.TargetId, out var placement)
                && Points(pr.TargetId, entities, scale, 1) is [var centre]:
            {
                var z = Direction(placement.Arguments.ElementAtOrDefault(2)) ?? new Vector3D(0, 0, 1);
                var x = Direction(placement.Arguments.ElementAtOrDefault(3)) ?? (System.Math.Abs(z.X) < 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0));
                x = (x - z * x.Dot(z)); x = x / x.Length; var y = z.Cross(x);
                var radius = N(curve.Arguments.ElementAtOrDefault(2) ?? new Step242NumberValue(0)) * scale;
                return centre + x * (radius * System.Math.Cos(t)) + y * (radius * System.Math.Sin(t));
            }
            default: return null;
        }
    }
}
