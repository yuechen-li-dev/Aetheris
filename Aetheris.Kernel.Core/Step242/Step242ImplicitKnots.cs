namespace Aetheris.Kernel.Core.Step242;

/// <summary>
/// ISO 10303-42 B-spline subtypes whose knots are implied by the type rather than written: BEZIER_CURVE,
/// QUASI_UNIFORM_CURVE, UNIFORM_CURVE and their surface counterparts. Each instance is rewritten with the explicit
/// B_SPLINE_*_WITH_KNOTS data its type defines, so the rest of the importer reads one B-spline form. Rational
/// weights, when present, are untouched.
/// </summary>
internal static class Step242ImplicitKnots
{
    private static readonly Dictionary<string, (string Spec, bool Surface)> Kinds = new(StringComparer.Ordinal)
    {
        ["BEZIER_CURVE"] = (".PIECEWISE_BEZIER_KNOTS.", false),
        ["QUASI_UNIFORM_CURVE"] = (".QUASI_UNIFORM_KNOTS.", false),
        ["UNIFORM_CURVE"] = (".UNIFORM_KNOTS.", false),
        ["BEZIER_SURFACE"] = (".PIECEWISE_BEZIER_KNOTS.", true),
        ["QUASI_UNIFORM_SURFACE"] = (".QUASI_UNIFORM_KNOTS.", true),
        ["UNIFORM_SURFACE"] = (".UNIFORM_KNOTS.", true),
    };

    public static IReadOnlyList<Step242ParsedEntity> Normalize(IReadOnlyList<Step242ParsedEntity> entities)
    {
        List<Step242ParsedEntity>? rewritten = null;
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            var replacement = TryRewrite(entity.Instance);
            if (replacement is null) continue;
            rewritten ??= [.. entities];
            rewritten[i] = entity with { Instance = replacement };
        }
        return rewritten ?? entities;
    }

    private static Step242EntityInstance? TryRewrite(Step242EntityInstance instance)
    {
        switch (instance)
        {
            // A simple instance carries the whole B_SPLINE_* attribute list itself, name first.
            case Step242SimpleEntityInstance { Constructor: var simple } when Kinds.TryGetValue(simple.Name, out var kind):
            {
                var arguments = simple.Arguments;
                var expected = kind.Surface ? 8 : 6;
                if (arguments.Count != expected) return null;
                var knots = kind.Surface ? SurfaceKnots(arguments.Skip(1).ToArray(), kind.Spec) : CurveKnots(arguments.Skip(1).ToArray(), kind.Spec);
                if (knots is null) return null;
                return new Step242SimpleEntityInstance(new(kind.Surface ? "B_SPLINE_SURFACE_WITH_KNOTS" : "B_SPLINE_CURVE_WITH_KNOTS", [.. arguments, .. knots]));
            }
            // In a complex instance the B_SPLINE_* constructor holds the attributes and the subtype is empty.
            case Step242ComplexEntityInstance complex:
            {
                var index = -1;
                for (var k = 0; k < complex.Constructors.Count; k++)
                    if (Kinds.ContainsKey(complex.Constructors[k].Name)) { index = k; break; }
                if (index < 0) return null;
                var kind = Kinds[complex.Constructors[index].Name];
                var spline = complex.Constructors.FirstOrDefault(c => c.Name == (kind.Surface ? "B_SPLINE_SURFACE" : "B_SPLINE_CURVE"));
                if (spline is null) return null;
                var knots = kind.Surface ? SurfaceKnots(spline.Arguments, kind.Spec) : CurveKnots(spline.Arguments, kind.Spec);
                if (knots is null) return null;
                var constructors = complex.Constructors.ToList();
                constructors[index] = new(kind.Surface ? "B_SPLINE_SURFACE_WITH_KNOTS" : "B_SPLINE_CURVE_WITH_KNOTS", knots);
                return new Step242ComplexEntityInstance(constructors);
            }
            default:
                return null;
        }
    }

    /// <summary>(degree, control_points_list, curve_form, closed_curve, self_intersect) → (multiplicities, knots, spec).</summary>
    private static Step242Value[]? CurveKnots(IReadOnlyList<Step242Value> attributes, string spec)
    {
        if (attributes.Count < 2 || attributes[0] is not Step242NumberValue degree || attributes[1] is not Step242ListValue points) return null;
        var knots = Knots(spec, (int)degree.Value, points.Items.Count);
        return knots is null ? null : [knots.Value.Multiplicities, knots.Value.Values, new Step242EnumValue(spec.Trim('.'))];
    }

    /// <summary>(u_degree, v_degree, control_points_list, …) → (u multiplicities, v multiplicities, u knots, v knots, spec).</summary>
    private static Step242Value[]? SurfaceKnots(IReadOnlyList<Step242Value> attributes, string spec)
    {
        if (attributes.Count < 3 || attributes[0] is not Step242NumberValue uDegree || attributes[1] is not Step242NumberValue vDegree
            || attributes[2] is not Step242ListValue rows || rows.Items.Count == 0 || rows.Items[0] is not Step242ListValue firstRow) return null;
        var u = Knots(spec, (int)uDegree.Value, rows.Items.Count);
        var v = Knots(spec, (int)vDegree.Value, firstRow.Items.Count);
        return u is null || v is null ? null : [u.Value.Multiplicities, v.Value.Multiplicities, u.Value.Values, v.Value.Values, new Step242EnumValue(spec.Trim('.'))];
    }

    /// <summary>
    /// The knots ISO 10303-42 defines for <paramref name="count"/> control points of degree <paramref name="degree"/>:
    /// piecewise Bézier 0…s with end multiplicity degree + 1 and interior degree (s = (count − 1) / degree);
    /// quasi-uniform 0…(count − degree) with end multiplicity degree + 1; uniform −degree…count, each once.
    /// </summary>
    private static (Step242ListValue Multiplicities, Step242ListValue Values)? Knots(string spec, int degree, int count)
    {
        if (degree < 1 || count < degree + 1) return null;
        var multiplicities = new List<int>(); var values = new List<double>();
        switch (spec)
        {
            case ".PIECEWISE_BEZIER_KNOTS.":
                if ((count - 1) % degree != 0) return null;
                var segments = (count - 1) / degree;
                for (var k = 0; k <= segments; k++) { values.Add(k); multiplicities.Add(k == 0 || k == segments ? degree + 1 : degree); }
                break;
            case ".QUASI_UNIFORM_KNOTS.":
                var last = count - degree;
                for (var k = 0; k <= last; k++) { values.Add(k); multiplicities.Add(k == 0 || k == last ? degree + 1 : 1); }
                break;
            default:
                for (var k = -degree; k <= count; k++) { values.Add(k); multiplicities.Add(1); }
                break;
        }
        return (new Step242ListValue(multiplicities.Select(m => (Step242Value)new Step242NumberValue(m)).ToArray()),
            new Step242ListValue(values.Select(v => (Step242Value)new Step242NumberValue(v)).ToArray()));
    }
}
