using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Collaboration;

/// <summary>Parses bounded, ordinary Firmament Review declarations into backend-independent ReviewIR.</summary>
public static partial class FirmamentReviewCompiler
{
    public const string AuthorRequired = "review-author-required";
    public const string DateRequired = "review-date-required";
    public const string DateInvalid = "review-date-invalid";
    public const string TargetUnknown = "review-target-unknown";
    public const string UnitMismatch = "review-proposal-unit-mismatch";
    public const string IdentityDuplicate = "review-identity-duplicate";

    public static ReviewCompilationResult Compile(
        string source,
        string sourcePath,
        IReadOnlyDictionary<string, (string? CurrentValue, IReadOnlyList<string> Capabilities)>? knownTargets = null)
    {
        var diagnostics = new List<string>();
        var located=new List<ReviewSourceDiagnostic>();
        void Add(string code,string message,FirmamentSyntaxScope owner,string? field=null)
        {
            var span=owner.Declaration.Body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f=>field is not null && FirmamentSyntaxFields.SameField(f.Name.Text,field))?.Name.Span??owner.Span;
            diagnostics.Add(code+": "+message);located.Add(new(code,message,new(sourcePath,span.Start,span.Length)));
        }
        var ranges = new List<(int, int)>();
        var threads = new List<ReviewThreadIr>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var syntax=FirmamentSyntaxParser.Parse(source,sourcePath);
        diagnostics.AddRange(syntax.Diagnostics.Select(d=>d.Code+": "+d.Message));
        located.AddRange(syntax.Diagnostics.Where(d=>d.SourceSpan is not null).Select(d=>new ReviewSourceDiagnostic(d.Code,d.Message,new(sourcePath,d.SourceSpan!.Start,d.SourceSpan.Length))));
        foreach (var declaration in syntax.Document.Statements.OfType<Aetheris.Kernel.Firmament.FirmamentV2.Syntax.DeclarationSyntax>()
            .SelectMany(d=>d.Type.Name.Text=="Model"?d.Body?.Statements.OfType<DeclarationSyntax>()??[]:[d]).Where(d=>d.Type.Name.Text=="Review"))
        {
            ranges.Add((declaration.Span.Start,declaration.Span.Length));
            var id = declaration.Name?.Text??"<missing>";
            var body=new FirmamentSyntaxScope(declaration,source,sourcePath);
            if (!identities.Add(id)) Add(IdentityDuplicate,id,body);
            var target = Field(body, "Target");
            var statusText = Field(body, "Status") ?? "Open";
            if (target is null) { Add("review-target-required",id,body); continue; }
            if (!Enum.TryParse<ReviewStatus>(statusText, true, out var status)) Add("review-status-invalid",$"{id}: {statusText}",body,"Status");
            var targetInfo = knownTargets?.GetValueOrDefault(target);
            if (knownTargets is not null && !knownTargets.ContainsKey(target)) Add(TargetUnknown,$"{id}: {target}",body,"Target");
            var entries = new List<ReviewEntryIr>();
            var entryOrder = 0;
            foreach (var entry in (declaration.Body?.Statements.OfType<DeclarationSyntax>()??[])
                .Where(d=>d.Type.Name.Text is "Comment" or "Issue" or "Proposal" or "Resolution"))
            {
                var entryBody=body.Children(entry.Type.Name.Text).Single(s=>ReferenceEquals(s.Declaration,entry));
                var kind=Enum.Parse<ReviewEntryKind>(entry.Type.Name.Text);
                var entryId=entry.Name?.Text??$"{id}.{kind}.{entryOrder+1}";
                if (!identities.Add(entryId)) Add(IdentityDuplicate,entryId,entryBody);
                var authorName = StringField(entryBody, "Author");
                if (string.IsNullOrWhiteSpace(authorName)) Add(AuthorRequired,entryId,entryBody,"Author");
                var dateText = Field(entryBody, "Date");
                if (dateText is null) Add(DateRequired,entryId,entryBody,"Date");
                else if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) Add(DateInvalid,$"{entryId}: {dateText}",entryBody,"Date");
                _ = DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
                var authoredText=StringField(entryBody,"Text");var reason=StringField(entryBody,"Reason");var text=authoredText??reason??string.Empty;
                StructuredProposalIr? proposal = null;
                if (kind == ReviewEntryKind.Proposal)
                {
                    var property = Field(entryBody, "Property");
                    var proposedValue=Field(entryBody,"Proposed");var tolerance=Field(entryBody,"Tolerance");var proposed=proposedValue??tolerance;
                    if(proposedValue is not null && tolerance is not null)entryBody.Invalid("Proposed","Use Proposed or its legacy Tolerance alias, not both.");
                    var current = Field(entryBody, "Current") ?? targetInfo?.CurrentValue;
                    var units = Field(entryBody, "Units") ?? UnitOf(proposed);
                    var currentUnit = UnitOf(current); var proposedUnit = UnitOf(proposed);
                    if (currentUnit is not null && proposedUnit is not null && !string.Equals(currentUnit, proposedUnit, StringComparison.OrdinalIgnoreCase))
                        Add(UnitMismatch,$"{entryId}: {currentUnit} vs {proposedUnit}",entryBody,"Proposed");
                    if (proposed is not null) proposal = new(property ?? "value", current, proposed, units, StringField(entryBody, "Reason"));
                }
                entries.Add(new(entryId, kind, new(authorName ?? "<missing>", StringField(entryBody, "Organization"), StringField(entryBody, "Email")), date, text, proposal, entryOrder++));
            }
            var closed=body.CompleteTree();diagnostics.AddRange(closed.Select(d=>d.Code+": "+d.Message));
            located.AddRange(closed.Where(d=>d.SourceSpan is not null).Select(d=>new ReviewSourceDiagnostic(d.Code,d.Message,new(sourcePath,d.SourceSpan!.Start,d.SourceSpan.Length))));
            threads.Add(new(id, new(target, sourcePath, targetInfo?.CurrentValue, targetInfo?.Capabilities ?? [], null), status, entries, $"{sourcePath}:Review {id}"));
        }
        return new(diagnostics.Count == 0, new(threads), diagnostics, ranges){SourceDiagnostics=located};
    }

    public static string EraseDeclarations(string source, IEnumerable<(int Start, int Length)> ranges)
    {
        var chars = source.ToCharArray();
        foreach (var (start, length) in ranges)
            for (var index = start; index < start + length; index++) if (chars[index] != '\n' && chars[index] != '\r') chars[index] = ' ';
        return new(chars);
    }

    private static string? Field(FirmamentSyntaxScope body,string name)=>body.Raw(name);
    private static string? StringField(FirmamentSyntaxScope body,string name)=>body.Word(name);
    private static string? UnitOf(string? value) => value is null ? null : Regex.Match(value, @"(?i)(mm|cm|m|in|deg)\b").Groups[1].Value is { Length: > 0 } unit ? unit.ToLowerInvariant() : null;

}
