using Aetheris.Kernel.Core.Diagnostics;
namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Retains compatibility messages while projecting located domain errors through the common CLI/SDK contract.</summary>
public static class FirmamentDomainDiagnostics
{
    public static IReadOnlyList<KernelDiagnostic> Present(IReadOnlyList<string> messages,IReadOnlyList<FirmamentV2ValidationDiagnostic> frontend,string owner)
    {
        var spans=frontend.GroupBy(d=>d.Code.Split(':')[0]).ToDictionary(g=>g.Key,g=>new Queue<FirmamentV2ValidationDiagnostic>(g));
        return messages.Select(message=>
        {
            var code=message.Split(':')[0];
            var located=spans.TryGetValue(code,out var pending)&&pending.Count>0?pending.Dequeue():null;
            return new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed,KernelDiagnosticSeverity.Error,located is null?message:code+": "+located.Message,owner)
            {OriginalSourceSpan=located?.SourceSpan is {} span?new(span.Start,span.Length){LocationNote=located.LocationNote}:null};
        }).ToArray();
    }
}
