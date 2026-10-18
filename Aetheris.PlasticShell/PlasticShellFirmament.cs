using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Block=Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxScope;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.PlasticShell;

/// <summary>Module-owned manufacturing-first Firmament surface. It lowers to PlasticShellIr before any BRep operation.</summary>
public static class PlasticShellFirmament
{
    public static bool LooksLikePlasticShell(string? source) => source is not null && source.Contains("PlasticShell",StringComparison.Ordinal)
        && FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).Any(d=>d.Type.Name.Text=="PlasticShell");
    public static PlasticShellCompileResult CompileFile(string path) => Compile(File.ReadAllText(path), path);

    public static PlasticShellCompileResult Compile(string source, string sourcePath = "authored.firmament")
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<PlasticDiagnostic>();
        var binding=new FirmamentDomainBinding(source,sourcePath,"PlasticShell",new HashSet<string>(StringComparer.Ordinal));
        diagnostics.AddRange(binding.Diagnostics.Select(Located));
        var models=binding.Document.Statements.OfType<DeclarationSyntax>().Where(d=>d.Type.Name.Text=="Model").ToArray();
        if(models.Length!=1)return Fail("<unknown>","PlasticShell requires exactly one Model.");
        var model=new Block(models[0],binding.Source,sourcePath,binding.Nominal,binding.OriginalSpan);var modelName=model.Name;
        foreach(var statement in binding.Document.Statements.Where(s=>!ReferenceEquals(s,models[0])))
            if(statement is not DeclarationSyntax data || !binding.OwnsData(data))diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid,PlasticDiagnosticSeverity.Error,"Unsupported statement outside PlasticShell Model."));
        foreach(var data in models[0].Body?.Statements.OfType<DeclarationSyntax>().Where(binding.OwnsData)??[])model.Consume(data);
        if(model.Word("Units")!="mm")diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid,PlasticDiagnosticSeverity.Error,"PlasticShell requires units: mm."));
        var blocks=model.Children("PlasticShell").ToArray();
        if(blocks.Length!=1)diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid,PlasticDiagnosticSeverity.Error,"Exactly one PlasticShell is required."));
        if(diagnostics.Count>0)return new(false,modelName,null,null,diagnostics);
        var shell = blocks[0];
        try
        {
            var exteriorKind = Scalar(shell.Body, "Exterior") ?? "Frustum";
            if (!exteriorKind.Equals("Frustum", StringComparison.OrdinalIgnoreCase)) diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "X0 realizes only the exact analytic Frustum exterior family; the IR remains extensible to accepted SURF authority."));
            var bottomRadius = Length(shell.Body, "BottomRadius", diagnostics); var topRadius = Length(shell.Body, "TopRadius", diagnostics); var height = Length(shell.Body, "Height", diagnostics);
            var material = Scalar(shell.Body, "Material") ?? "UnspecifiedPlastic";
            var tooling = Direction(shell.Body, "ToolingDirection", diagnostics);
            var minimumDraft = Angle(shell.Body, "MinimumDraftAngle", diagnostics);
            var wallBlock = Single(shell,"WallPolicy");
            if (wallBlock is null) diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "PlasticShell requires WallPolicy."));
            var nominal = wallBlock is null ? null : Length(wallBlock.Body, "NominalThickness", diagnostics);
            var minimum = wallBlock is null ? null : Length(wallBlock.Body, "MinimumThickness", diagnostics);
            var maximum = wallBlock is null ? null : Length(wallBlock.Body, "MaximumThickness", diagnostics);
            var tolerance = wallBlock is null ? null : Length(wallBlock.Body, "ThicknessTolerance", diagnostics, allowZero: true);
            var partingBlock = Single(shell,"PartingPlane");
            if (partingBlock is null) diagnostics.Add(new(PlasticDiagnosticCodes.InvalidParting, PlasticDiagnosticSeverity.Error, "PlasticShell X0 requires an explicit PartingPlane."));
            var partingOrigin = partingBlock is null ? null : Point(partingBlock.Body, "Origin", diagnostics);
            var partingNormal = partingBlock is null ? null : Direction(partingBlock.Body, "Normal", diagnostics);
            var preserved = List(shell.Body, "Preserve");
            var gates = Blocks(shell.Body, "Gate").Select(b => ParseGate(b, diagnostics)).Where(x => x is not null).Select(x => x!).ToArray();
            var standoffs = Blocks(shell.Body, "Standoff").Select(b => ParseStandoff(b, diagnostics)).Where(x => x is not null).Select(x => x!).ToArray();
            var ejectors = Blocks(shell.Body, "EjectorPin").Select(b => ParseEjector(b, diagnostics)).Where(x => x is not null).Select(x => x!).ToArray();
            var autoBlock = Single(shell,"AutoRib");
            var autoRib = autoBlock is null ? null : ParseAutoRib(autoBlock, diagnostics);
            if (gates.Length == 0) diagnostics.Add(new(PlasticDiagnosticCodes.InvalidGate, PlasticDiagnosticSeverity.Error, "PlasticShell X0 requires at least one explicit Gate."));
            if (ejectors.Length == 0) diagnostics.Add(new(PlasticDiagnosticCodes.EjectorNotCoreAccessible, PlasticDiagnosticSeverity.Error, "PlasticShell X0 policy requires at least one explicit EjectorPin."));
            diagnostics.AddRange(model.CompleteTree().Select(Located));
            if (diagnostics.Any(d => d.Severity == PlasticDiagnosticSeverity.Error) || bottomRadius is null || topRadius is null || height is null || tooling is null || minimumDraft is null || nominal is null || minimum is null || maximum is null || tolerance is null || partingOrigin is null || partingNormal is null)
                return new(false, modelName, null, null, diagnostics);
            var exteriorToken = preserved.FirstOrDefault(p => p is "ExteriorDesignSurface" or "ExteriorCrown");
            var exteriorId = $"{shell.Name}.{exteriorToken ?? "ExteriorDesignSurface"}";
            var ir = new PlasticShellIr(shell.Name,
                new(exteriorId, exteriorKind, bottomRadius.Value, topRadius.Value, height.Value, exteriorToken is not null),
                material, new(nominal.Value, minimum.Value, maximum.Value, tolerance.Value), tooling.Value,
                new(partingBlock?.Name ?? $"{shell.Name}.PartingPlane", partingOrigin.Value, partingNormal.Value), minimumDraft.Value,
                gates, standoffs, ejectors, autoRib, preserved.Select(p => p is "ExteriorDesignSurface" or "ExteriorCrown" ? $"{shell.Name}.{p}" : p).ToArray());
            var compiled = PlasticShellCompiler.Compile(ir, modelName);
            return compiled with { Diagnostics = diagnostics.Concat(compiled.Diagnostics).ToArray() };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            diagnostics.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, ex.Message));
            return new(false, modelName, null, null, diagnostics);
        }
    }

    private static PlasticGate? ParseGate(Block b, ICollection<PlasticDiagnostic> d)
    {
        var position = Point(b.Body, "Position", d); var target = Scalar(b.Body, "Target") ?? "TopAnnularRim"; var size = OptionalLength(b.Body, "Size", d);
        var kindText = Scalar(b.Body, "Kind") ?? "Point";
        if ((!Enum.TryParse<GateKind>(kindText, true, out var kind)||!Enum.IsDefined(kind))) { d.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, $"Unknown Gate Kind '{kindText}'.", b.Name)); return null; }
        return position is null ? null : new(b.Name, position.Value, target, kind, size);
    }

    private static PlasticStandoff? ParseStandoff(Block b, ICollection<PlasticDiagnostic> d)
    {
        var p = Point(b.Body, "Position", d); var h = Length(b.Body, "Height", d); var od = Length(b.Body, "OuterDiameter", d); var hole = OptionalLength(b.Body, "HoleDiameter", d);
        var intentText = Scalar(b.Body, "SupportIntent") ?? "Pcb";
        if ((!Enum.TryParse<StandoffSupportIntent>(intentText, true, out var intent)||!Enum.IsDefined(intent))) { d.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, $"Unknown Standoff SupportIntent '{intentText}'.", b.Name)); return null; }
        return p is null || h is null || od is null ? null : new(b.Name, p.Value, h.Value, od.Value, hole, intent);
    }

    private static PlasticEjectorPin? ParseEjector(Block b, ICollection<PlasticDiagnostic> d)
    {
        var p = Point(b.Body, "Position", d); var diameter = Length(b.Body, "Diameter", d); var target = Scalar(b.Body, "Target") ?? "InnerBottom";
        return p is null || diameter is null ? null : new(b.Name, p.Value, diameter.Value, target);
    }

    private static PlasticAutoRibRequest? ParseAutoRib(Block b, ICollection<PlasticDiagnostic> d)
    {
        var supports = List(b.Body, "Support"); var gate = Scalar(b.Body, "Gate") ?? string.Empty; var keepouts = List(b.Body, "KeepOut");
        var policyBlock = Single(b,"RibPolicy");
        if (policyBlock is null) { d.Add(new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "AutoRib requires RibPolicy.", b.Name)); return null; }
        var ratio = Number(policyBlock.Body, "ThicknessRatio", d); var min = Length(policyBlock.Body, "MinimumHeight", d); var max = Length(policyBlock.Body, "MaximumHeight", d); var spacing = Length(policyBlock.Body, "MinimumSpacing", d); var draft = Angle(policyBlock.Body, "DraftAngle", d); var blend = Length(policyBlock.Body, "BaseBlendRadius", d, allowZero: true);
        return ratio is null || min is null || max is null || spacing is null || draft is null || blend is null ? null : new(b.Name, supports, gate, keepouts, new(ratio.Value, min.Value, max.Value, spacing.Value, draft.Value, blend.Value));
    }

    private static PlasticDiagnostic Located(FirmamentV2ValidationDiagnostic d)=>new(d.Code,PlasticDiagnosticSeverity.Error,d.Message,d.Target){SourceFile=d.SourceFile,SourceSpan=d.SourceSpan is {} span?new(span.Start,span.Length){LocationNote=d.LocationNote}:null};
    public static IReadOnlyList<Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic> KernelDiagnostics(PlasticShellCompileResult result)=>result.Diagnostics.Select(d=>new Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic(
        Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,d.Severity switch {PlasticDiagnosticSeverity.Error=>Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,PlasticDiagnosticSeverity.Warning=>Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Warning,_=>Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Info},
        d.Code+": "+d.Message,"Firmament.PlasticShell"){OriginalSourceSpan=d.SourceSpan}).ToArray();
    private static Block? Single(Block owner,string kind){var children=owner.Children(kind);if(children.Count>1)owner.Invalid(kind,"Exactly one declaration is allowed.");return children.Count==1?children[0]:null;}
    private static IEnumerable<Block> Blocks(Block body,string kind)=>body.Children(kind);
    private static string? Scalar(Block body,string key)=>body.Word(key);
    private static double? Number(Block body,string key,ICollection<PlasticDiagnostic> d)=>Quantity(body,key,"",d,false);
    private static double? Length(Block body,string key,ICollection<PlasticDiagnostic> d,bool allowZero=false)=>Quantity(body,key,"mm",d,allowZero);
    private static double? Angle(Block body,string key,ICollection<PlasticDiagnostic> d)=>Quantity(body,key,"deg",d,true);
    private static double? OptionalLength(Block body,string key,ICollection<PlasticDiagnostic> d)=>body.Take(key) is null?null:Length(body,key,d);
    private static double? Quantity(Block body,string key,string unit,ICollection<PlasticDiagnostic> d,bool allowZero)
    {
        if(FirmamentSyntaxValues.TryMeasure(body.Take(key),unit,out var value) && double.IsFinite(value) && (allowZero?value>=0:value>0))return value;
        body.Invalid(key,$"Expected a finite {(allowZero?"nonnegative":"positive")} {unit} value.");return null;
    }
    private static Point3D? Point(Block body,string key,ICollection<PlasticDiagnostic> d){var v=Vector(body,key,"mm");return v is null?null:new(v[0],v[1],v[2]);}
    private static Direction3D? Direction(Block body,string key,ICollection<PlasticDiagnostic> d)
    {
        var v=Vector(body,key,"");if(v is null)return null;
        try{return Direction3D.Create(new Vector3D(v[0],v[1],v[2]));}catch(ArgumentException){body.Invalid(key,"Direction must be nonzero.");return null;}
    }
    private static double[]? Vector(Block body,string key,string unit)
    {
        if(FirmamentSyntaxValues.Vector(body.Take(key),unit,out var values,unit) && values.Length==3 && values.All(double.IsFinite))return values;
        body.Invalid(key,"Expected three finite components.");return null;
    }
    private static IReadOnlyList<string> List(Block body,string key)
    {
        var expression=body.Take(key);if(expression is null)return [];
        string? Identity(ExpressionSyntax value)=>FirmamentSyntaxValues.Reference(value) ?? (key=="KeepOut" && value is BinarySyntax {Operator:BinaryOperator.Subtract} legacy && Identity(legacy.Left) is {} left && Identity(legacy.Right) is {} right?left+"-"+right:null);
        if(expression is SequenceSyntax {Kind:SequenceKind.List} list && list.Items.All(i=>i.Name is null && i.Default is null && Identity(i.Value) is not null))return list.Items.Select(i=>Identity(i.Value)!).ToArray();
        body.Invalid(key);return [];
    }
    private static PlasticShellCompileResult Fail(string model,string message)=>new(false,model,null,null,[new(PlasticDiagnosticCodes.SourceInvalid,PlasticDiagnosticSeverity.Error,message)]);
}
