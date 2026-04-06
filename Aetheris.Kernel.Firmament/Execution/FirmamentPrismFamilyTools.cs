using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum FirmamentPrismToolKind
{
    TriangularPrism,
    HexagonalPrism,
    StraightSlot
}

internal sealed record FirmamentPrismToolDescriptor(
    FirmamentPrismToolKind Kind,
    string OpName,
    IReadOnlyList<string> RequiredFields,
    Func<FirmamentLoweredToolOp, KernelResult<BrepBody>> CreateBody,
    Func<FirmamentLoweredToolOp, double> ResolveHeight,
    Func<FirmamentLoweredToolOp, IReadOnlyList<ProfilePoint2D>> ResolveFootprint);

internal static class FirmamentPrismFamilyTools
{
    private static readonly FirmamentPrismToolDescriptor TriangularPrismDescriptor = new(
        FirmamentPrismToolKind.TriangularPrism,
        "triangular_prism",
        ["base_width", "base_depth", "height"],
        static tool => BrepPrimitives.CreateTriangularPrism(
            FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["base_width"]),
            FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["base_depth"]),
            FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["height"])),
        static tool => FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["height"]),
        static tool =>
        [
            new ProfilePoint2D(-FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["base_width"]) * 0.5d, -FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["base_depth"]) * 0.5d),
            new ProfilePoint2D(FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["base_width"]) * 0.5d, -FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["base_depth"]) * 0.5d),
            new ProfilePoint2D(0d, FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["base_depth"]) * 0.5d),
        ]);

    private static readonly FirmamentPrismToolDescriptor HexagonalPrismDescriptor = new(
        FirmamentPrismToolKind.HexagonalPrism,
        "hexagonal_prism",
        ["across_flats", "height"],
        static tool => BrepPrimitives.CreateHexagonalPrism(
            FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["across_flats"]),
            FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["height"])),
        static tool => FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["height"]),
        static tool =>
        {
            var acrossFlats = FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["across_flats"]);
            var circumradius = acrossFlats / double.Sqrt(3d);
            return Enumerable.Range(0, 6)
                .Select(index =>
                {
                    var angle = (double.Pi / 3d) * index;
                    return new ProfilePoint2D(circumradius * double.Cos(angle), circumradius * double.Sin(angle));
                })
                .ToArray();
        });

    private static readonly FirmamentPrismToolDescriptor StraightSlotDescriptor = new(
        FirmamentPrismToolKind.StraightSlot,
        "straight_slot",
        ["length", "width", "height"],
        static tool => BrepPrimitives.CreateStraightSlot(
            FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["length"]),
            FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["width"]),
            FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["height"])),
        static tool => FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["height"]),
        static tool =>
        {
            var length = FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["length"]);
            var width = FirmamentPrimitiveToolParsing.ParseScalar(tool.RawFields["width"]);
            const int semicircleSegments = 8;
            var halfLength = length * 0.5d;
            var radius = width * 0.5d;
            var centerOffset = halfLength - radius;
            var profileVertices = new List<ProfilePoint2D>(2 * (semicircleSegments + 1));

            for (var i = 0; i <= semicircleSegments; i++)
            {
                var t = double.Pi * (i / (double)semicircleSegments) - (double.Pi * 0.5d);
                profileVertices.Add(new ProfilePoint2D(centerOffset + (radius * double.Cos(t)), radius * double.Sin(t)));
            }

            for (var i = 0; i <= semicircleSegments; i++)
            {
                var t = double.Pi * (i / (double)semicircleSegments) + (double.Pi * 0.5d);
                profileVertices.Add(new ProfilePoint2D(-centerOffset + (radius * double.Cos(t)), radius * double.Sin(t)));
            }

            return profileVertices;
        });

    private static readonly IReadOnlyDictionary<string, FirmamentPrismToolDescriptor> DescriptorsByOpName =
        new Dictionary<string, FirmamentPrismToolDescriptor>(StringComparer.Ordinal)
        {
            [TriangularPrismDescriptor.OpName] = TriangularPrismDescriptor,
            [HexagonalPrismDescriptor.OpName] = HexagonalPrismDescriptor,
            [StraightSlotDescriptor.OpName] = StraightSlotDescriptor
        };

    public static bool TryGetDescriptor(string opName, out FirmamentPrismToolDescriptor descriptor)
        => DescriptorsByOpName.TryGetValue(opName, out descriptor!);

    public static bool IsPrismTool(string opName)
        => DescriptorsByOpName.ContainsKey(opName);

    public static KernelResult<BrepBody> TryCreateBody(FirmamentLoweredToolOp tool)
    {
        if (!TryGetDescriptor(tool.OpName, out var descriptor))
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, $"Prism-family tool lookup failed for unsupported op '{tool.OpName}'.")
            ]);
        }

        foreach (var requiredField in descriptor.RequiredFields)
        {
            if (!tool.RawFields.TryGetValue(requiredField, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return KernelResult<BrepBody>.Failure([
                    new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Error,
                        $"Boolean execution expected validated nested field 'with.{requiredField}' for tool op '{descriptor.OpName}'.")
                ]);
            }
        }

        return descriptor.CreateBody(tool);
    }

    public static Vector3D ResolveDefaultFrameTranslation(FirmamentLoweredToolOp tool)
    {
        if (!TryGetDescriptor(tool.OpName, out var descriptor))
        {
            return Vector3D.Zero;
        }

        var height = descriptor.ResolveHeight(tool);
        return new Vector3D(0d, 0d, height * 0.5d);
    }

    public static IReadOnlyList<ProfilePoint2D> ResolveFootprint(FirmamentPrismToolDescriptor descriptor, FirmamentLoweredToolOp tool)
        => descriptor.ResolveFootprint(tool);
}
