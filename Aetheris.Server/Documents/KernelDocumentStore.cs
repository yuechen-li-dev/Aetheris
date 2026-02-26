namespace Aetheris.Server.Documents;

public sealed class KernelDocumentStore
{
    private readonly Dictionary<Guid, DocumentSession> _documents = new();
    private readonly object _sync = new();

    public DocumentSession Create(string? name = null)
    {
        var session = new DocumentSession(Guid.NewGuid(), name);
        lock (_sync)
        {
            _documents[session.Id] = session;
        }

        return session;
    }

    public bool TryGet(Guid documentId, out DocumentSession session)
    {
        lock (_sync)
        {
            return _documents.TryGetValue(documentId, out session!);
        }
    }
}
