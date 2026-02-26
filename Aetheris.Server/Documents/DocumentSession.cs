using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Server.Documents;

public sealed class DocumentSession
{
    private readonly Dictionary<Guid, BrepBody> _bodies = new();
    private readonly Dictionary<Guid, Transform3D> _bodyTransforms = new();
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
            _bodyTransforms[bodyId] = Transform3D.Identity;
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

    public bool TryGetBodyTransform(Guid bodyId, out Transform3D transform)
    {
        lock (_sync)
        {
            if (_bodyTransforms.TryGetValue(bodyId, out transform))
            {
                return true;
            }

            transform = Transform3D.Identity;
            return false;
        }
    }

    public bool ApplyBodyTranslation(Guid bodyId, Vector3D translation, out Transform3D updatedTransform)
    {
        lock (_sync)
        {
            if (!_bodies.ContainsKey(bodyId))
            {
                updatedTransform = Transform3D.Identity;
                return false;
            }

            var current = _bodyTransforms.GetValueOrDefault(bodyId, Transform3D.Identity);
            var delta = Transform3D.CreateTranslation(translation);
            updatedTransform = delta * current;
            _bodyTransforms[bodyId] = updatedTransform;
            return true;
        }
    }
}
