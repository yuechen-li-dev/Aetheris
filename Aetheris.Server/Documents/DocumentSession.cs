using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Server.Documents;

// M25 lightweight assembly model:
// - BodyDefinition stores canonical geometry.
// - BodyOccurrence references a definition and carries instance placement.
public sealed class DocumentSession
{
    private readonly Dictionary<Guid, BodyDefinition> _definitions = new();
    private readonly Dictionary<Guid, BodyOccurrence> _occurrences = new();
    private readonly object _sync = new();

    public DocumentSession(Guid id, string? name = null)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }

    public string? Name { get; }

    public IReadOnlyList<BodyOccurrence> SnapshotOccurrences()
    {
        lock (_sync)
        {
            return _occurrences.Values
                .Select(static occurrence => occurrence with { })
                .OrderBy(static occurrence => occurrence.OccurrenceId)
                .ToArray();
        }
    }

    public Guid AddBody(BrepBody body, string? occurrenceName = null)
    {
        lock (_sync)
        {
            var definitionId = Guid.NewGuid();
            var occurrenceId = Guid.NewGuid();
            _definitions[definitionId] = new BodyDefinition(definitionId, body);
            _occurrences[occurrenceId] = new BodyOccurrence(occurrenceId, definitionId, Transform3D.Identity, occurrenceName);
            return occurrenceId;
        }
    }

    public bool TryGetOccurrence(Guid occurrenceId, out BodyOccurrence occurrence)
    {
        lock (_sync)
        {
            if (_occurrences.TryGetValue(occurrenceId, out var stored))
            {
                occurrence = stored;
                return true;
            }

            occurrence = default;
            return false;
        }
    }

    public bool TryGetBody(Guid occurrenceId, out BrepBody body)
    {
        lock (_sync)
        {
            if (!_occurrences.TryGetValue(occurrenceId, out var occurrence))
            {
                body = default!;
                return false;
            }

            if (!_definitions.TryGetValue(occurrence.DefinitionId, out var definition))
            {
                body = default!;
                return false;
            }

            body = definition.Body;
            return true;
        }
    }

    public bool ReplaceBody(Guid occurrenceId, BrepBody body)
    {
        lock (_sync)
        {
            if (!_occurrences.TryGetValue(occurrenceId, out var occurrence))
            {
                return false;
            }

            if (!_definitions.ContainsKey(occurrence.DefinitionId))
            {
                return false;
            }

            _definitions[occurrence.DefinitionId] = new BodyDefinition(occurrence.DefinitionId, body);
            return true;
        }
    }

    public bool TryGetBodyTransform(Guid occurrenceId, out Transform3D transform)
    {
        lock (_sync)
        {
            if (_occurrences.TryGetValue(occurrenceId, out var occurrence))
            {
                transform = occurrence.Placement;
                return true;
            }

            transform = Transform3D.Identity;
            return false;
        }
    }

    public bool ApplyBodyTranslation(Guid occurrenceId, Vector3D translation, out Transform3D updatedTransform)
    {
        lock (_sync)
        {
            if (!_occurrences.TryGetValue(occurrenceId, out var occurrence))
            {
                updatedTransform = Transform3D.Identity;
                return false;
            }

            var delta = Transform3D.CreateTranslation(translation);
            updatedTransform = delta * occurrence.Placement;
            _occurrences[occurrenceId] = occurrence with { Placement = updatedTransform };
            return true;
        }
    }

    public bool TryCreateOccurrence(Guid sourceOccurrenceId, out BodyOccurrence occurrence)
    {
        lock (_sync)
        {
            if (!_occurrences.TryGetValue(sourceOccurrenceId, out var source))
            {
                occurrence = default;
                return false;
            }

            var newOccurrence = new BodyOccurrence(Guid.NewGuid(), source.DefinitionId, Transform3D.Identity, source.Name);
            _occurrences[newOccurrence.OccurrenceId] = newOccurrence;
            occurrence = newOccurrence;
            return true;
        }
    }
}

public readonly record struct BodyDefinition(Guid DefinitionId, BrepBody Body);

public readonly record struct BodyOccurrence(Guid OccurrenceId, Guid DefinitionId, Transform3D Placement, string? Name);
