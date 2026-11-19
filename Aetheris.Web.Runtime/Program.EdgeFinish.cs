using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Web.Runtime;

public static partial class Program
{
    /// <summary>What a single-edge finish trial needs from the last successful part build.</summary>
    private sealed record FinishContext(string Source, string? Directory, IReadOnlyDictionary<string, string>? Resources,
        SemanticTopologyCorrespondence Correspondence);

    private sealed partial class WebModelSession
    {
        private FinishContext? _finishContext;
        private FinishContext? _pendingFinishContext;
        private readonly Dictionary<(int Revision, string Kind), IReadOnlyList<SingleEdgeFinishAdmission>> _admissions = [];

        /// <summary>
        /// Every edge the current build offers a single-edge finish on, each admitted or refused by a trial
        /// build of that finish (<see cref="SingleEdgeFinishTrial"/>). Cached per revision and kind.
        /// </summary>
        public object EdgeFinishAdmission(WebRequest request)
        {
            var (context, kind) = FinishRequest(request);
            if (!_admissions.TryGetValue((Revision, kind), out var admissions))
            {
                admissions = SingleEdgeFinishTrial.Admit(context.Source, context.Directory, context.Resources, context.Correspondence, kind,
                    TimeSpan.FromSeconds(20));
                foreach (var key in _admissions.Keys.Where(k => k.Revision != Revision).ToArray()) _admissions.Remove(key);
                _admissions[(Revision, kind)] = admissions;
            }
            return new
            {
                revision = Revision, kind,
                edges = admissions.Select(a => new
                {
                    edgeIds = a.EdgeIds.Select(id => $"edge:{id}").ToArray(), body = a.Body, face = a.Face, target = a.Target, label = a.Label,
                    admitted = a.Admitted, probeMm = a.AdmittedSize, code = a.Code,
                    reason = a.Admitted ? null : SingleEdgeFinishTrial.Explain(a.Code!, a.Reason!, kind)
                }).ToArray()
            };
        }

        /// <summary>The sizes the finish on one admitted edge builds at.</summary>
        public object EdgeFinishRange(WebRequest request)
        {
            var (context, kind) = FinishRequest(request);
            if (request.EdgeId is not { } edgeId) throw new WebRuntimeException("edge-finish-request-invalid", "An edgeId is required.");
            EdgeFinishAdmission(request);
            var admission = _admissions[(Revision, kind)].FirstOrDefault(a => a.EdgeIds.Contains(edgeId))
                ?? throw new WebRuntimeException("edge-finish-not-offered", "This edge has no source address a finish can name.");
            if (!admission.Admitted)
                throw new WebRuntimeException("edge-finish-not-admitted", SingleEdgeFinishTrial.Explain(admission.Code!, admission.Reason!, kind));
            var range = SingleEdgeFinishTrial.Range(context.Source, context.Directory, context.Resources,
                new(admission.Body, admission.Face, admission.Target, admission.Label), kind, admission.AdmittedSize!.Value);
            return new
            {
                revision = Revision, kind, edgeId = $"edge:{edgeId}",
                minimumMm = range?.MinimumMm, maximumMm = range?.MaximumMm, maximumIsSearchLimit = range?.MaximumIsSearchLimit ?? false
            };
        }

        private (FinishContext Context, string Kind) FinishRequest(WebRequest request)
        {
            if (_dirty || request.BuildRevision != Revision)
                throw new WebRuntimeException("edge-finish-stale-revision", "Build the current source successfully before checking edge finishes.");
            var kind = request.FinishKind switch
            {
                "fillet" or "Fillet" => "Fillet",
                "chamfer" or "Chamfer" => "Chamfer",
                _ => throw new WebRuntimeException("edge-finish-request-invalid", "finishKind is fillet or chamfer.")
            };
            return (_finishContext ?? throw new WebRuntimeException("edge-finish-unavailable",
                "This build has no source-owned part topology to finish."), kind);
        }
    }
}
