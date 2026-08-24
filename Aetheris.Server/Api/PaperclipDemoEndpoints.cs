using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Server.Api;

public sealed record PaperclipDemoRequest(double WireDiameter, double OuterLegLength, double InnerLegLength,
    double OuterBendRadius, double InnerBendRadius, string? Material);

public sealed record PaperclipDemoResponse(string StepText, string SpecializationIdentity, double CenterlineLength,
    double MassGrams, double PaperclipsPerMeter, IReadOnlyList<double> Bounds, string Material,
    bool Manufacturable, bool StepAp242, bool Deterministic);

public static class PaperclipDemoEndpoints
{
    public static void MapPaperclipDemoApi(this WebApplication app)
    {
        app.MapPost("/api/v1/demos/maximum-paperclips", (PaperclipDemoRequest request) =>
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WireDiameter"] = Mm(request.WireDiameter), ["OuterLegLength"] = Mm(request.OuterLegLength),
                ["InnerLegLength"] = Mm(request.InnerLegLength), ["OuterBendRadius"] = Mm(request.OuterBendRadius),
                ["InnerBendRadius"] = Mm(request.InnerBendRadius),
                ["Material"] = System.Text.Json.JsonSerializer.Serialize(request.Material ?? "Standard.Materials.StainlessSteel.304_Annealed"),
            };
            var expansion = FirmamentTemplateHostBridge.Expand(PaperclipTemplateLibrary.Source, "PaperclipTemplate", "InteractivePaperclip",
                new Dictionary<string, FirmamentHostArgument>(StringComparer.Ordinal) { ["P"] = new(string.Empty, "PaperclipPolicy", fields) }, out var diagnostics);
            if (expansion is null) return ApiMappings.BadRequestFromMessage(string.Join("; ", diagnostics), "maximum-paperclips.bind");
            var compiled = FirmamentBuildAndExport.CompileSource(expansion.ExpandedSource);
            if (!compiled.IsSuccess || compiled.Value.Sweep is null)
                return ApiMappings.KernelFailure(compiled.Diagnostics);
            var sweep = compiled.Value.Sweep;
            return ApiMappings.Ok(new PaperclipDemoResponse(compiled.Value.StepText, expansion.SpecializationIdentity,
                sweep.CenterlineLength, sweep.MassKilograms * 1000d, 1000d / sweep.CenterlineLength, sweep.Bounds,
                sweep.Material, sweep.EnclosedManifold, sweep.StepReimportSucceeded, true));
        });
    }

    private static string Mm(double value) => double.IsFinite(value)
        ? value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "mm"
        : "NaNmm";
}
