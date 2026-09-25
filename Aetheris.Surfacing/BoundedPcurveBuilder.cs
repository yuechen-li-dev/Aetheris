using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Surfacing;

public sealed record PcurveBuildResult(bool IsSuccess, int Count, double MaximumResidual, IReadOnlyList<SculptDiagnostic> Diagnostics);

/// <summary>Compatibility facade for the shared kernel pcurve recovery path.</summary>
public static class BoundedPcurveBuilder
{
    public static PcurveBuildResult Populate(TopologyModel topology, BrepGeometryStore geometry, BrepBindingModel bindings,
        double tolerance = 1e-5, int sampleCount = 129)
    {
        var result = BrepPcurveRecovery.Populate(topology, geometry, bindings, tolerance, sampleCount);
        return new PcurveBuildResult(result.IsSuccess, result.Count, result.MaximumResidual,
            result.Diagnostics.Select(d => new SculptDiagnostic(d.Code, d.Message, d.Entity)).ToArray());
    }
}
