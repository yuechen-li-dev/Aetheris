using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Server.Documents;

public sealed class DocumentSession
{
    private readonly Dictionary<Guid, BrepBody> _bodies = new();
    private readonly object _sync = new();

    public DocumentSession(Guid id, string? name = null)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }

    public string? Name { get; }

    public IReadOnlyDictionary<Guid, BrepBody> SnapshotBodies()
    {
        lock (_sync)
        {
            return _bodies.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value);
        }
    }

    public Guid AddBody(BrepBody body)
    {
        var bodyId = Guid.NewGuid();
        lock (_sync)
        {
            _bodies[bodyId] = body;
        }

        return bodyId;
    }

    public bool TryGetBody(Guid bodyId, out BrepBody body)
    {
        lock (_sync)
        {
            return _bodies.TryGetValue(bodyId, out body!);
        }
    }

    public bool ReplaceBody(Guid bodyId, BrepBody body)
    {
        lock (_sync)
        {
            if (!_bodies.ContainsKey(bodyId))
            {
                return false;
            }

            _bodies[bodyId] = body;
            return true;
        }
    }
}
