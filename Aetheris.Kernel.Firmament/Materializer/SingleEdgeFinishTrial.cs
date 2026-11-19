using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Whether one offered edge takes a finish of one kind, and why not when it does not.</summary>
public sealed record SingleEdgeFinishAdmission(
    string Body, string Face, string Target, string Label, string Kind, IReadOnlyList<int> EdgeIds,
    bool Admitted, double? AdmittedSize, string? Code, string? Reason);

/// <summary>The sizes one edge's finish builds at, as far as bisection between trial builds finds them.</summary>
public sealed record SingleEdgeFinishRange(double MinimumMm, double MaximumMm, bool MaximumIsSearchLimit);

/// <summary>
/// The admission for a single-edge fillet or chamfer is a trial build of the real model: the finish is
/// inserted exactly as an editor inserts it (the palette template, through <c>insertFeature</c>, with
/// Face, Target and size set) and the document is compiled. An edge is offered only when that build
/// succeeds, so the offer and the build cannot disagree; a refusal carries the compiler's own diagnostic.
/// <see cref="SingleEdgeFinishSelection"/> stays the cheap candidate list: which edges have a source
/// address a finish can name.
/// </summary>
public static partial class SingleEdgeFinishTrial
{
    /// <summary>The first trial size: under every route's size limits, over their 0.01 mm clearances.</summary>
    public const double ProbeMm = 0.05;
    /// <summary>The second trial size, for routes with a minimum above <see cref="ProbeMm"/>.</summary>
    public const double FallbackProbeMm = 1.0;
    /// <summary>The largest size the range search tries.</summary>
    public const double SearchLimitMm = 1000.0;

    public static IReadOnlyList<string> Kinds { get; } = ["Fillet", "Chamfer"];

    /// <summary>The source with one finish inserted on <paramref name="selection"/>, or the edit's refusal.</summary>
    public static (string? Source, string? Error) Insert(string source, SingleEdgeFinishSelection selection, string kind, double sizeMm)
    {
        if (!Kinds.Contains(kind)) return (null, $"Unknown finish kind '{kind}'.");
        var palette = FirmamentLanguagePaletteService.Palette().Items.Single(i => i.Id == (kind == "Fillet" ? "fillet" : "chamfer"));
        try
        {
            var outline = FirmamentLanguageOutlineService.Outline(source, "trial", "0");
            var body = outline.Nodes.FirstOrDefault(n => n.Name == selection.Body && n.Kind is not ("Modify" or "modify") && !n.Call);
            if (body is null) return (null, $"EdgeFinishBodyNotInDocument: The body '{selection.Body}' is not declared in this document.");
            var name = UniqueName(source, kind == "Fillet" ? "EdgeFillet" : "EdgeChamfer");
            var inserted = FirmamentLanguageEditService.Apply(source, "trial", "0",
                [new("insertFeature") { Node = body.Id, Source = palette.Source, Name = name }]);
            var node = inserted.Nodes.Single();
            var size = sizeMm.ToString("0.######", CultureInfo.InvariantCulture) + "mm";
            var edited = FirmamentLanguageEditService.Apply(inserted.Text, "trial", "1", [
                new("setField") { Node = node, Field = "Face", Value = selection.Face },
                new("setField") { Node = node, Field = "Target", Value = selection.Target },
                new("setField") { Node = node, Field = kind == "Fillet" ? "Radius" : "Distance", Value = size }]);
            return (edited.Text, null);
        }
        catch (FirmamentLanguageEditException error) { return (null, $"{error.Code}: {error.Message}"); }
    }

