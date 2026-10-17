using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

public sealed record SheetMetalManufacturingDatum(string Label, string Target);

public sealed record SheetMetalManufacturingDimension(
    string Name,
    string Kind,
    string Target,
    double Value,
    double TolerancePlus,
    double ToleranceMinus,
    int? Quantity)
{
    public IReadOnlyList<FirmamentV2ValueOrigin> DimensionOrigins { get; init; } = [];
}

public sealed record SheetMetalManufacturingGeometricTolerance(
    string Name,
    string Kind,
    string Target,
    double Value,
    IReadOnlyList<string> DatumReferences,
    int? Quantity);

public sealed record SheetMetalManufacturingAnnotation(string Name, string Target, string Text);

public sealed record SheetMetalManufacturingDefinition(
    string ReleaseName,
    string? MaterialSpecification,
    double? ThicknessTolerancePlus,
    double? ThicknessToleranceMinus,
    string? BlankProcess,
    string? FormingProcess,
    double? GeneralCutTolerance,
    double? GeneralFormedTolerance,
    double? GeneralAngularToleranceDegrees,
    IReadOnlyList<SheetMetalManufacturingDatum> Datums,
    IReadOnlyList<SheetMetalManufacturingDimension> Dimensions,
    IReadOnlyList<SheetMetalManufacturingGeometricTolerance> GeometricTolerances,
    IReadOnlyList<SheetMetalManufacturingAnnotation> Annotations)
{
    public static readonly SheetMetalManufacturingDefinition Empty = new(
        "unspecified", null, null, null, null, null, null, null, null, [], [], [], []);

    public IReadOnlyList<Step242SemanticPmi> ToStep242SemanticPmi(SheetMetalPartIr? part = null)
    {
        var result = new List<Step242SemanticPmi>();
        result.AddRange(Datums.Select(datum =>
            new Step242SemanticPmiDatum(datum.Target, "plane", datum.Label, datum.Target)));

        foreach (var dimension in Dimensions)
        {
            if (dimension.Kind.Equals("Diameter", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new Step242SemanticPmiHole(
                    dimension.Target,
                    dimension.Value,
                    null,
                    "sheet_metal_pattern",
                    dimension.TolerancePlus,
                    dimension.ToleranceMinus,
                    dimension.Quantity));
            }
            else
            {
                result.Add(new Step242SemanticPmiDimension(
                    dimension.Target,
                    dimension.Name,
                    dimension.Kind,
                    dimension.Value,
                    dimension.TolerancePlus,
                    dimension.ToleranceMinus,
                    dimension.Quantity));
            }
        }

        result.AddRange(GeometricTolerances.Select(tolerance =>
            new Step242SemanticPmiGeometricTolerance(
                tolerance.Target,
                tolerance.Name,
                tolerance.Kind,
                tolerance.Value,
                tolerance.DatumReferences,
                tolerance.Quantity)));
        result.AddRange(Annotations.Select(annotation =>
            new Step242SemanticPmiNote(annotation.Name, annotation.Target, annotation.Text)));
        if (part?.FormedBody is null) return result;
        return result.Select(item => item with
        {
            GeometricFaceIds = ResolveTargetFaces(part, item switch
            {
                Step242SemanticPmiDatum datum => datum.Target,
                Step242SemanticPmiNote note => note.Target,
                _ => item.FeatureId
            })
        }).ToArray();
    }

    private static IReadOnlyList<int> ResolveTargetFaces(SheetMetalPartIr part, string target)
    {
        var body = part.FormedBody!;
        var faceIds = new HashSet<int>();
        foreach (var feature in part.Features.Where(feature =>
                     feature.StableId.Equals(target, StringComparison.OrdinalIgnoreCase)
                     || feature.StableId.StartsWith($"{target}[", StringComparison.OrdinalIgnoreCase)))
            AddFeatureFaces(feature);

        if (part.Regions.FirstOrDefault(region => region.StableId.Equals(target, StringComparison.OrdinalIgnoreCase)) is { } region)
            AddRegionFaces(region);
        if (part.Bends.FirstOrDefault(bend => bend.StableId.Equals(target, StringComparison.OrdinalIgnoreCase)) is { } bend
            && part.Regions.FirstOrDefault(region => region.StableId.Equals($"{bend.StableId}Region", StringComparison.OrdinalIgnoreCase)) is { } bendRegion)
            AddRegionFaces(bendRegion);
        return faceIds.OrderBy(id => id).ToArray();

        void AddFeatureFaces(SheetFeatureIr feature)
        {
            var owner = part.Regions.Single(region => region.StableId.Equals(feature.OwningRegionId, StringComparison.OrdinalIgnoreCase));
            if (owner.Plane is null) return;
            var normal = Unit(owner.Plane.Normal);
            var expected = new List<(Point3D Center, double Radius)>();
            if (feature.Kind == SheetFeatureKind.CircularHole && feature.Diameter is { } diameter)
                expected.Add((feature.Center, diameter / 2d));
            else if (feature.ExactContour is { } contour)
                foreach (var arc in contour.OuterLoop.Segments.Select(segment => segment.Geometry).OfType<LineArcCircularArc2D>())
                    expected.Add((owner.Plane.Origin + owner.Plane.UAxis * arc.Center.X + owner.Plane.VAxis * arc.Center.Y, arc.Radius));

            foreach (var face in body.Topology.Faces)
            {
                if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface?.Cylinder is not { } cylinder) continue;
                var axis = Unit(cylinder.Axis.ToVector());
                if (Math.Abs(Math.Abs(axis.Dot(normal)) - 1d) > 1e-7) continue;
                if (expected.Any(item => Math.Abs(item.Radius - cylinder.Radius) <= 1e-7 && DistanceToAxis(item.Center, cylinder.Origin, axis) <= 1e-6))
                    faceIds.Add(face.Id.Value);
            }
        }

        void AddRegionFaces(SheetRegionIr region)
        {
            foreach (var face in body.Topology.Faces)
            {
                if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null) continue;
                if (region.Plane is { } plane && surface.Plane is { } surfacePlane)
                {
                    var normal = Unit(plane.Normal);
                    if (Math.Abs(Math.Abs(surfacePlane.Normal.ToVector().Dot(normal)) - 1d) <= 1e-7
                        && Math.Abs((surfacePlane.Origin - (plane.Origin + normal * (part.Thickness / 2d))).Dot(normal)) <= 1e-6)
                        faceIds.Add(face.Id.Value);
                }
                else if (region.Cylinder is { } reference && surface.Cylinder is { } cylinder)
                {
                    var axis = Unit(reference.AxisDirection);
                    if (Math.Abs(Math.Abs(cylinder.Axis.ToVector().Dot(axis)) - 1d) <= 1e-7
                        && DistanceToAxis(reference.AxisOrigin, cylinder.Origin, axis) <= 1e-6
                        && (Math.Abs(cylinder.Radius - reference.InsideRadius) <= 1e-6 || Math.Abs(cylinder.Radius - (reference.InsideRadius + part.Thickness)) <= 1e-6))
                        faceIds.Add(face.Id.Value);
                }
            }
        }
    }

    private static double DistanceToAxis(Point3D point, Point3D origin, Vector3D unitAxis) =>
        ((point - origin) - unitAxis * (point - origin).Dot(unitAxis)).Length;

    private static Vector3D Unit(Vector3D vector) => vector / vector.Length;
}

