using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentManifoldChecker
{
    public static bool IsManifold(BrepBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var topology = body.Topology;
        var edgeIncidenceCounts = new Dictionary<EdgeId, int>();

        foreach (var face in topology.Faces)
        {
            foreach (var loopId in face.LoopIds)
            {
                if (!topology.TryGetLoop(loopId, out var loop) || loop is null)
                {
                    continue;
                }

                foreach (var coedgeId in loop.CoedgeIds)
                {
                    if (!topology.TryGetCoedge(coedgeId, out var coedge) || coedge is null)
                    {
                        continue;
                    }

                    edgeIncidenceCounts.TryGetValue(coedge.EdgeId, out var incidenceCount);
                    edgeIncidenceCounts[coedge.EdgeId] = incidenceCount + 1;
                }
            }
        }

        foreach (var edge in topology.Edges)
        {
            var incidenceCount = edgeIncidenceCounts.TryGetValue(edge.Id, out var count)
                ? count
                : 0;

            if (incidenceCount != 2)
            {
                return false;
            }
        }

        return true;
    }
}