    /// <summary>Build the model with the finish; null when it builds, else the first error.</summary>
    public static string? Try(string source, string? sourceDirectory, IReadOnlyDictionary<string, string>? resources,
        SingleEdgeFinishSelection selection, string kind, double sizeMm)
    {
        var (edited, error) = Insert(source, selection, kind, sizeMm);
        if (edited is null) return error;
        KernelResult<FirmamentStepExportResult> build;
        try { build = FirmamentBuildAndExport.CompileSource(edited, sourceDirectory, resources); }
        catch (Exception exception) { return $"edge-finish-trial-exception: {exception.GetType().Name}: {exception.Message}"; }
        if (build.IsSuccess) return null;
        var first = build.Diagnostics.FirstOrDefault(d => d.Severity == Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error)
            ?? build.Diagnostics.FirstOrDefault();
        return first?.Message ?? "The model did not build.";
    }

    /// <summary>Every offered edge's admission for one finish kind, grouped by the finish address the edges share.</summary>
    public static IReadOnlyList<SingleEdgeFinishAdmission> Admit(string source, string? sourceDirectory,
        IReadOnlyDictionary<string, string>? resources, SemanticTopologyCorrespondence correspondence, string kind,
        TimeSpan? budget = null)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var output = new List<SingleEdgeFinishAdmission>();
        foreach (var group in SingleEdgeFinishSelection.Index(correspondence)
                     .GroupBy(e => (e.Value.Body, e.Value.Face, e.Value.Target))
                     .OrderBy(g => g.Min(e => e.Key.Value)))
        {
            var selection = group.First().Value;
            var edges = group.Select(e => e.Key.Value).Order().ToArray();
            if (kind == "Fillet" && !selection.FilletSupported)
            {
                output.Add(new(selection.Body, selection.Face, selection.Target, selection.Label, kind, edges, false, null,
                    "EdgeFinishFilletNotOffered", "This edge runs around an end of the extrusion, where fillets are not supported yet. A chamfer works here, or fillet an edge that runs along the extrusion."));
                continue;
            }
            if (budget is { } limit && started.Elapsed > limit)
            {
                output.Add(new(selection.Body, selection.Face, selection.Target, selection.Label, kind, edges, false, null,
                    "EdgeFinishTrialNotRun", "Not checked: this model has too many edges to check them all in time. Select the edge to check it alone."));
                continue;
            }
            var first = Try(source, sourceDirectory, resources, selection, kind, ProbeMm);
            if (first is null) { output.Add(new(selection.Body, selection.Face, selection.Target, selection.Label, kind, edges, true, ProbeMm, null, null)); continue; }
            var second = Try(source, sourceDirectory, resources, selection, kind, FallbackProbeMm);
            output.Add(second is null
                ? new(selection.Body, selection.Face, selection.Target, selection.Label, kind, edges, true, FallbackProbeMm, null, null)
                : new(selection.Body, selection.Face, selection.Target, selection.Label, kind, edges, false, null, Code(first), first));
        }
        return output;
    }

    /// <summary>
    /// The sizes the finish builds at, from an admitted size: doubling to the first failure, then bisecting
    /// each bound to <paramref name="resolutionMm"/>. It assumes the admitted sizes form one interval; a
    /// build that fails inside it is a kernel fault, which the offer contract tests look for.
    /// </summary>
    public static SingleEdgeFinishRange? Range(string source, string? sourceDirectory, IReadOnlyDictionary<string, string>? resources,
        SingleEdgeFinishSelection selection, string kind, double admittedMm, double resolutionMm = 0.01)
    {
        bool Builds(double size) => Try(source, sourceDirectory, resources, selection, kind, size) is null;
        if (!Builds(admittedMm)) return null;
        var low = admittedMm; var high = admittedMm;
        double? fail = null;
        while (high < SearchLimitMm)
        {
            var next = Math.Min(high * 2, SearchLimitMm);
            if (!Builds(next)) { fail = next; break; }
            high = next;
        }
        if (fail is { } bad)
            while (bad - high > resolutionMm) { var mid = (high + bad) / 2; if (Builds(mid)) high = mid; else bad = mid; }
        var floor = 0d;
        while (low - floor > resolutionMm) { var mid = (low + floor) / 2; if (Builds(mid)) low = mid; else floor = mid; }
        // Shown to people: round inward to the resolution, so both ends still build.
        var minimum = Math.Ceiling(low / resolutionMm - 1e-9) * resolutionMm;
        var maximum = Math.Floor(high / resolutionMm + 1e-9) * resolutionMm;
        if (maximum < minimum) (minimum, maximum) = (low, high);
        return new(Round(minimum), Round(maximum), fail is null);
    }

    /// <summary>The leading identifier of a compiler message, such as <c>ProfileRimFinishHistoryUnsupported</c>.</summary>
    public static string Code(string message)
    {
        var trimmed = message.Trim();
        if (BareCodePattern().IsMatch(trimmed)) return trimmed;
        var match = CodePattern().Match(trimmed);
        return match.Success ? match.Groups["code"].Value : "EdgeFinishRefused";
    }

    /// <summary>A refusal in the terms of what the user picked, keyed by the compiler's code.</summary>
    public static string Explain(string code, string message, string kind)
    {
        var finish = kind == "Fillet" ? "fillet" : "chamfer";
        var detail = message.Contains(':') ? message[(message.IndexOf(':') + 1)..].Trim() : message;
        if (code.StartsWith("firmament-v2-assert-", StringComparison.Ordinal))
            return $"A {finish} here breaks one of this model's own Assert checks. {detail}";
        if (code.StartsWith("pmi-", StringComparison.Ordinal) || code.Contains("-pmi-", StringComparison.Ordinal))
            return $"A {finish} here leaves this model's PMI without the faces it is attached to. {detail}";
        return code switch
        {
            "ProfileRimFinishHistoryUnsupported" =>
                $"Another operation on this body reaches this edge's face, so the {finish} cannot be added after it yet.",
            "ProfileBoundaryEdgeFinishSequenceUnsupported" =>
                $"This body already has an edge finish, and this kind of body takes one finish so far.",
            "ProfileBoundaryChamferComposeOpenChainUnsupported" =>
                "On a body with other operations, only a face's whole rim can be chamfered, not one of its edges.",
            "ProfileBoundaryChamferComposeBottomUnsupported" =>
                "On a body with other operations, only the top rim can be chamfered.",
            "ProfileBoundaryChamferInnerLoopUnsupported" =>
                $"Edges of a profile with openings cannot take a {finish} yet.",
            "ProfileBoundaryChamferSegmentUnknown" or "ProfileBoundaryChamferProfileUnknown" =>
                $"This edge comes from a curve the {finish} cannot name yet.",
            "ProfileBoundaryChamferArcMaterializationNotImplemented" =>
                "Chamfers on curved edges are not supported yet.",
            "longitudinal-edge-finish" => detail.Contains(':') ? detail[(detail.IndexOf(':') + 1)..].Trim() : detail,
            // The compiler's text tells an author to add EndClearance; a person picking the edge needs the cause.
            "ProfileBoundaryFilletFullLengthCornerUnsupported" => detail.Contains("inside corner", StringComparison.Ordinal)
                ? "This edge runs into an inside corner, where a fillet along the whole edge is not supported yet. A chamfer works here."
                : "This edge meets a neighbouring edge at an angle that is not square, where a fillet along the whole edge is not supported yet. A chamfer works here.",
            "ProfileBoundaryFilletRadiusExceedsNeighbour" => detail,
            "EdgeFinishBodyNotInDocument" =>
                $"This edge belongs to a body made by a library part or template, so a {finish} cannot be written here. Finish it where that part is defined.",
            _ => message
        };
    }

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.ToZero);

    private static string UniqueName(string source, string stem)
    {
        var name = stem;
        for (var i = 2; Regex.IsMatch(source, $@"\b{name}\b", RegexOptions.CultureInvariant); i++) name = stem + i;
        return name;
    }

    [GeneratedRegex(@"^(?<code>[A-Za-z][A-Za-z0-9.\-]*)(?=:)", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex BareCodePattern();
}
