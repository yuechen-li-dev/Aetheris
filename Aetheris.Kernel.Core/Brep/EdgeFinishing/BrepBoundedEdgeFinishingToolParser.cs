namespace Aetheris.Kernel.Core.Brep.EdgeFinishing;

/// <summary>
/// Shared bounded tool-field parsing seam for explicit edge-finishing selectors.
/// Candidate resolution/utility-based selection remains deferred to later milestones.
/// </summary>
public static class BrepBoundedEdgeFinishingToolParser
{
    public static bool TryParseChamferSelection(
        IReadOnlyDictionary<string, string> fields,
        out BrepBoundedChamferEdge? edge,
        out BrepBoundedChamferCorner? corner,
        out string error)
    {
        edge = null;
        corner = null;
        error = string.Empty;

        var hasEdges = fields.TryGetValue("edges", out var edgesRaw) && !string.IsNullOrWhiteSpace(edgesRaw);
        var hasCorners = fields.TryGetValue("corners", out var cornersRaw) && !string.IsNullOrWhiteSpace(cornersRaw);
        if (hasEdges == hasCorners)
        {
            error = "provide exactly one of edges or corners";
            return false;
        }

        if (hasEdges)
        {
            if (!TryParseChamferEdge(fields, out var parsedEdge, out error))
            {
                return false;
            }

            edge = parsedEdge;
            return true;
        }

        var tokens = ParseSingleTokenArray(cornersRaw!, out error);
        if (tokens is null)
        {
            return false;
        }

        if (tokens.Length != 1)
        {
            error = "bounded E2 supports exactly one explicit corner token";
            return false;
        }

        if (tokens[0] is not "x_max_y_max_z_max")
        {
            error = "supported corner tokens are x_max_y_max_z_max";
            return false;
        }

        corner = BrepBoundedChamferCorner.XMaxYMaxZMax;
        return true;
    }

    public static bool TryParseChamferEdge(
        IReadOnlyDictionary<string, string> fields,
        out BrepBoundedChamferEdge edge,
        out string error)
    {
        edge = BrepBoundedChamferEdge.XMinYMin;
        error = string.Empty;

        if (!fields.TryGetValue("edges", out var edgesRaw) || string.IsNullOrWhiteSpace(edgesRaw))
        {
            error = "missing required edges list";
            return false;
        }

        var tokens = ParseSingleTokenArray(edgesRaw, out error);
        if (tokens is null)
        {
            return false;
        }

        if (tokens.Length != 1)
        {
            error = "bounded M5a supports exactly one explicit edge token";
            return false;
        }

        edge = tokens[0] switch
        {
            "x_min_y_min" => BrepBoundedChamferEdge.XMinYMin,
            "x_min_y_max" => BrepBoundedChamferEdge.XMinYMax,
            "x_max_y_min" => BrepBoundedChamferEdge.XMaxYMin,
            "x_max_y_max" => BrepBoundedChamferEdge.XMaxYMax,
            _ => BrepBoundedChamferEdge.XMinYMin
        };

        if (tokens[0] is not ("x_min_y_min" or "x_min_y_max" or "x_max_y_min" or "x_max_y_max"))
        {
            error = "supported tokens are x_min_y_min, x_min_y_max, x_max_y_min, x_max_y_max";
            return false;
        }

        return true;
    }

    public static bool TryParseFilletEdge(
        IReadOnlyDictionary<string, string> fields,
        out BrepBoundedManufacturingFilletEdge edge,
        out string error)
    {
        edge = BrepBoundedManufacturingFilletEdge.InnerXMinYMin;
        error = string.Empty;

        if (!fields.TryGetValue("edges", out var edgesRaw) || string.IsNullOrWhiteSpace(edgesRaw))
        {
            error = "missing required edges list";
            return false;
        }

        var tokens = ParseSingleTokenArray(edgesRaw, out error);
        if (tokens is null)
        {
            return false;
        }

        if (tokens.Length != 1)
        {
            error = "bounded M5b supports exactly one explicit internal edge token";
            return false;
        }

        edge = tokens[0] switch
        {
            "inner_x_min_y_min" => BrepBoundedManufacturingFilletEdge.InnerXMinYMin,
            "inner_x_min_y_max" => BrepBoundedManufacturingFilletEdge.InnerXMinYMax,
            "inner_x_max_y_min" => BrepBoundedManufacturingFilletEdge.InnerXMaxYMin,
            "inner_x_max_y_max" => BrepBoundedManufacturingFilletEdge.InnerXMaxYMax,
            _ => BrepBoundedManufacturingFilletEdge.InnerXMinYMin
        };

        if (tokens[0] is not ("inner_x_min_y_min" or "inner_x_min_y_max" or "inner_x_max_y_min" or "inner_x_max_y_max"))
        {
            error = "supported tokens are inner_x_min_y_min, inner_x_min_y_max, inner_x_max_y_min, inner_x_max_y_max";
            return false;
        }

        return true;
    }

    private static string[]? ParseSingleTokenArray(string raw, out string error)
    {
        error = string.Empty;
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            error = "expected array-like edges value";
            return null;
        }

        return trimmed[1..^1]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim().Trim('"'))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();
    }
}
