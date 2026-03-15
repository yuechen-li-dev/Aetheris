using System.Linq;
using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Lowering;

internal static class FirmamentPrimitiveLowerer
{
    private const string PrimitiveOnlySkipReason = "unsupported-op-in-m3a-primitive-only-lowering";

    public static KernelResult<FirmamentPrimitiveLoweringPlan> Lower(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        var loweredPrimitives = new List<FirmamentLoweredPrimitive>();
        var skippedOps = new List<FirmamentLoweringSkippedOp>();

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];
            switch (entry.KnownKind)
            {
                case FirmamentKnownOpKind.Box:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.Box,
                        Parameters: LowerBoxParameters(entry.RawFields["size"])));
                    break;

                case FirmamentKnownOpKind.Cylinder:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.Cylinder,
                        Parameters: new FirmamentLoweredCylinderParameters(
                            Radius: ParseScalar(entry.RawFields["radius"]),
                            Height: ParseScalar(entry.RawFields["height"]))));
                    break;

                case FirmamentKnownOpKind.Sphere:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.Sphere,
                        Parameters: new FirmamentLoweredSphereParameters(
                            Radius: ParseScalar(entry.RawFields["radius"]))));
                    break;

                default:
                    skippedOps.Add(new FirmamentLoweringSkippedOp(
                        OpIndex: index,
                        OpName: entry.OpName,
                        KnownKind: entry.KnownKind,
                        Family: entry.Family,
                        Reason: PrimitiveOnlySkipReason));
                    break;
            }
        }

        return KernelResult<FirmamentPrimitiveLoweringPlan>.Success(new FirmamentPrimitiveLoweringPlan(loweredPrimitives, skippedOps));
    }

    private static FirmamentLoweredBoxParameters LowerBoxParameters(string sizeRaw)
    {
        using var doc = JsonDocument.Parse(sizeRaw);
        var elements = doc.RootElement.EnumerateArray().ToArray();

        return new FirmamentLoweredBoxParameters(
            ParseScalar(elements[0].ToString()),
            ParseScalar(elements[1].ToString()),
            ParseScalar(elements[2].ToString()));
    }

    private static double ParseScalar(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        return double.Parse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
