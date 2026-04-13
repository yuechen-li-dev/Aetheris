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
        out BrepBoundedChamferIncidentEdgePairSelector? incidentEdgePair,
        out BrepBoundedChamferCorner? corner,
        out string error)
    {
        edge = null;
        incidentEdgePair = null;
        corner = null;
        error = string.Empty;

        var hasEdges = fields.TryGetValue("edges", out var edgesRaw) && !string.IsNullOrWhiteSpace(edgesRaw);
        var hasCorners = fields.TryGetValue("corners", out var cornersRaw) && !string.IsNullOrWhiteSpace(cornersRaw);
        var hasCornerEdges = fields.TryGetValue("corner_edges", out var cornerEdgesRaw) && !string.IsNullOrWhiteSpace(cornerEdgesRaw);

        if (hasCornerEdges)
        {
            if (hasEdges)
            {
                error = "corner_edges selector mode does not allow edges";
                return false;
            }

            if (!hasCorners)
            {
                error = "corner_edges selector mode requires exactly one corner token";
                return false;
            }

            var cornerTokens = ParseSingleTokenArray(cornersRaw!, out error);
            if (cornerTokens is null)
            {
                return false;
            }

            if (cornerTokens.Length != 1)
            {
                error = "bounded E5b corner-edge selector supports exactly one corner token";
                return false;
            }

            if (cornerTokens[0] is not "x_max_y_max_z_max")
            {
                error = "supported corner tokens are x_max_y_max_z_max";
                return false;
            }

            var edgeTokens = ParseSingleTokenArray(cornerEdgesRaw!, out error);
            if (edgeTokens is null)
            {
                return false;
            }

            if (edgeTokens.Length != 2)
            {
                error = "bounded E5b corner-edge selector requires exactly two incident corner edge tokens";
                return false;
            }

            if (!TryParseIncidentCornerEdgeToken(edgeTokens[0], out var first)
                || !TryParseIncidentCornerEdgeToken(edgeTokens[1], out var second))
            {
                error = "supported corner edge tokens are x_neg, y_neg, z_neg";
                return false;
            }

            if (first == second)
            {
                error = "corner-edge selector requires two distinct incident edge tokens";
                return false;
            }

            corner = BrepBoundedChamferCorner.XMaxYMaxZMax;
            incidentEdgePair = new BrepBoundedChamferIncidentEdgePairSelector(corner.Value, first, second);
            return true;
        }

        if (hasEdges == hasCorners)
        {
            error = "provide exactly one selector family: edges, corners, or corners+corner_edges";
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
        edge = default;
        if (!TryParseChamferEdges(fields, out var edges, out error))
        {
            return false;
        }

        if (edges.Length != 1)
        {
            error = "bounded M5a supports exactly one explicit edge token";
            return false;
        }

        edge = edges[0];
        return true;
    }

    private static bool TryParseChamferEdges(
        IReadOnlyDictionary<string, string> fields,
        out BrepBoundedChamferEdge[] edges,
        out string error)
    {
        edges = [];
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

        var parsed = new BrepBoundedChamferEdge[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            parsed[i] = tokens[i] switch
            {
                "x_min_y_min" => BrepBoundedChamferEdge.XMinYMin,
                "x_min_y_max" => BrepBoundedChamferEdge.XMinYMax,
                "x_max_y_min" => BrepBoundedChamferEdge.XMaxYMin,
                "x_max_y_max" => BrepBoundedChamferEdge.XMaxYMax,
                "inner_x_min_y_min" => BrepBoundedChamferEdge.InnerXMinYMin,
                "inner_x_min_y_max" => BrepBoundedChamferEdge.InnerXMinYMax,
                "inner_x_max_y_min" => BrepBoundedChamferEdge.InnerXMaxYMin,
                "inner_x_max_y_max" => BrepBoundedChamferEdge.InnerXMaxYMax,
                _ => default
            };

            if (tokens[i] is not ("x_min_y_min" or "x_min_y_max" or "x_max_y_min" or "x_max_y_max"
                or "inner_x_min_y_min" or "inner_x_min_y_max" or "inner_x_max_y_min" or "inner_x_max_y_max"))
            {
                error = "supported tokens are x_min_y_min, x_min_y_max, x_max_y_min, x_max_y_max, inner_x_min_y_min, inner_x_min_y_max, inner_x_max_y_min, inner_x_max_y_max";
                return false;
            }
        }

        edges = parsed;
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

    private static bool TryParseIncidentCornerEdgeToken(string token, out BrepBoundedChamferCornerIncidentEdge edge)
    {
        edge = token switch
        {
            "x_neg" => BrepBoundedChamferCornerIncidentEdge.XNegative,
            "y_neg" => BrepBoundedChamferCornerIncidentEdge.YNegative,
            "z_neg" => BrepBoundedChamferCornerIncidentEdge.ZNegative,
            _ => default
        };

        return token is "x_neg" or "y_neg" or "z_neg";
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