internal sealed record SheetMetalManufacturingParseResult(
    SheetMetalManufacturingDefinition Definition,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.All(diagnostic => diagnostic.Severity != SheetMetalDiagnosticSeverity.Error);
}

internal static class SheetMetalManufacturingParser
{
    public static SheetMetalManufacturingParseResult Parse(FirmamentDomainBinding binding, string partName,
        AuthoredSheetBaseSpec baseSpec, IReadOnlyList<AuthoredSheetFlangeSpec> flanges,
        SheetMetalSemanticLayout layout, IReadOnlyList<AuthoredSheetCutSpec> cuts, string? selectedRelease)
    {
        var errors = new List<string>();
        var diagnostics = new List<SheetMetalDiagnostic>();
        var root = binding.Document.Statements.OfType<DeclarationSyntax>().Single(d=>d.Type.Name.Text=="SheetMetal" && d.Modifiers.Count==0);
        var declarations = binding.Document.Statements.Concat(root.Body!.Statements).OfType<DeclarationSyntax>().ToArray();
        var releases=declarations.Where(d=>d.Type.Name.Text=="Manufacturing").ToArray();
        var pmis=declarations.Where(d=>d.Type.Name.Text=="Pmi").ToArray();
        var selected=releases.Where(d=>selectedRelease is null || d.Name?.Text==selectedRelease).ToArray();
        if (selected.Length>1 || selectedRelease is not null && selected.Length!=1)
            Error(root,"Select one declared Manufacturing release with manufacturing: <name>; release ownership is ambiguous or unresolved.");
        if (pmis.Length>1) Error(pmis[1],"Declare one owning Pmi block; multiple blocks cannot be silently selected by source order.");
        var release=selected.Length==1?selected[0]:null;
        var releaseValues=new Dictionary<DeclarationSyntax,SheetMetalManufacturingDefinition>();
        foreach(var item in releases)
        {
            var f=new FirmamentSyntaxFields(item,errors,bind:binding.Nominal);
            var material=f.Reference("MaterialSpecification");
            var blank=f.Reference("BlankProcess");var forming=f.Reference("FormingProcess");
            var thickness=PlusMinus(f.Take("ThicknessTolerance"),item);
            var cut=OptionalMeasure(f,"GeneralCutTolerance","mm");
            var formed=OptionalMeasure(f,"GeneralFormedTolerance","mm");
            var angular=OptionalMeasure(f,"GeneralAngularTolerance","deg");
            f.Complete();
            releaseValues[item]=new(item.Name?.Text ?? "unspecified",material,thickness?.Plus,thickness?.Minus,blank,forming,cut,formed,angular,[],[],[],[]);
        }
        var definition=release is null?SheetMetalManufacturingDefinition.Empty:releaseValues[release];
        var targets = new HashSet<string>(StringComparer.Ordinal) { partName,baseSpec.Name };
        foreach(var flange in flanges) { targets.Add(flange.Name);targets.Add(flange.Name+"Bend"); }
        foreach(var pattern in layout.Patterns) { targets.Add(pattern.Path);foreach(var member in pattern.Members)targets.Add(member); }
        foreach(var cut in cuts)targets.Add(cut.Name);
        var datums=new List<SheetMetalManufacturingDatum>();var dimensions=new List<SheetMetalManufacturingDimension>();
        var tolerances=new List<SheetMetalManufacturingGeometricTolerance>();var annotations=new List<SheetMetalManufacturingAnnotation>();
        foreach(var pmi in pmis.Take(1))
        {
            if(pmi.Body is null) { Error(pmi,"Pmi needs a body.");continue; }
            var header = new FirmamentSyntaxFields(pmi with { Body=pmi.Body with { Statements=[] } },errors);header.Complete();
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var statement in pmi.Body.Statements)
            {
                if(statement is not DeclarationSyntax record || record.Name is null || record.Body is null)
                { Error(statement,"Expected a named DatumFeature, Dimension, Position or Note record in Pmi.");continue; }
                if(!names.Add(record.Name.Text))Error(record,$"PMI name '{record.Name.Text}' is declared more than once.");
                var f=new FirmamentSyntaxFields(record,errors,bind:binding.Nominal);
                var target=SheetMetalSyntaxDiagnostics.Path(f.Take("Target"));
                if(target is null || !targets.Contains(target)) Error(record,$"PMI '{record.Name.Text}' targets unknown Sheet Metal identity '{target ?? "<missing>"}'.");
                switch(record.Type.Name.Text)
                {
                    case "DatumFeature":
                        if(target is not null)datums.Add(new(record.Name.Text,target));break;
                    case "Dimension":
                    {
                        var kind=f.Reference("Kind");
                        if(kind is not ("Thickness" or "Linear" or "Diameter"))Error(record,$"Unsupported Dimension kind '{kind}'. Use Thickness, Linear or Diameter.");
                        var rawDimension=f.TakeAll("Dimension");
                        var dimension=rawDimension.Count==1?rawDimension[0].Value:null;
                        if(rawDimension.Count>1)Error(record,"Duplicate dimension projection.");
                        double? nominal=null; (double Plus,double Minus)? tolerance=null;
                        IReadOnlyList<FirmamentV2ValueOrigin> origins=[];
                        if(dimension is not null)
                        {
                            if(record.Body.Statements.OfType<FieldSyntax>().Any(v=>FirmamentSyntaxFields.SameField(v.Name.Text,"Value") || FirmamentSyntaxFields.SameField(v.Name.Text,"Tolerance")))
                                Error(record,"dimension: cannot be combined with literal value: or tolerance:.");
                            if(!binding.TryValue(dimension,out var bound) || bound.Type!=FirmamentV2PrimitiveType.Length || bound.Tolerance is null)
                                Error(record,"dimension: requires a bound toleranced Length from Static or Record data.");
                            else { nominal=bound.Value.NumericValue;tolerance=(bound.Tolerance.Plus,bound.Tolerance.Minus);origins=bound.Origins; }
                        }
                        else { nominal=OptionalMeasure(f,"Value","mm");tolerance=PlusMinus(f.Take("Tolerance"),record); }
                        var quantity=Quantity(f,record);
                        if(nominal is null || nominal<=0 || tolerance is null)Error(record,$"Dimension '{record.Name.Text}' requires a positive nominal and nonnegative PlusMinus allowances, or dimension: <typed requirement>.");
                        else if(target is not null) dimensions.Add(new(record.Name.Text,kind ?? "",target,nominal.Value,tolerance.Value.Plus,tolerance.Value.Minus,quantity) { DimensionOrigins=origins });
                        break;
                    }
                    case "Position":
                    {
                        var tolerance=OptionalMeasure(f,"Tolerance","mm");var refs=List(f.Take("DatumRefs"),record);
                        var quantity=Quantity(f,record);
                        if(tolerance is null || tolerance<=0 || refs.Count==0)Error(record,$"Position '{record.Name.Text}' requires positive tolerance: and datumRefs:.");
                        else if(target is not null)tolerances.Add(new(record.Name.Text,"Position",target,tolerance.Value,refs,quantity));
                        break;
                    }
                    case "Annotation": case "Note":
                        var text=f.Reference("Text");
                        if(string.IsNullOrWhiteSpace(text))Error(record,"Note requires nonempty text:.");
                        else if(target is not null)annotations.Add(new(record.Name.Text,target,text));break;
                    default:Error(record,$"Unsupported Sheet Metal PMI record '{record.Type.Name.Text}'. Supported: DatumFeature, Dimension, Position, Note (legacy Annotation).");break;
                }
                f.Complete();
            }
        }
        foreach(var position in tolerances)
            foreach(var reference in position.DatumReferences.Where(r=>!datums.Any(d=>d.Label==r)))
                Error(root,$"Position '{position.Name}' references unknown datum '{reference}'.");
        diagnostics.AddRange(SheetMetalSyntaxDiagnostics.Codes(errors,binding));
        return new(definition with { Datums=datums,Dimensions=dimensions,GeometricTolerances=tolerances,Annotations=annotations },diagnostics);

