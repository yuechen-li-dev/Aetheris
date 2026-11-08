using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Blends;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// <c>EdgeFinish&lt;Fillet&gt; { target: AllEdges }</c> on a body the dedicated Box/Polyhedron route does not admit:
/// build the body without that finish through the ordinary pipeline, then round every edge of the result
/// (<see cref="UniformRounding"/>: an opening by the ball, exact on convex bodies bounded by planes). Anything the
/// rounding cannot do yet (curved, spline or elliptical edges, concave bodies, a radius that does not fit) is
/// refused at the finish with the rounding's own reason.
/// </summary>
internal static class UniformRoundingRoute
{
    internal const string Code = "firmament-uniform-rounding";

    internal sealed record Request(string Stripped, double Radius, string FinishName, FirmamentV2SourceSpan Span, string? Model, string? Host);

    /// <summary>The one AllEdges fillet in the source and the source without it; null when there is none (or more than one,
    /// or it is a chamfer: those keep their existing diagnostics).</summary>
    internal static Request? Find(string source)
    {
        var document = FirmamentSyntaxParser.Parse(source).Document;
        var declarations = FirmamentSyntaxTraversal.Declarations(document).ToArray();
        var finishes = declarations.Where(d => d.Type.Name.Text == "EdgeFinish" && Field(d, "Target") is { } t && FirmamentSyntaxValues.Reference(t) == "AllEdges").ToArray();
        if (finishes.Length != 1) return null;
        var finish = finishes[0];
        var generic = finish.Type.Arguments.Count == 1 ? FirmamentSyntaxValues.Reference(finish.Type.Arguments[0].Value) : null;
        var kind = generic ?? FirmamentSyntaxValues.Reference(Field(finish, "Kind"));
        if (kind != "Fillet" || !FirmamentSyntaxValues.TryMeasure(Field(finish, "Radius"), "mm", out var radius)) return null;
        // Only a finish this route reads whole: any other field (a face selector, a distance, an invalid kind) keeps
        // the parser's own diagnostic.
        var fields = finish.Body?.Statements.OfType<FieldSyntax>().ToArray() ?? [];
        if (fields.Any(f => !new[] { "Target", "Radius", "Kind" }.Any(n => FirmamentSyntaxFields.SameField(f.Name.Text, n)))
            || Field(finish, "Kind") is { } k && FirmamentSyntaxValues.Reference(k) != "Fillet") return null;
        // Remove the finish; a Modify left empty goes with it.
        var owner = declarations.FirstOrDefault(d => d.Type.Name.Text == "Modify" && d.Body?.Statements.Contains(finish) == true);
        var span = owner is not null && owner.Body!.Statements.Count == 1 ? owner.Span : finish.Span;
        var stripped = source.Remove(span.Start, span.Length);
        var model = declarations.FirstOrDefault(d => d.Type.Name.Text == "Model")?.Name?.Text;
        return new(stripped, radius, finish.Name?.Text ?? "AllEdges", finish.Span, model, owner?.Name?.Text);
    }

    internal static KernelResult<FirmamentStepExportResult> Export(Request request, Func<string, KernelResult<FirmamentStepExportResult>> build)
    {
        var unfinished = build(request.Stripped);
        if (!unfinished.IsSuccess) return unfinished;
        var body = unfinished.Value.RuntimeBody;
        if (body is null)
        {
            var imported = Step242Importer.ImportBody(unfinished.Value.StepText);
            if (!imported.IsSuccess) return Refuse(request, "the body before rounding could not be read back: " + string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
            body = imported.Value;
        }
        var rounded = UniformRounding.RoundAllEdges(body, request.Radius);
        if (!rounded.IsSuccess) return Refuse(request, string.Join("; ", rounded.Diagnostics.Select(d => d.Message)));
        var step = Step242Exporter.ExportBody(rounded.Value, new Step242ExportOptions { ProductName = unfinished.Value.ExportedFeatureId });
        if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        return KernelResult<FirmamentStepExportResult>.Success(unfinished.Value with
        {
            StepText = step.Value,
            RuntimeBody = rounded.Value,
            // Correspondence and retained CIR described the body before rounding; they no longer hold.
            RuntimeCorrespondence = null,
            Cir = null,
            DocumentName = request.Model ?? unfinished.Value.DocumentName,
            MaterializedFeatures = [.. unfinished.Value.MaterializedFeatures ?? [], new FirmamentFeatureReceipt(request.Host ?? unfinished.Value.ExportedFeatureId, "EdgeFinish", request.FinishName)],
        }, unfinished.Diagnostics);
    }

    private static KernelResult<FirmamentStepExportResult> Refuse(Request request, string reason) => KernelResult<FirmamentStepExportResult>.Failure([
        new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
            $"{Code}: target: AllEdges (fillet every edge) on '{request.FinishName}': {reason}", "Firmament.UniformRounding")
        { SourceSpan = new(request.Span.Start, request.Span.Length) }]);

    private static ExpressionSyntax? Field(DeclarationSyntax d, string name) =>
        d.Body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name.Text, name))?.Value;
}
