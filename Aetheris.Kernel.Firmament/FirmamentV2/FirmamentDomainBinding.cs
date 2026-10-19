using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Domain adapters consume the production expansion and value binder without
/// losing tolerances to nominal substitution or implementing another evaluator.</summary>
public sealed class FirmamentDomainBinding
{
    private readonly FirmamentCompileTimeValues values;
    private readonly FirmamentConcepts concepts;
    private readonly FirmamentSyntaxOrigins origins;
    private readonly FirmamentSyntaxOrigins inputOrigins;
    private readonly bool inputExpanded;
    private readonly DocumentSyntax inputDocument;
    public bool IsExpanded { get; }
    public string Source { get; }
    public DocumentSyntax Document { get; }
    public IReadOnlyList<ConceptIrTemplateInstantiation> Instantiations { get; }
    public IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics => errors.Concat(concepts.Diagnostics.Select(d=>d with
        { SourceFile=Document.SourceFile,SourceSpan=d.SourceSpan is {} span?OriginalSpan(span):null,
          LocationNote=IsExpanded?"Location identifies the original enclosing declaration; the check evaluated expanded values.":d.LocationNote })).DistinctBy(d=>(d.Code,d.SourceSpan,d.Message)).ToArray();
    private readonly List<FirmamentV2ValidationDiagnostic> errors = [];

    public FirmamentDomainBinding(string source, string sourceFile, string ownerType, IReadOnlySet<string> domainResults, string? originalSource=null)
    {
        originalSource ??= source;
        var frontend=FirmamentFrontendSchemas.Select(source);
        errors.AddRange(frontend.LocatedDiagnostics.Select(d=>d with {SourceFile=sourceFile}));
        source=frontend.Source;
        values = new(source);
        var codes = new List<string>();
        var expansion = FirmamentV2TemplateExpansion.Expand(source, codes, values);
        var prepared=expansion?.Source ?? source;
        var features=FirmamentV2FeatureExpansion.Expand(prepared,codes,values,source,domainResults);
        if(features is not null)prepared=features.Source;
        Source=CanonicalStaticAuthoring.ExpandDomainPatterns(prepared,ownerType,values,codes,domainResults);
        Instantiations = expansion?.Instantiations ?? [];
        var syntax = FirmamentSyntaxParser.Parse(Source, sourceFile);
        Document = syntax.Document;
        originalSource ??= source;
        IsExpanded = Source != originalSource;
        inputExpanded=source!=originalSource;
        var original=FirmamentSyntaxParser.Parse(originalSource,sourceFile).Document;
        origins = new(original, Document);
        inputDocument=FirmamentSyntaxParser.Parse(source,sourceFile).Document;
        inputOrigins=new(original,inputDocument);
        errors.AddRange(syntax.Diagnostics.Select(d=>d with { SourceFile=sourceFile,SourceSpan=d.SourceSpan is {} span?OriginalSpan(span):null }));
        errors.AddRange(values.Diagnostics.Select(d=>d with { SourceFile=sourceFile,
            SourceSpan=inputExpanded && d.SourceSpan is {} span?Map(inputOrigins,inputDocument,span):d.SourceSpan,
            LocationNote=inputExpanded?"Input includes expanded library declarations; the location is an original enclosing declaration where available.":d.LocationNote }));
        if(FirmamentSyntaxTraversal.Declarations(Document).Any(OwnsConstruction))
        {
            var construction=ClosedBoundary2Authoring.Expand(Source,codes,values);
            if(construction is not null)codes.AddRange(ProfileAuthoringParser.ValidateConstructionGuides(construction.Source,values.NominalExpression));
        }
        foreach (var code in codes.Where(c => !errors.Any(e => c.StartsWith(e.Code, StringComparison.Ordinal))))
        {
            var presented=FirmamentBuildAdmissionValidation.Locate(new(code,"error",FirmamentInterop.FirmamentV2DiagnosticAdapter.MessageFor(code)),Source,sourceFile);
            errors.Add(presented with { Code=code.Split(':')[0],SourceSpan=presented.SourceSpan is {} span?OriginalSpan(span):null,Stage="Expansion" });
        }
        concepts = new(Source, values);
    }