        void Error(StatementSyntax owner,string message)=>diagnostics.Add(SheetMetalSyntaxDiagnostics.At(binding,"sheetmetal-manufacturing-pmi-invalid",message,owner.Span,(owner as DeclarationSyntax)?.Name?.Text));
        double? OptionalMeasure(FirmamentSyntaxFields f,string name,string unit)
        {
            var expression=f.Take(name);if(expression is null)return null;
            if(FirmamentSyntaxValues.TryMeasure(expression,unit,out var value) && value>=0)return value;
            Error(f.Declaration,$"{name} requires a finite nonnegative {unit} value.");return null;
        }
        (double Plus,double Minus)? PlusMinus(ExpressionSyntax? expression,StatementSyntax owner)
        {
            if(expression is null)return null;
            if(expression is CallSyntax { Target:ReferenceSyntax { Name.Text:"PlusMinus" },Arguments.Count:2 } call
                && call.Arguments.All(a=>a.Name is null && a.Default is null)
                && FirmamentSyntaxValues.TryMeasure(call.Arguments[0].Value,"mm",out var plus)
                && FirmamentSyntaxValues.TryMeasure(call.Arguments[1].Value,"mm",out var minus) && plus>=0 && minus>=0)return (plus,minus);
            Error(owner,"PlusMinus requires two finite nonnegative Length allowances in millimetres.");return null;
        }
        int? Quantity(FirmamentSyntaxFields f,StatementSyntax owner)
        {
            var expression=f.Take("Quantity");if(expression is null)return null;
            if(FirmamentSyntaxValues.TryMeasure(expression,"",out var number) && number>0 && number<=int.MaxValue && number==Math.Truncate(number))return (int)number;
            Error(owner,"quantity: requires a positive integer.");return null;
        }
        IReadOnlyList<string> List(ExpressionSyntax? value,StatementSyntax owner)
        {
            if(value is SequenceSyntax { Kind:SequenceKind.List } list && list.Items.All(i=>i.Name is null && i.Default is null && FirmamentSyntaxValues.Reference(i.Value) is not null))return list.Items.Select(i=>FirmamentSyntaxValues.Reference(i.Value)!).ToArray();
            Error(owner,"datumRefs: requires a list of declared datum labels.");return [];
        }
    }
}
