using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Scope=Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxScope;
namespace Aetheris.Kernel.Firmament.Drawing;

public static partial class FirmamentDrawingCompiler
{
    private static Dictionary<string,StaticRecord> ParseStaticRecords(string source,List<string> diagnostics)
    {
        var values=new FirmamentCompileTimeValues(source);
        var catalog=FirmamentV2TemplateExpansion.ReadDataCatalog(source,diagnostics,values,initialize:true);
        diagnostics.AddRange(values.Diagnostics.Select(d=>d.Code+": "+d.Message));
        return catalog.Records.ToDictionary(p=>p.Key,p=>new StaticRecord(
            FirmamentV2TemplateExpansion.FlattenRecordFields(p.Value,catalog.Types,catalog.Records,diagnostics),p.Value.Provenance),StringComparer.Ordinal);
    }

    private static ParseResult ParseDrawingLanguage(string source,string sourceFile,List<FirmamentV2ValidationDiagnostic> frontend)
    {
        var syntax=FirmamentSyntaxParser.Parse(source,sourceFile);
        frontend.AddRange(syntax.Diagnostics);
        var diagnostics=syntax.Diagnostics.Select(d=>d.Code+": "+d.Message).ToList();
        void Close(Scope scope){var located=scope.CompleteTree();frontend.AddRange(located);diagnostics.AddRange(located.Select(d=>d.Code+": "+d.Message));}
        var ranges=new List<(int Start,int Length)>();
        if(!syntax.IsSuccess)return new(null,null,ranges,diagnostics);
        // Drawing declarations live at document or product scope, never inside an unrelated feature.
        var declarations=syntax.Document.Statements.OfType<DeclarationSyntax>()
            .SelectMany(d=>d.Type.Name.Text=="Model"?d.Body?.Statements.OfType<DeclarationSyntax>()??[]:[d]).ToArray();
        var concepts=declarations.Where(d=>d.Type.Name.Text=="Drawing" && d.Modifiers.Any(m=>m.Name.Text=="Concept")).ToArray();
        var templates=declarations.Where(d=>d.Type.Name.Text=="Drawing" && d.Modifiers.Any(m=>m.Name.Text=="Template")).ToArray();
        var applications=declarations.Where(d=>d.Type.Name.Text=="Drawing" && d.Modifiers.Count==0).ToArray();
        foreach(var d in concepts.Concat(templates).Concat(applications))ranges.Add((d.Span.Start,d.Span.Length));
        if(applications.Length!=1){diagnostics.Add("drawing-declaration-cardinality: Exactly one Drawing product is required.");return new(null,null,ranges,diagnostics);}
        var app=applications[0];var contract=FirmamentSyntaxValues.Reference(app.Annotation)??"";
        var body=app;var templateName="Literal";var arguments=new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal);
        if(app.Initializer is SpecializationSyntax specialization)
        {
            templateName=FirmamentSyntaxValues.Reference(specialization.Target)??"";
            var candidates=templates.Where(t=>t.Name?.Text==templateName).ToArray();
            if(candidates.Length!=1){diagnostics.Add("drawing-template-unknown: "+templateName);return new(null,null,ranges,diagnostics);}
            body=candidates[0];contract=FirmamentSyntaxValues.Reference(body.Annotation)??"";
            if(body.Modifiers.Count!=1){diagnostics.Add("drawing-template-header-invalid");return new(null,null,ranges,diagnostics);}
            var parameters=body.Modifiers[0].Arguments;
            if(parameters.Any(p=>p.Name is null || p.Default is not null) || parameters.Select(p=>p.Name!.Text).Distinct().Count()!=parameters.Count)
                diagnostics.Add("drawing-template-parameter-invalid");
            foreach(var argument in specialization.Arguments)
            {
                var key=argument.Name?.Text??"";
                // Historical Product: argument names the unique Item: Product parameter.
                if(key=="Product" && !parameters.Any(p=>p.Name?.Text==key))
                {
                    var productParameters=parameters.Where(p=>FirmamentSyntaxValues.Reference(p.Value)=="Product").ToArray();
                    if(productParameters.Length==1)key=productParameters[0].Name?.Text??key;
                }
                if(!parameters.Any(p=>p.Name?.Text==key))diagnostics.Add("drawing-template-argument-unknown: "+key);
                else if(!arguments.TryAdd(key,argument.Value))diagnostics.Add("drawing-template-argument-duplicate: "+key);
                if(FirmamentSyntaxValues.Reference(argument.Value) is null || argument.Default is not null)diagnostics.Add("drawing-template-argument-invalid: "+key);
            }
            var catalog=FirmamentV2TemplateExpansion.ReadDataCatalog(source,diagnostics);
            foreach(var p in parameters)
            {
                if(p.Name is not {} name)continue;
                if(!arguments.TryGetValue(name.Text,out var value)){diagnostics.Add("drawing-template-argument-missing: "+name.Text);continue;}
                var type=FirmamentSyntaxValues.Reference(p.Value);var argument=FirmamentSyntaxValues.Reference(value);
                if(type=="Product")
                {
                    if(!syntax.Document.Statements.OfType<DeclarationSyntax>().Any(d=>d.Name?.Text==argument && d.Type.Name.Text is "Assembly" or "Model"))diagnostics.Add("drawing-template-product-unknown: "+argument);
                }
                else if(type is null || !catalog.Types.ContainsKey(type) || argument is null || !catalog.Records.TryGetValue(argument,out var record) || record.TypeName!=type)
                    diagnostics.Add("drawing-template-record-type-mismatch: "+name.Text+": expected "+type);
            }
            var application=new Scope(app,source,sourceFile).AdmitInitializer();
            Close(application);
        }
        else if(app.Initializer is not null)diagnostics.Add("drawing-initializer-invalid");
        // Unused declarations are still closed; unsupported library templates cannot disappear.
        if(templates.Any(t=>t!=body))diagnostics.Add("drawing-unused-template-unsupported: Keep one drawing prototype per compilation unit.");
        var substitution=new FirmamentSyntaxSubstitution(arguments);
        var scope=new Scope(body,source,sourceFile,substitution.Expression);
        if(body!=app)scope.AllowModifier("Template");
        if(body.Annotation is not null)scope.AdmitAnnotation();
        var parsed=ParseDrawingBody(app.Name?.Text??"<unnamed>",templateName,contract,scope,diagnostics);
        Close(scope);
        ParsedConcept? concept=null;
        if(contract.Length>0)
        {
            var matches=concepts.Where(c=>c.Name?.Text==contract).ToArray();
            if(matches.Length!=1)diagnostics.Add("drawing-concept-unknown: "+contract);
            else
            {
                var requirements=new List<string>();var owner=new Scope(matches[0],source,sourceFile).AllowModifier("Concept");
                foreach(var requirement in owner.Children("Require"))
                {
                    if(requirement.Name is not ("PrimaryView" or "ManufacturingPmi" or "Material" or "DesignTable" or "BOM" or "RevisionMetadata"))
                        requirement.Invalid("requirement","Unknown drawing policy requirement.");
                    requirements.Add(requirement.Name);
                }
                Close(owner);concept=new(contract,requirements);
            }
        }
        if(concepts.Any(c=>c.Name?.Text!=contract))diagnostics.Add("drawing-unused-concept-unsupported: Keep one drawing policy per compilation unit.");
        return new(parsed,concept,ranges,diagnostics);
    }

    private static ParsedDrawing ParseDrawingBody(string name,string template,string concept,Scope scope,List<string> diagnostics)
    {
        string? Field(string field)=>scope.Raw(field) is {} value?TrimValue(value):null;
        var source=Field("Source");if(string.IsNullOrWhiteSpace(source))diagnostics.Add("drawing-source-required");
        var orientation=Field("Orientation")??"Landscape";
        if(orientation is not ("Portrait" or "Landscape"))scope.Invalid("Orientation");
        var views=new List<ParsedView>();
        foreach(var view in scope.Children("View"))
        {
            var value=view.Take("Direction");double[] direction=[];
            var axis=FirmamentSyntaxValues.Reference(value);
            direction=axis switch{"+X"=>[1,0,0],"-X"=>[-1,0,0],"+Y"=>[0,1,0],"-Y"=>[0,-1,0],"+Z"=>[0,0,1],"-Z"=>[0,0,-1],_=>[]};
            if(direction.Length==0 && !FirmamentSyntaxValues.Vector(value,"",out direction))direction=[];
            if(direction.Length!=3 || !direction.All(double.IsFinite) || direction.All(v=>v==0)){view.Invalid("Direction");continue;}
            var projection=view.Word("Projection");
            if(projection is not (null or "Orthographic" or "Isometric"))view.Invalid("Projection");
            var hidden=view.Word("HiddenLines")??"VisibleOnly";
            if(hidden is not ("VisibleOnly" or "VisibleAndHidden" or "Wireframe"))view.Invalid("HiddenLines");
            var pmi=List(view,"PMI",false);
            var kind=projection=="Isometric" || projection is null && direction.Count(v=>Math.Abs(v)>1e-12)>1?DrawingProjectionKind.Isometric:DrawingProjectionKind.Orthographic;
            views.Add(new(view.Name,direction,kind,hidden=="VisibleAndHidden"?DrawingHiddenLinePolicy.VisibleAndHidden:DrawingHiddenLinePolicy.VisibleOnly,pmi));
        }
        if(views.Count==0)diagnostics.Add(DrawingViewMissing);
        if(views.Select(v=>v.Name).Distinct().Count()!=views.Count)diagnostics.Add("drawing-view-duplicate");
        var notes=List(scope,"Notes",true);var bom=Field("BOM");if(bom is not (null or "true" or "false"))scope.Invalid("BOM");
        return new(name,template,concept,source??"",orientation=="Portrait"?DrawingPageOrientation.Portrait:DrawingPageOrientation.Landscape,views,
            Field("Title"),Field("PartNumber"),Field("Revision"),Field("Material"),Field("Table"),notes,Field("Metadata"),bom=="true");
    }
    private static IReadOnlyList<string> List(Scope scope,string field,bool strings)
    {
        var expression=scope.Take(field);if(expression is null)return [];
        if(expression is SequenceSyntax {Kind:SequenceKind.List} list && list.Items.All(i=>i.Name is null && i.Default is null
            && (strings?i.Value is LiteralSyntax {Kind:LiteralKind.String}:FirmamentSyntaxValues.Reference(i.Value) is not null)))
            return list.Items.Select(i=>FirmamentSyntaxValues.Reference(i.Value)!).ToArray();
        scope.Invalid(field);return [];
    }
}
