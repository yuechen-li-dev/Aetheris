using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Semantics;

namespace Aetheris.SheetMetal;

public sealed record EnclosureProductSpec(
    double Width,
    double Depth,
    double Height,
    double BodyThickness,
    double LidThickness,
    double InsideRadius,
    double LidClearance,
    double LidOverlap,
    double MinimumClearance = 0.4,
    double MaximumClearance = 1.5,
    double FastenerDiameter = 3.2,
    double KFactor = .42,
    SheetReliefPolicy ReliefPolicy = SheetReliefPolicy.Rectangular,
    SheetMetalProductionVariation? ProductionVariation = null);

public sealed record SheetMetalProductionVariation(
    double LinearTolerance = .1,
    double ThicknessTolerance = .08,
    double BendAngleToleranceDegrees = .5,
    double BendLocationTolerance = .1,
    double CoatingThickness = 0,
    double CoatingThicknessTolerance = 0);

public sealed record FitToleranceContribution(string Source, string SemanticPath, double WorstCaseClearanceReductionMm);
public sealed record EnclosureFitEvidence(
    string InterfacePath,
    double NominalMinimumSeparationMm,
    double MinimumPossibleSeparationMm,
    double MaximumPossibleSeparationMm,
    double MaximumPenetrationMm,
    double OverlapVolumeLowerBoundMm3,
    double? ContactAreaMm2,
    string ContactEvidence,
    string OverlapEvidence,
    FitClassification NominalState,
    FitClassification VariationEnvelopeState,
    IReadOnlyList<FitToleranceContribution> DominantContributions);

public enum ProductDfmFindingKind { Part, Interface, Assembly }

public sealed record ProductDfmFinding(
    ProductDfmFindingKind Kind,
    string RuleId,
    SheetMetalDfmStatus Status,
    string SemanticPath,
    string Message,
    double? Measured = null,
    double? Required = null);

public sealed record EnclosureProductDfmReport(IReadOnlyList<ProductDfmFinding> Findings)
{
    public SheetMetalDfmStatus Overall => Findings.Any(item => item.Status == SheetMetalDfmStatus.Fail)
        ? SheetMetalDfmStatus.Fail
        : Findings.Any(item => item.Status == SheetMetalDfmStatus.Warning)
            ? SheetMetalDfmStatus.Warning
            : SheetMetalDfmStatus.Pass;
}

public sealed record EnclosureProductBomItem(string SemanticPath, string Definition, int Quantity);

public sealed record EnclosureProductArtifacts(
    string AssemblyStep,
    string BodyFormedStep,
    string BodyFlatStep,
    string BodyFlatSvg,
    string LidFormedStep,
    string LidFlatStep,
    string LidFlatSvg,
    string ProductDfmJson,
    string FitReportJson);

public sealed record ManufacturedEnclosureProduct(
    string TemplateName,
    string SpecializationIdentity,
    EnclosureProductSpec Spec,
    string FirmamentSource,
    AssemblyM1CompilationResult Assembly,
    ManufacturedSheetMetalResult Body,
    ManufacturedSheetMetalResult Lid,
    EnclosureProductDfmReport Dfm,
    EnclosureFitEvidence Fit,
    IReadOnlyList<string> SemanticPaths,
    IReadOnlyList<EnclosureProductBomItem> Bom)
{
    public EnclosureProductArtifacts Export(string directory)
    {
        var root = Path.GetFullPath(directory); Directory.CreateDirectory(root);
        var assembly = AssemblyIrAp242Exporter.Export(Assembly);
        if (!assembly.IsSuccess) throw new InvalidOperationException(string.Join("; ", assembly.Diagnostics.Select(item => item.Message)));
        var assemblyPath = Write("NetworkAppliance.step", assembly.Value);
        var bodyFormed = Formed("Body", Body); var lidFormed = Formed("Lid", Lid);
        var bodyFlat = Flat("Body", Body); var lidFlat = Flat("Lid", Lid);
        var bodySvg = Write("Body.flat.svg", Body.FlatSvg); var lidSvg = Write("Lid.flat.svg", Lid.FlatSvg);
        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var dfm = Write("product-dfm.json", System.Text.Json.JsonSerializer.Serialize(Dfm, options));
        var fit = Write("fit-report.json", System.Text.Json.JsonSerializer.Serialize(Fit, options));
        return new(assemblyPath, bodyFormed, bodyFlat, bodySvg, lidFormed, lidFlat, lidSvg, dfm, fit);

        string Formed(string name, ManufacturedSheetMetalResult part)
        {
            var exported = Step242Exporter.ExportBody(part.Part.FormedBody!);
            if (!exported.IsSuccess) throw new InvalidOperationException(string.Join("; ", exported.Diagnostics.Select(item => item.Message)));
            return Write(name + ".formed.step", exported.Value);
        }
        string Flat(string name, ManufacturedSheetMetalResult part)
        {
            var path = Path.Combine(root, name + ".flat.step");
            if (!SheetMetalManufacturingArtifacts.WriteFlatStep(path, part.Part, part.FlatPattern, out var diagnostics))
                throw new InvalidOperationException(string.Join("; ", diagnostics.Select(item => item.Message)));
            return path;
        }
        string Write(string name, string content) { var path = Path.Combine(root, name); File.WriteAllText(path, content, new UTF8Encoding(false)); return path; }
    }
}

