using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentSelectorResolver
{
    public static bool TryResolve(
        string selectorTarget,
        IReadOnlyDictionary<string, BrepBody> featureBodies,
        FirmamentSelectorResultKind resultKind,
        out FirmamentSelectorResolution resolution)
    {
        resolution = null!;

        var separatorIndex = selectorTarget.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= selectorTarget.Length - 1)
        {
            return false;
        }

        var featureId = selectorTarget[..separatorIndex];
        var port = selectorTarget[(separatorIndex + 1)..];

        if (!featureBodies.TryGetValue(featureId, out var body))
        {
            return false;
        }

        var count = resultKind switch
        {
            FirmamentSelectorResultKind.Face or FirmamentSelectorResultKind.FaceSet => body.Topology.Faces.Count(),
            FirmamentSelectorResultKind.EdgeSet => body.Topology.Edges.Count(),
            FirmamentSelectorResultKind.VertexSet => body.Topology.Vertices.Count(),
            _ => 0
        };

        resolution = new FirmamentSelectorResolution(featureId, port, resultKind, count);
        return true;
    }
}

internal sealed record FirmamentSelectorResolution(
    string FeatureId,
    string Port,
    FirmamentSelectorResultKind ResultKind,
    int Count);
