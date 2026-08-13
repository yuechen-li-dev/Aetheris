using Aetheris.Surfacing;

namespace Aetheris.Reconstruction;

/// <summary>Normal public Surfacing bridge: a recovered expression becomes a real bounded PanelIr with approximation evidence.</summary>
public static class RecoveredPanelMaterializer
{
    public static PanelResult Materialize(RecoveredChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        if (chart.Status != "Accepted") return new(null, [new("reconstruction-chart-rejected", $"Chart '{chart.StableId}' is not accepted: {chart.Status}.")]);
        if (chart.Patch.PointExpression is null) return new(null, [new("reconstruction-expression-unavailable", $"Chart '{chart.StableId}' has no expression tree to materialize.")]);
        var certificate = new ApproximationCertificate(chart.MaxResidual, chart.MaxResidual, null, 3, 3,
            "deterministic least-squares quadratic height fit", chart.StableId);
        return PanelFactory.FromRecoveredQuadratic(chart.Patch, certificate,
            [new(chart.StableId, $"source-triangles:{string.Join(',', chart.SourceTriangles)}", "surface-reconstruction")]);
    }
}
