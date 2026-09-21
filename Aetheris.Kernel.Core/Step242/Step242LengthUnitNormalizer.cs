using System.Globalization;

namespace Aetheris.Kernel.Core.Step242;

/// <summary>
/// Aetheris works in millimetres internally (the exporter always writes SI milli-metre). STEP files may declare any
/// length unit (McMaster/SolidWorks parts are frequently in inches), so the parsed entity list is rewritten once, right
/// after parsing, so that every length-valued attribute is expressed in millimetres. Angles are handled separately by
/// <see cref="Step242ParsedDocument.PlaneAngleToRadiansScale"/>.
/// </summary>
internal static class Step242LengthUnitNormalizer
{
    private const double MillimetresPerMetre = 1000d;

    // Entity name -> zero-based argument indices holding a length (radii, semi-axes, vector magnitudes).
    private static readonly Dictionary<string, int[]> ScalarLengthArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VECTOR"] = [1],
        ["CIRCLE"] = [2],
        ["ELLIPSE"] = [2, 3],
        ["HYPERBOLA"] = [2, 3],
        ["PARABOLA"] = [2],
        ["CYLINDRICAL_SURFACE"] = [2],
        ["CONICAL_SURFACE"] = [2],
        ["SPHERICAL_SURFACE"] = [2],
        ["TOROIDAL_SURFACE"] = [2, 3],
        ["DEGENERATE_TOROIDAL_SURFACE"] = [2, 3],
        ["OFFSET_SURFACE"] = [2],
        ["OFFSET_CURVE_3D"] = [2],
    };

    /// <summary>Resolves the millimetres-per-file-unit factor from the first length unit found in a unit-assigned context.</summary>
    public static double ResolveMillimetresPerUnit(IReadOnlyList<Step242ParsedEntity> entities)
    {
        var byId = entities.ToDictionary(e => e.Id);
        foreach (var entity in entities)
        {
            var context = Step242SubsetDecoder.TryGetConstructor(entity.Instance, "GLOBAL_UNIT_ASSIGNED_CONTEXT");
            if (context is null || context.Arguments.Count == 0 || context.Arguments[0] is not Step242ListValue units)
            {
                continue;
            }

            foreach (var unit in units.Items)
            {
                if (unit is Step242EntityReference reference
                    && TryResolveMetresPerUnit(byId, reference.TargetId, new HashSet<int>(), out var metres))
                {
                    return metres * MillimetresPerMetre;
                }
            }
        }

        return 1d;
    }

    /// <summary>
    /// Resolves the distance accuracy the source file declares for its geometry, in millimetres, or null when it
    /// declares none. This is the exporter's own statement of how exactly its entities agree, which makes it the
    /// right yardstick for deciding whether an approximated entity may be replaced by the primitive it encodes.
    /// The smallest declaration wins, and the value is scaled because unit normalization does not touch it.
    /// </summary>
    public static double? ResolveDistanceAccuracyMillimetres(IReadOnlyList<Step242ParsedEntity> entities, double millimetresPerUnit)
    {
        double? smallest = null;
        foreach (var entity in entities)
        {
            var uncertainty = Step242SubsetDecoder.TryGetConstructor(entity.Instance, "UNCERTAINTY_MEASURE_WITH_UNIT");
            if (uncertainty is null || uncertainty.Arguments.Count == 0)
            {
                continue;
            }

            var measure = uncertainty.Arguments[0] switch
            {
                Step242TypedValue typed when typed.Arguments.Count > 0 && typed.Arguments[0] is Step242NumberValue typedNumber => typedNumber.Value,
                Step242NumberValue number => number.Value,
                _ => double.NaN
            };

            var millimetres = measure * millimetresPerUnit;
            if (!double.IsFinite(millimetres) || millimetres <= 0d)
            {
                continue;
            }

            smallest = smallest is { } current ? double.Min(current, millimetres) : millimetres;
        }

        return smallest;
    }

    public static IReadOnlyList<Step242ParsedEntity> Normalize(IReadOnlyList<Step242ParsedEntity> entities, double scale)
    {
        if (double.Abs(scale - 1d) <= 1e-12d || scale <= 0d || !double.IsFinite(scale))
        {
            return entities;
        }

        return entities.Select(entity => entity with { Instance = ScaleInstance(entity.Instance, scale) }).ToList();
    }

    private static Step242EntityInstance ScaleInstance(Step242EntityInstance instance, double scale)
    {
        switch (instance)
        {
            case Step242SimpleEntityInstance simple:
                return simple with { Constructor = ScaleConstructor(simple.Constructor, scale) };
            case Step242ComplexEntityInstance complex:
                return complex with { Constructors = complex.Constructors.Select(c => ScaleConstructor(c, scale)).ToList() };
            default:
                return instance;
        }
    }

    private static Step242EntityConstructor ScaleConstructor(Step242EntityConstructor constructor, double scale)
    {
        var name = constructor.Name;
        var arguments = constructor.Arguments;

        if (string.Equals(name, "CARTESIAN_POINT", StringComparison.OrdinalIgnoreCase))
        {
            // 2D cartesian points are parametric (pcurve) coordinates, not lengths.
            if (arguments.Count >= 2 && arguments[1] is Step242ListValue coordinates && coordinates.Items.Count == 3)
            {
                return constructor with { Arguments = Replace(arguments, 1, ScaleNumberList(coordinates, scale)) };
            }

            return constructor;
        }

        if (string.Equals(name, "COORDINATES_LIST", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Count >= 3 && arguments[2] is Step242ListValue tuples)
            {
                var scaled = tuples.Items.Select(item => item is Step242ListValue tuple ? ScaleNumberList(tuple, scale) : item).ToList();
                return constructor with { Arguments = Replace(arguments, 2, new Step242ListValue(scaled)) };
            }

            return constructor;
        }

        if (ScalarLengthArguments.TryGetValue(name, out var indices))
        {
            var updated = arguments.ToList();
            foreach (var index in indices)
            {
                if (index < updated.Count && updated[index] is Step242NumberValue number)
                {
                    updated[index] = new Step242NumberValue(number.Value * scale);
                }
            }

            return constructor with { Arguments = updated };
        }

        return constructor;
    }

    private static Step242ListValue ScaleNumberList(Step242ListValue list, double scale)
        => new(list.Items.Select(item => item is Step242NumberValue number ? new Step242NumberValue(number.Value * scale) : item).ToList());

    private static IReadOnlyList<Step242Value> Replace(IReadOnlyList<Step242Value> arguments, int index, Step242Value value)
    {
        var updated = arguments.ToList();
        updated[index] = value;
        return updated;
    }

    private static bool TryResolveMetresPerUnit(Dictionary<int, Step242ParsedEntity> byId, int unitEntityId, HashSet<int> visited, out double metres)
    {
        metres = default;
        if (!visited.Add(unitEntityId) || !byId.TryGetValue(unitEntityId, out var unitEntity))
        {
            return false;
        }

        if (Step242SubsetDecoder.TryGetConstructor(unitEntity.Instance, "LENGTH_UNIT") is null)
        {
            return false;
        }

        var si = Step242SubsetDecoder.TryGetConstructor(unitEntity.Instance, "SI_UNIT");
        if (si is not null
            && si.Arguments.Count >= 2
            && si.Arguments[1] is Step242EnumValue unitName
            && string.Equals(unitName.Value, "METRE", StringComparison.Ordinal))
        {
            metres = si.Arguments[0] is Step242EnumValue prefix ? PrefixScale(prefix.Value) : 1d;
            return metres > 0d;
        }

        var conversion = Step242SubsetDecoder.TryGetConstructor(unitEntity.Instance, "CONVERSION_BASED_UNIT");
        if (conversion is null
            || conversion.Arguments.Count < 2
            || conversion.Arguments[1] is not Step242EntityReference measureRef
            || !byId.TryGetValue(measureRef.TargetId, out var measureEntity))
        {
            return false;
        }

        var measure = measureEntity.Instance.PrimaryConstructor;
        if (measure.Arguments.Count < 2
            || measure.Arguments[0] is not Step242TypedValue typed
            || typed.Arguments.Count != 1
            || typed.Arguments[0] is not Step242NumberValue magnitude
            || measure.Arguments[1] is not Step242EntityReference baseUnitRef
            || !TryResolveMetresPerUnit(byId, baseUnitRef.TargetId, visited, out var baseMetres))
        {
            return false;
        }

        metres = magnitude.Value * baseMetres;
        return metres > 0d;
    }

    private static double PrefixScale(string prefix) => prefix switch
    {
        "EXA" => 1e18, "PETA" => 1e15, "TERA" => 1e12, "GIGA" => 1e9, "MEGA" => 1e6, "KILO" => 1e3,
        "HECTO" => 1e2, "DECA" => 1e1, "DECI" => 1e-1, "CENTI" => 1e-2, "MILLI" => 1e-3, "MICRO" => 1e-6,
        "NANO" => 1e-9, "PICO" => 1e-12, "FEMTO" => 1e-15, "ATTO" => 1e-18, _ => 1d,
    };
}
