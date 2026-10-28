using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

public static partial class FirmamentBuildAndExport
{
    private static KernelResult<FirmamentStepExportResult> ExportPathFeatures(FirmamentV2Document document)
    {
        var stock = document.Solid;
        if(stock.Box is not null)return ExportBoxPathNetwork(document);
        var cylinder = stock.Cylinder!;
        var height = cylinder.Height;
        var built = BrepCircumferentialGroove.BuildCylinder(cylinder.Radius, height, document.PathFeatures.Select(f =>
            new CircumferentialGroove(f.Feature.Name, f.Feature.Section, f.Feature.Width, f.Feature.Extent, f.Station - height / 2, f.Support == "InnerWall")).ToArray(), stock.Hollow?.WallThickness);
        if (!built.IsSuccess)
            return KernelResult<FirmamentStepExportResult>.Failure(built.Diagnostics.Select(d =>
            {
                var operation = document.PathFeatures.FirstOrDefault(f => d.Message.Contains(f.Feature.Name + ":", StringComparison.Ordinal));
                var span = operation?.SourceSpan ?? stock.SourceSpan;
                return d with { SourceSpan = span is null ? null : new(span.Start, span.Length)
                    { LocationNote = operation is null ? "Enclosing material host; no unique individual feature location was supplied." : null } };
            }).ToArray());
        var body = FirmamentPrimitiveExecutionTranslation.TranslateBody(built.Value.Body, new(0, 0, height / 2));
        var annotations = BuildV2SemanticPmi(document, [], stock.Name, body: body);
        var exported = Step242Exporter.ExportBody(body, annotations, new Step242ExportOptions {
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        if (!exported.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(exported.Diagnostics);
        var descendants = document.PathFeatures.SelectMany(f => built.Value.Faces[f.Feature.Name].Select((face, i) =>
            new SemanticTopologyDescendant($"{f.Host}.{f.Feature.Name}.Face{i}", "Face", SemanticTopologyRole.PathFeatureFace,
                f.Host + "." + f.Feature.Name, Face: face, ParentStableId: f.Host,
                Addressability: SemanticTopologyAddressability.DerivedStable))).ToArray();
        return KernelResult<FirmamentStepExportResult>.Success(new(exported.Value, document.ModelName, 0, "path-feature", "AnalyticPathFeature") {
            RuntimeBody = body, RuntimeBodySourceName = stock.Name,
            RuntimeCorrespondence = new(stock.Name, descendants, ["UnifiedAst", "ConceptGuide", "AnalyticPathFeature"],
                document.PathFeatures.ToDictionary(f => f.Host + "." + f.Feature.Name, f => f.SourceSpan)),
            MaterializedFeatures = document.PathFeatures.Select(f => new FirmamentFeatureReceipt(f.Host, f.Feature.Add ? "Rib" : "Groove", f.Feature.Name)).ToArray(),
            EngineeringFeatures = document.PathFeatures.Select(f => new FirmamentEngineeringFeatureReport(
                f.Feature.Name, f.Feature.Add ? "Rib" : "Groove", f.Host + "." + f.Feature.Name, f.Host, f.Support,
                f.GuideReference, f.Feature.Extent, f.Feature.Add ? "Height" : "Depth", f.Feature.Add ? "Add" : "Remove",
                PolicySource: "ManualConceptGuide", MaterializationRoute: "AnalyticPathFeature",
                ToolFamily: f.Feature.Section.ToString(), Direction: f.Feature.Add ? "OutOfSupport" : "IntoSupport")).ToArray()
        }, exported.Diagnostics);
    }
}
