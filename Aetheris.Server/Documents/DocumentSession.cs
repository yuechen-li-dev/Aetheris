using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Geometry;

namespace Aetheris.Server.Documents;

public sealed class DocumentSession
{
    private readonly Dictionary<Guid, BrepBody> _definitions = new();
    private readonly Dictionary<Guid, BodyOccurrence> _occurrences = new();
    private readonly object _sync = new();
    private long _revision;
    private long _measurementRevision = -1;
    private IReadOnlyList<MeasurementTarget> _measurementTargets = [];
    private IReadOnlyList<MeasurementTarget> _importedMeasurementTargets = [];
    public long Revision { get { lock (_sync) return _revision; } }

    public void SetImportedMeasurementTargets(IReadOnlyList<MeasurementTarget> targets)
    {
        lock (_sync) { _importedMeasurementTargets = targets; _revision++; }
    }

    public MeasurementResult Measure(long revision, MeasurementRequest request)
    {
        lock (_sync)
        {
            if (revision != _revision) throw new MeasurementException("measurement-stale-revision", "The document changed. Reload the displayed geometry before measuring.");
            if (_measurementRevision != _revision)
            {
                _measurementTargets = _occurrences.Values.Select(o => new MeasurementTarget(o.OccurrenceId.ToString(), _definitions[o.DefinitionId], o.Placement))
                    .Concat(_importedMeasurementTargets).ToArray();
                _measurementRevision = _revision;
            }
            return BrepMeasurementQuery.Measure(_measurementTargets, request);
        }
    }

    public DocumentSession(Guid id, string? name = null)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }

    public string? Name { get; }

    public IReadOnlyDictionary<Guid, BrepBody> SnapshotDefinitions()
    {
        lock (_sync)
        {
            return _definitions.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value);
        }
    }

    public IReadOnlyDictionary<Guid, BodyOccurrence> SnapshotOccurrences()
    {
        lock (_sync)
        {
            return _occurrences.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value);
        }
    }

    public (Guid OccurrenceId, Guid DefinitionId) AddBody(BrepBody body, string? occurrenceName = null)
    {
        lock (_sync)
        {
            var definitionId = Guid.NewGuid();
            var occurrenceId = Guid.NewGuid();
            _definitions[definitionId] = body;
            _occurrences[occurrenceId] = new BodyOccurrence(occurrenceId, definitionId, Transform3D.Identity, occurrenceName);
            _revision++;
            return (occurrenceId, definitionId);
        }
    }

    public bool TryGetOccurrence(Guid occurrenceId, out BodyOccurrence occurrence)
    {
        lock (_sync)
        {
            return _occurrences.TryGetValue(occurrenceId, out occurrence);
        }
    }

    public bool TryGetBody(Guid occurrenceId, out BrepBody body)
    {
        lock (_sync)
        {
            if (!_occurrences.TryGetValue(occurrenceId, out var occurrence))
            {
                body = null!;
                return false;
            }

            return _definitions.TryGetValue(occurrence.DefinitionId, out body!);
        }
    }

    public bool TryGetDefinition(Guid definitionId, out BrepBody body)
    {
        lock (_sync)
        {
            return _definitions.TryGetValue(definitionId, out body!);
        }
    }

    public bool TryCreateOccurrence(Guid definitionId, out BodyOccurrence occurrence, string? name = null)
    {
        lock (_sync)
        {
            if (!_definitions.ContainsKey(definitionId))
            {
                occurrence = default;
                return false;
            }

            var occurrenceId = Guid.NewGuid();
            occurrence = new BodyOccurrence(occurrenceId, definitionId, Transform3D.Identity, name);
            _occurrences[occurrenceId] = occurrence;
            _revision++;
            return true;
        }
    }

    public bool TryCreateOccurrenceFromOccurrence(Guid sourceOccurrenceId, out BodyOccurrence occurrence, string? name = null)
    {
        lock (_sync)
        {
            if (!_occurrences.TryGetValue(sourceOccurrenceId, out var source))
            {
                occurrence = default;
                return false;
            }

            var occurrenceId = Guid.NewGuid();
            occurrence = new BodyOccurrence(occurrenceId, source.DefinitionId, Transform3D.Identity, name);
            _occurrences[occurrenceId] = occurrence;
            _revision++;
            return true;
        }
    }

    public (Guid OccurrenceId, Guid DefinitionId) AddDefinitionAndOccurrence(BrepBody body, string? occurrenceName = null)
        => AddBody(body, occurrenceName);

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
            _revision++;
            return true;
        }
    }
}

public readonly record struct BodyOccurrence(Guid OccurrenceId, Guid DefinitionId, Transform3D Placement, string? Name);