    public ExpressionSyntax Nominal(ExpressionSyntax expression) => values.NominalExpression(expression);
    public Materializer.ResolvedProfile2D? ResolveProfile(string name,out IReadOnlyList<string> diagnostics) =>
        ProfileAuthoringParser.ResolveNamedProfileCore(Source,name,new(StringComparer.Ordinal),out diagnostics,values);
    public bool OwnsConstruction(DeclarationSyntax declaration) => ClosedBoundary2Authoring.IsBoundaryType(declaration.Type.Name.Text)
        || ProfileAuthoringParser.OwnsConstructionGuide(declaration.Type.Name.Text);
    public bool OwnsProfileData(DeclarationSyntax declaration) => ProfileAuthoringParser.OwnsDeclaration(declaration);
    internal FirmamentV2TemplateExpansion.DataCatalog DataCatalog(List<string> diagnostics) => FirmamentV2TemplateExpansion.ReadDataCatalog(Source, diagnostics, values);
    internal string DataName(string identity) => values.DataBindings.FirstOrDefault(d => d.Identity == identity)?.Name ?? identity;
    public bool OwnsData(DeclarationSyntax declaration) => FirmamentDataBinding.NamespaceFor(declaration) is not null;
    public bool OwnsCommonDeclaration(DeclarationSyntax declaration) => OwnsData(declaration)
        || declaration.Type.Name.Text is "Concept" or "Require";

    /// <summary>Common requirements use the existing Concept evaluator and shared
    /// compile-time values. Domain geometry is not fabricated as scalar data.</summary>
    public IReadOnlyList<FirmamentV2ValidationDiagnostic> BindCommonRequirements(IEnumerable<StatementSyntax> statements)
    {
        var previous = Diagnostics.ToHashSet();
        foreach (var requirement in statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Require"))
            CheckRequirement(requirement);
        return Diagnostics.Where(d => !previous.Contains(d)).ToArray();
    }
    public FirmamentV2SourceSpan? OriginalSpan(FirmamentV2SourceSpan span)
    {
        if(!IsExpanded)return span;
        return Map(origins,Document,span);
    }
    private static FirmamentV2SourceSpan? Map(FirmamentSyntaxOrigins mapping,DocumentSyntax document,FirmamentV2SourceSpan span)
    {
        if(mapping.Find(span) is {} exact)return exact;
        foreach(var owner in FirmamentSyntaxTraversal.Declarations(document).Where(d=>d.Span.Start<=span.Start && d.Span.Start+d.Span.Length>=span.Start+span.Length).OrderBy(d=>d.Span.Length))
            if(mapping.Find(owner.Span) is {} enclosing)return enclosing;
        return null;
    }
    public bool TryValue(ExpressionSyntax expression, out FirmamentV2BoundLet value)
    {
        if(!values.TryValue(FirmamentSyntaxValues.Reference(expression) ?? "",out value!))return false;
        value=value with { Origins=value.Origins.Select(origin=>
        {
            var span=inputExpanded?Map(inputOrigins,inputDocument,origin.SourceSpan):origin.SourceSpan;
            return span is null
                ? origin with { SourceFile=Document.SourceFile+"#expanded",LocationNote="Original imported value location is unavailable; span belongs to expanded input." }
                : origin with { SourceFile=Document.SourceFile,SourceSpan=span,
                    LocationNote=inputExpanded?"Location identifies the original enclosing compile-time value declaration.":origin.LocationNote };
        }).ToArray() };
        return true;
    }
    public bool CheckRequirement(DeclarationSyntax requirement)
    {
        if(requirement.Name is null || requirement.Initializer is null || requirement.Parameters.Count>0 || requirement.Annotation is not null
            || requirement.Modifiers.Count>0 || requirement.Type.Arguments.Count>0 || requirement.Clauses.Count>0 || requirement.ResultType is not null || requirement.Occurrence)
        {
            errors.Add(new("firmament-concept-requirement-invalid","error","Use Require name => BooleanPredicate without parameters, placement or type annotations.")
                { SourceFile=Document.SourceFile,SourceSpan=OriginalSpan(requirement.Span),Stage="Concept binding" });
            return false;
        }
        return concepts.Check(requirement.Initializer,requirement.Name.Text,requirement.Span);
    }
    public bool CheckContract(string contract, IReadOnlyDictionary<string, string> members, string owner, FirmamentV2SourceSpan span) =>
        concepts.CheckMembers(contract, members, owner, span);
}
