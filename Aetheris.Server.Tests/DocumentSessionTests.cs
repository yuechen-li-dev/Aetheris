using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Server.Documents;

namespace Aetheris.Server.Tests;

public sealed class DocumentSessionTests
{
    [Fact]
    public void AddBody_CreatesDefinitionAndInitialOccurrence()
    {
        var session = new DocumentSession(Guid.NewGuid());
        var body = BrepPrimitives.CreateBox(1, 1, 1).Value;

        var created = session.AddBody(body);

        Assert.Single(session.SnapshotDefinitions());
        Assert.Single(session.SnapshotOccurrences());
        Assert.True(session.TryGetOccurrence(created.OccurrenceId, out var occurrence));
        Assert.Equal(created.DefinitionId, occurrence.DefinitionId);
    }

    [Fact]
    public void CreateOccurrenceFromOccurrence_ReusesDefinition()
    {
        var session = new DocumentSession(Guid.NewGuid());
        var body = BrepPrimitives.CreateBox(1, 1, 1).Value;
        var created = session.AddBody(body);

        var success = session.TryCreateOccurrenceFromOccurrence(created.OccurrenceId, out var second);

        Assert.True(success);
        Assert.NotEqual(created.OccurrenceId, second.OccurrenceId);
        Assert.Equal(created.DefinitionId, second.DefinitionId);
    }

    [Fact]
    public void TranslateOccurrence_DoesNotMoveSiblingOccurrence()
    {
        var session = new DocumentSession(Guid.NewGuid());
        var body = BrepPrimitives.CreateBox(1, 1, 1).Value;
        var created = session.AddBody(body);
        session.TryCreateOccurrenceFromOccurrence(created.OccurrenceId, out var sibling);

        var moved = session.ApplyBodyTranslation(created.OccurrenceId, new Vector3D(5, 0, 0), out var movedTransform);
        var hasSiblingTransform = session.TryGetBodyTransform(sibling.OccurrenceId, out var siblingTransform);

        Assert.True(moved);
        Assert.True(hasSiblingTransform);
        Assert.Equal(5d, movedTransform.Apply(new Point3D(0, 0, 0)).X);
        Assert.Equal(0d, siblingTransform.Apply(new Point3D(0, 0, 0)).X);
    }
}
