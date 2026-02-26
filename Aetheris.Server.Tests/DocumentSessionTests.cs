using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Server.Documents;

namespace Aetheris.Server.Tests;

public sealed class DocumentSessionTests
{
    [Fact]
    public void AddBody_CreatesDefinitionAndOccurrence()
    {
        var session = new DocumentSession(Guid.NewGuid(), "test");
        var body = BrepPrimitives.CreateBox(1, 1, 1).Value;

        var occurrenceId = session.AddBody(body);

        Assert.True(session.TryGetOccurrence(occurrenceId, out var occurrence));
        Assert.True(session.TryGetBody(occurrenceId, out var loaded));
        Assert.Equal(body.Topology.Faces.Count(), loaded.Topology.Faces.Count());
        Assert.NotEqual(Guid.Empty, occurrence.DefinitionId);
    }

    [Fact]
    public void CreateOccurrence_ReusesDefinition_AndMaintainsIndependentPlacement()
    {
        var session = new DocumentSession(Guid.NewGuid(), "test");
        var body = BrepPrimitives.CreateBox(1, 1, 1).Value;
        var primaryOccurrence = session.AddBody(body);

        var created = session.TryCreateOccurrence(primaryOccurrence, out var secondaryOccurrence);
        var translated = session.ApplyBodyTranslation(primaryOccurrence, new Vector3D(5, 0, 0), out var transformed);

        Assert.True(created);
        Assert.True(translated);
        Assert.True(session.TryGetOccurrence(primaryOccurrence, out var first));
        Assert.True(session.TryGetOccurrence(secondaryOccurrence.OccurrenceId, out var second));
        Assert.Equal(first.DefinitionId, second.DefinitionId);
        Assert.NotEqual(first.Placement, second.Placement);
        Assert.Equal(transformed, first.Placement);
        Assert.Equal(Transform3D.Identity, second.Placement);
    }
}
