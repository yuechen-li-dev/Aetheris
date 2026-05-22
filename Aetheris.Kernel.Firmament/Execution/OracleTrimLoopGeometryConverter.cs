using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class OracleTrimLoopGeometryConverter
{
    internal static bool TryConvertAnalyticCircle(
        SourceSurfaceDescriptor planarSource,
        TieredTrimCurveRepresentation oracleTrim,
        string? orderingToken,
        out RetainedCircularLoopGeometry geometry,
        out IReadOnlyList<string> diagnostics)
    {
        var d = new List<string>();
        geometry = default;

        if (planarSource.BoundedPlanarGeometry is not { Kind: BoundedPlanarPatchGeometryKind.Rectangle } rect)
        {
            d.Add("oracle-trim-conversion-rejected: source planar patch is not bounded rectangle geometry.");
            diagnostics = d;
            return false;
        }

        if (oracleTrim.Kind != TieredTrimRepresentationKind.AnalyticCircle || oracleTrim.Circle is null)
        {
            d.Add("oracle-trim-conversion-rejected: oracle trim is not analytic-circle.");
            diagnostics = d;
            return false;
        }

        var uAxis = rect.Corner10 - rect.Corner00;
        var vAxis = rect.Corner01 - rect.Corner00;
        var uLen = uAxis.Length;
        var vLen = vAxis.Length;
        if (uLen <= 1e-9 || vLen <= 1e-9)
        {
            d.Add("oracle-trim-conversion-rejected: rectangle parameterization axes are degenerate.");
            diagnostics = d;
            return false;
        }

        // T10 policy: require near-uniform UV-to-world scaling to guarantee circular world result.
        var relScaleDelta = Math.Abs(uLen - vLen) / Math.Max(uLen, vLen);
        if (relScaleDelta > 1e-6)
        {
            d.Add($"oracle-trim-conversion-rejected: non-uniform uv/world scale would yield non-circular world trim (u={uLen:G6}, v={vLen:G6}).");
            diagnostics = d;
            return false;
        }

        var uDir = uAxis / uLen;
        var vDir = vAxis / vLen;
        var c = oracleTrim.Circle;
        var center = rect.Center + (uDir * c.CenterU) + (vDir * c.CenterV);
        var radius = c.RadiusUV;

        var token = string.IsNullOrWhiteSpace(orderingToken) ? string.Empty : orderingToken!;
        if (string.IsNullOrWhiteSpace(token))
        {
            d.Add("oracle-trim-conversion-warning: internal ordering token unavailable from retained loop evidence.");
        }

        geometry = new RetainedCircularLoopGeometry(center, rect.Normal, radius, RetainedRegionLoopOrientationPolicy.ReverseForToolCavity, token,
            "oracle-trim-conversion-success: analytic-circle UV converted to retained world circular geometry.");
        d.Add("oracle-trim-conversion-success: analytic-circle UV converted to world circular geometry.");
        diagnostics = d;
        return true;
    }
}
