using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Construction;

/// <summary>
/// Immutable, deliberately bounded construction bridge. Consumers receive section geometry and
/// lineage, never arbitrary AIR nodes, route selectors, or BRep-plan internals.
/// </summary>
public sealed record ContinuumConstructionSection(double AxialPosition,IReadOnlyList<Point3D> ProfileVertices);

public sealed record ContinuumConstructionDescriptor(
    string SourceIdentity,
    string SemanticRegionIdentity,
    IReadOnlyList<ContinuumConstructionSection> Sections,
    IReadOnlyList<int> Correspondence,
    IReadOnlyList<string> AdmittedOperations,
    IReadOnlyList<string> Provenance)
{
    public ContinuumConstructionDescriptor Validate()
    {
        if(string.IsNullOrWhiteSpace(SourceIdentity)||string.IsNullOrWhiteSpace(SemanticRegionIdentity))throw new ArgumentException("Construction identities are required.");
        if(Sections.Count<2||Sections.Any(s=>s.ProfileVertices.Count<3))throw new ArgumentException("At least two polygonal sections are required.");
        if(Sections.Zip(Sections.Skip(1)).Any(pair=>pair.First.AxialPosition>=pair.Second.AxialPosition))throw new ArgumentException("Sections must be strictly axially ordered.");
        var count=Sections[0].ProfileVertices.Count;if(Sections.Any(s=>s.ProfileVertices.Count!=count)||Correspondence.Count!=count)throw new ArgumentException("Section correspondence must be complete.");
        if(Correspondence.Distinct().Count()!=count||Correspondence.Any(i=>i<0||i>=count))throw new ArgumentException("Correspondence must be a permutation.");
        return this;
    }
}
