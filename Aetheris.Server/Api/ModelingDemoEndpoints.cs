using System.Globalization;
using Aetheris.Kernel.Firmament;

namespace Aetheris.Server.Api;

public sealed record ComposedBlockRequest(double Width, double Depth, double Height,
    double PadWidth, double PadDepth, double PadHeight);
public sealed record ComposedBlockResponse(string StepText, string ExportPath, string Source, string MaterializationRoute);

public static class ModelingDemoEndpoints
{
    public static void MapModelingDemoApi(this WebApplication app)
    {
        app.MapPost("/api/v1/demos/composed-block", (ComposedBlockRequest request) =>
        {
            var dimensions = new[] { request.Width, request.Depth, request.Height, request.PadWidth, request.PadDepth, request.PadHeight };
            if (dimensions.Any(value => !double.IsFinite(value) || value <= 0)
                || request.PadWidth > request.Width || request.PadDepth > request.Depth)
                return ApiMappings.BadRequestFromMessage("Dimensions must be positive; the raised block must fit within the base footprint.", "modeling-demo.dimensions");
            var source = $$"""
                Model ComposedBlock {
                  units: mm
                  Concept Struct Layout on XY {
                    Rect2 Stock { center: [0mm, 0mm]; size: [{{Mm(request.Width)}}, {{Mm(request.Depth)}}] }
                    Rect2 Pad { center: [0mm, 0mm]; size: [{{Mm(request.PadWidth)}}, {{Mm(request.PadDepth)}}] }
                  }
                  Profile BaseProfile using Layout { Loop Outer { Stock.Bottom |> Stock.Right |> Stock.Top |> Stock.Left |> Close } }
                  Profile PadProfile using Layout { Loop Outer { Pad.Bottom |> Pad.Right |> Pad.Top |> Pad.Left |> Close } }
                  Compose Body {
                    Base Stock { profile: BaseProfile; from: 0mm; to: {{Mm(request.Height)}}; role: Stock }
                    Boss RaisedBlock { on: Top; profile: PadProfile; height: {{Mm(request.PadHeight)}} }
                  }
                }
                """;
            var compiled = FirmamentBuildAndExport.CompileSource(source);
            if (!compiled.IsSuccess) return ApiMappings.KernelFailure(compiled.Diagnostics);
            var route = compiled.Value.EngineeringFeatures?.SingleOrDefault(feature => feature.Name == "RaisedBlock")?.MaterializationRoute;
            if (route is null) return ApiMappings.BadRequestFromMessage("The composed block has no materialization receipt.", "modeling-demo.compile");
            return ApiMappings.Ok(new ComposedBlockResponse(compiled.Value.StepText, "composed-block.step", source, route));
        });
    }

    private static string Mm(double value) => value.ToString("R", CultureInfo.InvariantCulture) + "mm";
}