/// <summary>
/// Forge-facing product call.  It specializes the same user-authored Firmament
/// Assembly Template and delegates Body/Lid geometry to the ordinary Sheet Metal
/// Template compiler through Assembly's domain-materialization seam.
/// </summary>
public static class EnclosureProductFamilies
{
    public static ManufacturedEnclosureProduct MakeEnclosureProduct(EnclosureProductSpec spec, string instanceName = "EnclosureProduct")
        => Compile(BuildSource(spec, instanceName), spec, "product:" + instanceName);

    public static ManufacturedEnclosureProduct Compile(string source, EnclosureProductSpec spec, string sourceIdentity = "<enclosure-product>")
    {
        Validate(spec);
        var parts = new Dictionary<string, ManufacturedSheetMetalResult>(StringComparer.Ordinal);
        AssemblyPartMaterialization? Materialize(string identity, string? declarations, string identitySource, IList<AssemblyDiagnostic> diagnostics)
        {
            var result = MaterializeAssemblyPartCore(identity, declarations, identitySource, diagnostics, out var manufactured, out var concreteName);
            if (manufactured is not null) parts[concreteName] = manufactured;
            return result;
        }

        var assembly = new AssemblyM1Pipeline().Compile(source, sourceIdentity, Materialize);
        if (!assembly.IsSuccess)
            throw new InvalidOperationException("Firmament enclosure product failed: " + string.Join("; ", assembly.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        var body = parts["AssemblyBody"]; var lid = parts["AssemblyLid"];
        var fit = AnalyzeFit(spec, assembly);
        var findings = ProductDfm(spec, assembly, body, lid, fit);
        var paths = assembly.Ir!.Instances.Select(item => item.Path.ToString())
            .Concat(["Product.Body.Front", "Product.Body.Rear", "Product.Body.FrontLip", "Product.Lid.Top", "Product.Lid.Front", "Product.Closure", "Product.Attachments"])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var identity = assembly.Ir.AssemblyDefinitions?.SingleOrDefault()?.SpecializationIdentity
            ?? "ElectronicsEnclosureProduct<Spec>";
        return new("ElectronicsEnclosureProduct", identity, spec, source, assembly, body, lid, new(findings), fit, paths,
            [new("Product.Body", body.SpecializationIdentity, 1), new("Product.Lid", lid.SpecializationIdentity, 1), new("Product.Attachments.M3", "ISO4762-placeholder", 4)]);
    }

    /// <summary>CLI/host adapter for Sheet Metal definitions encountered by the generic Assembly pipeline.</summary>
    public static AssemblyPartMaterialization? MaterializeAssemblyPart(string identity, string? declarations, string sourceIdentity, IList<AssemblyDiagnostic> diagnostics)
        => MaterializeAssemblyPartCore(identity, declarations, sourceIdentity, diagnostics, out _, out _);

    private static AssemblyPartMaterialization? MaterializeAssemblyPartCore(string identity, string? declarations, string sourceIdentity,
        IList<AssemblyDiagnostic> diagnostics, out ManufacturedSheetMetalResult? manufactured, out string concreteName)
    {
        manufactured = null; concreteName = string.Empty;
        if (!identity.StartsWith("ElectronicsEnclosure<", StringComparison.Ordinal)
            && !identity.StartsWith("RemovablePanLid<", StringComparison.Ordinal)) return null;
        var resolved = ResolveProjectedRecord(identity, declarations ?? string.Empty);
        concreteName = "Assembly" + (resolved.StartsWith("ElectronicsEnclosure", StringComparison.Ordinal) ? "Body" : "Lid");
        var partDeclarations = Regex.Replace(declarations ?? string.Empty, @"\bConcept\s+EnclosureProduct\s*\{[^}]*\}", string.Empty, RegexOptions.CultureInvariant);
        partDeclarations = Regex.Replace(partDeclarations, @"\bRecord\s+EnclosureProductSpec\s*\{[^}]*\}", string.Empty, RegexOptions.CultureInvariant);
        partDeclarations = Regex.Replace(partDeclarations, @"\bStatic\s+\w+\s*:\s*EnclosureProductSpec\s*=\s*EnclosureProductSpec\s*\{[^}]*\}", string.Empty, RegexOptions.CultureInvariant);
        partDeclarations = SelectPartExtensions(partDeclarations, concreteName.EndsWith("Body", StringComparison.Ordinal) ? "Body" : "Lid", concreteName);
        var concreteSource = partDeclarations + Environment.NewLine + $"SheetMetal {concreteName} = {resolved}" + Environment.NewLine;
        var compiled = SheetMetalFirmament.Compile(concreteSource, sourceIdentity + "#" + concreteName);
        if (!compiled.IsSuccess || compiled.Part?.FormedBody is null || compiled.FlatPattern is null)
        {
            foreach (var item in compiled.Diagnostics)
                diagnostics.Add(new("assembly-sheetmetal-materialization-failed", $"{identity}: {item.Code}: {item.Message}"));
            return null;
        }
        var specialization = compiled.TemplateInstantiations?.LastOrDefault()?.SpecializationIdentity ?? resolved;
        manufactured = new ManufacturedSheetMetalResult(
            resolved.StartsWith("ElectronicsEnclosure", StringComparison.Ordinal) ? "ElectronicsEnclosure" : "RemovablePanLid",
            specialization, compiled, SheetMetalDfm.Evaluate(compiled.Part, compiled.FlatPattern),
            SheetMetalConceptPaths.Inspect(compiled.Spec!, compiled.Part, compiled.FlatPattern),
            SheetMetalFabricationArtifacts.Create(compiled.Part, compiled.FlatPattern), SheetMetalSvgRenderer.Render(compiled.FlatPattern));
        return new(compiled.Part.FormedBody, specialization,
            [new SemanticProvenance("sheetmetal-template-specialization", specialization, resolved, SemanticSourceSpan.Generated(sourceIdentity))]);
    }

    private static IReadOnlyList<ProductDfmFinding> ProductDfm(EnclosureProductSpec spec, AssemblyM1CompilationResult assembly,
        ManufacturedSheetMetalResult body, ManufacturedSheetMetalResult lid, EnclosureFitEvidence fit)
    {
        var findings = body.Dfm.Findings.Select(item => new ProductDfmFinding(ProductDfmFindingKind.Part, item.RuleId, item.Status,
                "Product.Body" + (item.SubjectId is null ? string.Empty : "." + item.SubjectId), item.Message, item.Measured, item.Required))
            .Concat(lid.Dfm.Findings.Select(item => new ProductDfmFinding(ProductDfmFindingKind.Part, item.RuleId, item.Status,
                "Product.Lid" + (item.SubjectId is null ? string.Empty : "." + item.SubjectId), item.Message, item.Measured, item.Required))).ToList();
        var bodyInstance = assembly.Ir!.Instances.Single(item => item.Kind == AssemblyInstanceKind.Part && item.Path.Segments.Last() == "Body");
        var lidInstance = assembly.Ir.Instances.Single(item => item.Kind == AssemblyInstanceKind.Part && item.Path.Segments.Last() == "Lid");
        var geometryByInstance = assembly.Geometry!.Artifact.Instances.ToDictionary(item => item.InstanceStableId, StringComparer.Ordinal);
        var bodyBounds = geometryByInstance[bodyInstance.StableId].Metrics; var lidBounds = geometryByInstance[lidInstance.StableId].Metrics;
        var measured = new[] { bodyBounds.Minimum[0] - lidBounds.Minimum[0], lidBounds.Maximum[0] - bodyBounds.Maximum[0], bodyBounds.Minimum[1] - lidBounds.Minimum[1], lidBounds.Maximum[1] - bodyBounds.Maximum[1] }.Min();
        var clearancePass = measured >= spec.MinimumClearance && measured <= spec.MaximumClearance;
        findings.Add(new(ProductDfmFindingKind.Interface, "assembly-dfm-lid-clearance", clearancePass ? SheetMetalDfmStatus.Pass : SheetMetalDfmStatus.Fail,
            "Product.Closure", $"Authored nominal clearance is {spec.LidClearance:G4} mm; derived formed-envelope side clearance is {measured:G4} mm (policy {spec.MinimumClearance:G4}..{spec.MaximumClearance:G4} mm).", measured, spec.MinimumClearance));
        findings.Add(new(ProductDfmFindingKind.Interface, "assembly-dfm-tolerance-aware-fit",
            fit.VariationEnvelopeState == FitClassification.GuaranteedClearance ? SheetMetalDfmStatus.Pass : SheetMetalDfmStatus.Warning,
            fit.InterfacePath, $"Nominal {fit.NominalState}; production envelope {fit.VariationEnvelopeState} ({fit.MinimumPossibleSeparationMm:G4}..{fit.MaximumPossibleSeparationMm:G4} mm).",
            fit.MinimumPossibleSeparationMm, 0));
        findings.Add(new(ProductDfmFindingKind.Interface, "assembly-dfm-required-overlap", spec.LidOverlap > 0 ? SheetMetalDfmStatus.Pass : SheetMetalDfmStatus.Fail,
            "Product.Closure", $"Required lid/body overlap is {spec.LidOverlap:G4} mm.", spec.LidOverlap, double.Epsilon));
        var interference = assembly.Diagnostics.Any(item => item.Code == "assembly-solid-volume-interference");
        findings.Add(new(ProductDfmFindingKind.Assembly, "assembly-dfm-solid-interference", interference ? SheetMetalDfmStatus.Fail : SheetMetalDfmStatus.Pass,
            "Product.Body↔Product.Lid", interference ? "Exact BRep evidence found positive-volume penetration." : "Exact BRep evidence found no unintended positive-volume penetration."));
        var bodyHole = bodyInstance.SemanticRoot.ExposedMembers["Attachments"].ExposedMembers["HoleA"];
        var lidHole = lidInstance.SemanticRoot.ExposedMembers["Attachments"].ExposedMembers["HoleA"];
        var bodyPoint = (ExactPointBinding)AssemblyWorldQuery.Resolve(assembly.Ir, bodyHole.StableIdentity);
        var lidPoint = (ExactPointBinding)AssemblyWorldQuery.Resolve(assembly.Ir, lidHole.StableIdentity);
        var attachmentResidual = Math.Sqrt(Math.Pow(bodyPoint.X - lidPoint.X, 2) + Math.Pow(bodyPoint.Y - lidPoint.Y, 2) + Math.Pow(bodyPoint.Z - lidPoint.Z, 2));
        findings.Add(new(ProductDfmFindingKind.Interface, "assembly-dfm-attachment-alignment", attachmentResidual <= 1e-5 ? SheetMetalDfmStatus.Pass : SheetMetalDfmStatus.Fail,
            "Product.Attachments.HoleA", $"Corresponding body/lid attachment point residual is {attachmentResidual:G6} mm for nominal M{spec.FastenerDiameter:G3} placeholder screws.", attachmentResidual, 1e-5));
        findings.Add(new(ProductDfmFindingKind.Assembly, "assembly-dfm-service-removal", !interference ? SheetMetalDfmStatus.Pass : SheetMetalDfmStatus.Fail,
            "Product.Lid.ServiceDirection", "Bounded translational removal along +Z is unobstructed at the closed-position interference proof; general motion planning is not inferred."));
        return findings.OrderBy(item => item.Kind).ThenBy(item => item.SemanticPath, StringComparer.Ordinal).ThenBy(item => item.RuleId, StringComparer.Ordinal).ToArray();
    }

    private static EnclosureFitEvidence AnalyzeFit(EnclosureProductSpec spec, AssemblyM1CompilationResult assembly)
    {
        var fit = (assembly.Ir?.FitResults ?? []).Concat((assembly.Ir?.AssemblyDefinitions ?? []).SelectMany(definition => definition.LocalFitResults ?? [])).Single();
        var contributions = (fit.Contributions ?? []).Where(item => item.WorstCaseClearanceReductionMm > 0)
            .Select(item => new FitToleranceContribution(item.Source, item.SemanticPath, item.WorstCaseClearanceReductionMm)).ToArray();
        var frameSolutions = (assembly.Ir?.DatumMateSolutions ?? []).Concat((assembly.Ir?.AssemblyDefinitions ?? []).SelectMany(definition => definition.LocalDatumMateSolutions ?? [])).ToArray();
        var frameContact = frameSolutions.Length > 0 && frameSolutions.All(item => item.Status == "resolved" && item.ConstrainedDegreesOfFreedom == 6);
        return new("Product.Closure.Body.LidSeat↔Product.Closure.Lid.BodySeat", fit.NominalClearance, fit.WorstCaseMinimum, fit.WorstCaseMaximum,
            fit.MaximumPenetration, 0, null,
            frameContact ? "Intended seating relation certified by coincident semantic DatumFrames; contact area is not certified." : "No certified seating-contact witness.",
            "Nominal positive-volume overlap is checked by BrepSolidInterference. Tolerance penetration is an analytic conservative bound; no arbitrary-BRep Boolean volume is claimed.",
            fit.NominalState, fit.VariationEnvelopeState, contributions);
    }

    private static string ResolveProjectedRecord(string identity, string declarations)
    {
        var application = Regex.Match(identity, @"^(?<template>\w+)<Spec:\s*(?<outer>\w+)\.(?<field>\w+)>$", RegexOptions.CultureInvariant);
        if (!application.Success) return identity;
        var record = Regex.Match(declarations, $@"\bStatic\s+{Regex.Escape(application.Groups["outer"].Value)}\s*:\s*\w+\s*=\s*\w+\s*\{{(?<body>.*?)\}}", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        var field = record.Success ? Regex.Match(record.Groups["body"].Value, $@"\b{Regex.Escape(application.Groups["field"].Value)}\s*:\s*(?<value>\w+)", RegexOptions.CultureInvariant) : Match.Empty;
        if (!field.Success) throw new InvalidOperationException($"Projected product Record argument '{application.Groups["outer"].Value}.{application.Groups["field"].Value}' could not be resolved.");
        return $"{application.Groups["template"].Value}<Spec: {field.Groups["value"].Value}>";
    }

    private static string SelectPartExtensions(string source, string semanticPartName, string concreteName)
    {
        var chars = source.ToCharArray(); var selected = new List<string>();
        foreach (Match header in Regex.Matches(source, @"\bExtend\s+SheetMetal\s+(?<name>\w+)\s*\{", RegexOptions.CultureInvariant))
        {
            var open = header.Index + header.Length - 1; var depth = 0; var close = -1;
            for (var index = open; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}' && --depth == 0) { close = index; break; }
            }
            if (close < 0) continue;
            if (header.Groups["name"].Value == semanticPartName)
                selected.Add("Extend SheetMetal " + concreteName + source[(open - 1)..(close + 1)].TrimStart());
            Array.Fill(chars, ' ', header.Index, close - header.Index + 1);
        }
        return new string(chars) + Environment.NewLine + string.Join(Environment.NewLine, selected);
    }

    private static string BuildSource(EnclosureProductSpec spec, string instanceName)
    {
        var body = $"{Id(instanceName)}BodySpec"; var lid = $"{Id(instanceName)}LidSpec"; var product = $"{Id(instanceName)}Spec";
        var w = N(spec.Width); var d = N(spec.Depth); var h = N(spec.Height); var lw = N(spec.Width + 2 * spec.LidClearance); var ld = N(spec.Depth + 2 * spec.LidClearance);
        var seat = N(ExactBodySeatZ(spec));
        var variation = spec.ProductionVariation ?? new SheetMetalProductionVariation();
        return $$"""
            Use SheetMetal.ProductFamilies;
            Concept EnclosureProduct { Body: Part Lid: Part Closure: Mate Attachments: Mate }
            Record EnclosureProductSpec { Body: EnclosureSpec Lid: LidSpec Clearance: Length MinimumClearance: Length MaximumClearance: Length Overlap: Length FastenerDiameter: Length }
            Static {{body}}: EnclosureSpec = EnclosureSpec { Width: {{w}}mm Depth: {{d}}mm Height: {{h}}mm LidLipHeight: {{N(spec.LidOverlap)}}mm Thickness: {{N(spec.BodyThickness)}}mm InsideRadius: {{N(spec.InsideRadius)}}mm KFactor: {{N(spec.KFactor)}} ReliefPolicy: {{Relief(spec.ReliefPolicy)}} }
            Static {{lid}}: LidSpec = LidSpec { Width: {{lw}}mm Depth: {{ld}}mm SkirtHeight: {{N(spec.LidOverlap)}}mm Clearance: {{N(spec.LidClearance)}}mm Thickness: {{N(spec.LidThickness)}}mm InsideRadius: {{N(spec.InsideRadius)}}mm KFactor: {{N(spec.KFactor)}} ReliefPolicy: {{Relief(spec.ReliefPolicy)}} }
            Static {{product}}: EnclosureProductSpec = EnclosureProductSpec { Body: {{body}} Lid: {{lid}} Clearance: {{N(spec.LidClearance)}}mm MinimumClearance: {{N(spec.MinimumClearance)}}mm MaximumClearance: {{N(spec.MaximumClearance)}}mm Overlap: {{N(spec.LidOverlap)}}mm FastenerDiameter: {{N(spec.FastenerDiameter)}}mm }
            Extend SheetMetal Body {
              Hole FrontIndicator { On: Front; Center: (45mm, 18mm); Diameter: 3mm; }
              Cut RearEthernet { On: Rear; At: (80mm, 18mm); Profile: Rectangle; Width: 16mm; Length: 14mm; }
              Cut LeftVent { On: Left; At: (45mm, 15mm); Profile: Slot; Width: 3mm; Length: 18mm; }
              Cut RightVent { On: Right; At: (45mm, 15mm); Profile: Slot; Width: 3mm; Length: 18mm; }
              Hole LidMountA { On: Body; Center: (12mm, 12mm); Diameter: {{N(spec.FastenerDiameter)}}mm; }
            }
            Extend SheetMetal Lid { Hole LidMountA { On: Top; Center: ({{N(12 + spec.LidClearance)}}mm, {{N(12 + spec.LidClearance)}}mm); Diameter: {{N(spec.FastenerDiameter)}}mm; } }
            Interface LidClosure {
              Role Body requires DatumFrameCapable, DimensionalCapable; Role Lid requires DatumFrameCapable, DimensionalCapable;
              Lower FrameCoincident Lid.BodySeat Body.LidSeat OpposedDirection;
              Fit Body.Width inside Lid.Width per-side;
              ClearancePolicy Minimum {{N(spec.MinimumClearance)}}mm Maximum {{N(spec.MaximumClearance)}}mm;
              Variation Linear {{N(variation.LinearTolerance)}}mm Thickness {{N(variation.ThicknessTolerance)}}mm BendAngle {{N(variation.BendAngleToleranceDegrees)}}deg BendLocation {{N(variation.BendLocationTolerance)}}mm Coating {{N(variation.CoatingThickness)}}mm CoatingTolerance {{N(variation.CoatingThicknessTolerance)}}mm Engagement {{N(spec.LidOverlap)}}mm;
            }
            Interface AlignedScrewPattern { Role Body requires PointCapable; Role Lid requires PointCapable; Allow rotation:about-axis; }
            Template < Spec: EnclosureProductSpec >
            Assembly ElectronicsEnclosureProduct: EnclosureProduct {
              <Assembly ElectronicsEnclosureProduct>
                <Part Body = ElectronicsEnclosure<Spec: Spec.Body>>
                  Semantic Datums { DatumFrame LidSeat = [{{N(spec.Width / 2)}},{{N(spec.Depth / 2)}},{{seat}}] x [1,0,0] y [0,1,0] z [0,0,1]; Dimension Width = {{w}}mm; Dimension Overlap = {{N(spec.LidOverlap)}}mm; }
                  Semantic Attachments { Point HoleA = [12,12,{{seat}}]; }
                </Part>
                <Part Lid = RemovablePanLid<Spec: Spec.Lid>>
                  Semantic Datums { DatumFrame BodySeat = [{{N((spec.Width + 2 * spec.LidClearance) / 2)}},{{N((spec.Depth + 2 * spec.LidClearance) / 2)}},0] x [1,0,0] y [0,-1,0] z [0,0,-1]; Dimension Width = {{lw}}mm; Dimension Overlap = {{N(spec.LidOverlap)}}mm; }
                  Semantic Attachments { Point HoleA = [{{N(12 + spec.LidClearance)}},{{N(12 + spec.LidClearance)}},0]; }
                </Part>
              </Assembly>
              Anchor: ElectronicsEnclosureProduct.Body.Datums.LidSeat;
              Mate Closure: LidClosure { Body: ElectronicsEnclosureProduct.Body.Datums; Lid: ElectronicsEnclosureProduct.Lid.Datums; }
              Mate Attachments: AlignedScrewPattern { Body: ElectronicsEnclosureProduct.Body.Attachments; Lid: ElectronicsEnclosureProduct.Lid.Attachments; }
              Expose { Semantic Body = Body.Datums; Semantic Lid = Lid.Datums; DatumFrame ClosureFrame = Lid.Datums.BodySeat; }
            }
            Assembly {{Id(instanceName)}} { <Assembly {{Id(instanceName)}}><Assembly Product = ElectronicsEnclosureProduct<Spec: {{product}}>></Assembly></Assembly> Anchor: {{Id(instanceName)}}.Product; }
            """;
    }

    private static double ExactBodySeatZ(EnclosureProductSpec spec)
    {
        var body = SheetMetalProductFamilies.MakeEnclosure(new ElectronicsEnclosureSpec(spec.Width, spec.Depth, spec.Height,
            spec.LidOverlap, spec.BodyThickness, spec.InsideRadius, spec.KFactor, spec.ReliefPolicy), "__DatumProbe").Part.FormedBody!;
        var points = body.Topology.Vertices.Select(vertex => body.TryGetVertexPoint(vertex.Id, out var point) ? point : (Aetheris.Kernel.Core.Math.Point3D?)null)
            .Where(point => point.HasValue).Select(point => point!.Value).ToArray();
        if (points.Length == 0) throw new InvalidOperationException("The enclosure Body has no exact vertices from which to derive LidSeat.");
        return points.Max(point => point.Z);
    }

    private static void Validate(EnclosureProductSpec spec)
    {
        var variation = spec.ProductionVariation ?? new SheetMetalProductionVariation();
        var values = new[] { spec.Width, spec.Depth, spec.Height, spec.BodyThickness, spec.LidThickness, spec.LidOverlap, spec.MinimumClearance, spec.MaximumClearance, spec.FastenerDiameter };
        var variationValues = new[] { variation.LinearTolerance, variation.ThicknessTolerance, variation.BendAngleToleranceDegrees, variation.BendLocationTolerance, variation.CoatingThickness, variation.CoatingThicknessTolerance };
        if (values.Any(value => !double.IsFinite(value) || value <= 0) || !double.IsFinite(spec.LidClearance) || spec.LidClearance < 0)
            throw new ArgumentOutOfRangeException(nameof(spec), "Product dimensions must be finite; physical dimensions must be positive and clearance nonnegative.");
        if (spec.MaximumClearance < spec.MinimumClearance) throw new ArgumentException("Maximum clearance must not be below minimum clearance.", nameof(spec));
        if (variationValues.Any(value => !double.IsFinite(value) || value < 0)) throw new ArgumentOutOfRangeException(nameof(spec), "Production variation bounds must be finite and nonnegative.");
    }
    private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Id(string value) { var id = Regex.Replace(value, "[^A-Za-z0-9_]", "_"); return id.Length > 0 && (char.IsLetter(id[0]) || id[0] == '_') ? id : "Product_" + id; }
    private static string Relief(SheetReliefPolicy value) => value switch { SheetReliefPolicy.Round => "Round", SheetReliefPolicy.Rectangular => "Rectangular", _ => "Auto" };
}
