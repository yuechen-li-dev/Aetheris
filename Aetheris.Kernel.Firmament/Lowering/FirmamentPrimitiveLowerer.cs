using System.Linq;
using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Lowering;

internal static class FirmamentPrimitiveLowerer
{
    private const string ValidationSkipReason = "unsupported-op-in-m3b-boolean-lowering";

    public static KernelResult<FirmamentPrimitiveLoweringPlan> Lower(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        var loweredPrimitives = new List<FirmamentLoweredPrimitive>();
        var loweredBooleans = new List<FirmamentLoweredBoolean>();
        var skippedOps = new List<FirmamentLoweringSkippedOp>();

        for (var index = 0; index < parsedDocument.Ops.Entries.Count; index++)
        {
            var entry = parsedDocument.Ops.Entries[index];
            switch (entry.KnownKind)
            {
                case FirmamentKnownOpKind.Box:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        OpIndex: index,
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.Box,
                        Parameters: LowerBoxParameters(entry.RawFields["size"]),
                        Placement: LowerPlacement(entry.Placement)));
                    break;

                case FirmamentKnownOpKind.Cylinder:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        OpIndex: index,
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.Cylinder,
                        Parameters: new FirmamentLoweredCylinderParameters(
                            Radius: ParseScalar(entry.RawFields["radius"]),
                            Height: ParseScalar(entry.RawFields["height"])),
                        Placement: LowerPlacement(entry.Placement)));
                    break;

                case FirmamentKnownOpKind.Cone:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        OpIndex: index,
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.Cone,
                        Parameters: new FirmamentLoweredConeParameters(
                            BottomRadius: ParseScalar(entry.RawFields["bottom_radius"]),
                            TopRadius: ParseScalar(entry.RawFields["top_radius"]),
                            Height: ParseScalar(entry.RawFields["height"])),
                        Placement: LowerPlacement(entry.Placement)));
                    break;

                case FirmamentKnownOpKind.Torus:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        OpIndex: index,
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.Torus,
                        Parameters: new FirmamentLoweredTorusParameters(
                            MajorRadius: ParseScalar(entry.RawFields["major_radius"]),
                            MinorRadius: ParseScalar(entry.RawFields["minor_radius"])),
                        Placement: LowerPlacement(entry.Placement)));
                    break;

                case FirmamentKnownOpKind.Sphere:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        OpIndex: index,
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.Sphere,
                        Parameters: new FirmamentLoweredSphereParameters(
                            Radius: ParseScalar(entry.RawFields["radius"])),
                        Placement: LowerPlacement(entry.Placement)));
                    break;
                case FirmamentKnownOpKind.TriangularPrism:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        OpIndex: index,
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.TriangularPrism,
                        Parameters: new FirmamentLoweredTriangularPrismParameters(
                            BaseWidth: ParseScalar(entry.RawFields["base_width"]),
                            BaseDepth: ParseScalar(entry.RawFields["base_depth"]),
                            Height: ParseScalar(entry.RawFields["height"])),
                        Placement: LowerPlacement(entry.Placement)));
                    break;
                case FirmamentKnownOpKind.HexagonalPrism:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        OpIndex: index,
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.HexagonalPrism,
                        Parameters: new FirmamentLoweredHexagonalPrismParameters(
                            AcrossFlats: ParseScalar(entry.RawFields["across_flats"]),
                            Height: ParseScalar(entry.RawFields["height"])),
                        Placement: LowerPlacement(entry.Placement)));
                    break;
                case FirmamentKnownOpKind.StraightSlot:
                    loweredPrimitives.Add(new FirmamentLoweredPrimitive(
                        OpIndex: index,
                        FeatureId: entry.RawFields["id"],
                        Kind: FirmamentLoweredPrimitiveKind.StraightSlot,
                        Parameters: new FirmamentLoweredStraightSlotParameters(
                            Length: ParseScalar(entry.RawFields["length"]),
                            Width: ParseScalar(entry.RawFields["width"]),
                            Height: ParseScalar(entry.RawFields["height"])),
                        Placement: LowerPlacement(entry.Placement)));
                    break;

                case FirmamentKnownOpKind.Add:
                    loweredBooleans.Add(LowerBoolean(index, entry, FirmamentLoweredBooleanKind.Add, "to"));
                    break;

                case FirmamentKnownOpKind.Subtract:
                    loweredBooleans.Add(LowerBoolean(index, entry, FirmamentLoweredBooleanKind.Subtract, "from"));
                    break;

                case FirmamentKnownOpKind.Intersect:
                    loweredBooleans.Add(LowerBoolean(index, entry, FirmamentLoweredBooleanKind.Intersect, "left"));
                    break;

                default:
                    skippedOps.Add(new FirmamentLoweringSkippedOp(
                        OpIndex: index,
                        OpName: entry.OpName,
                        KnownKind: entry.KnownKind,
                        Family: entry.Family,
                        Reason: ValidationSkipReason));
                    break;
            }
        }

        return KernelResult<FirmamentPrimitiveLoweringPlan>.Success(new FirmamentPrimitiveLoweringPlan(loweredPrimitives, loweredBooleans, skippedOps));
    }


    private static FirmamentLoweredPlacement? LowerPlacement(FirmamentParsedPlacement? placement)
    {
        if (placement is null)
        {
            return null;
        }

        FirmamentLoweredPlacementAnchor? loweredAnchor = placement.On switch
        {
            FirmamentParsedPlacementOriginAnchor => new FirmamentLoweredPlacementOriginAnchor(),
            FirmamentParsedPlacementSelectorAnchor selector => new FirmamentLoweredPlacementSelectorAnchor(selector.Selector),
            _ => null
        };
        return new FirmamentLoweredPlacement(
            loweredAnchor,
            placement.Offset,
            placement.OnFace,
            placement.CenteredOn,
            placement.AroundAxis,
            placement.RadialOffset,
            placement.AngleDegrees,
            placement.UnknownFields);
    }

    private static FirmamentLoweredBoolean LowerBoolean(
        int opIndex,
        FirmamentParsedOpEntry entry,
        FirmamentLoweredBooleanKind booleanKind,
        string primaryFieldName)
    {
        var tool = ParseTool(entry.RawFields["with"]);

        return new FirmamentLoweredBoolean(
            OpIndex: opIndex,
            FeatureId: entry.RawFields["id"],
            Kind: booleanKind,
            PrimaryReferenceField: primaryFieldName,
            PrimaryReferenceFeatureId: entry.RawFields[primaryFieldName],
            Tool: tool,
            Placement: LowerPlacement(entry.Placement));
    }

    private static FirmamentLoweredToolOp ParseTool(string rawWith)
    {
        var trimmed = rawWith.Trim();

        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            var body = trimmed[1..^1].Trim();
            var pairs = SplitTopLevelCommaSeparated(body);
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in pairs)
            {
                var separator = pair.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                var key = NormalizeArrayFieldName(pair[..separator].Trim());
                var value = pair[(separator + 1)..].Trim();
                if (key.Length > 0)
                {
                    fields[key] = value;
                }
            }

            fields.TryGetValue("op", out var opName);
            return new FirmamentLoweredToolOp(opName ?? string.Empty, fields, rawWith);
        }

        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            using var jsonDoc = JsonDocument.Parse(trimmed);
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                fields[property.Name] = property.Value.ToString();
            }

            fields.TryGetValue("op", out var opName);
            return new FirmamentLoweredToolOp(opName ?? string.Empty, fields, rawWith);
        }

        return new FirmamentLoweredToolOp(string.Empty, new Dictionary<string, string>(StringComparer.Ordinal), rawWith);
    }


    private static IReadOnlyList<string> SplitTopLevelCommaSeparated(string raw)
    {
        var parts = new List<string>();
        var start = 0;
        var squareDepth = 0;
        var curlyDepth = 0;

        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            switch (ch)
            {
                case '[':
                    squareDepth++;
                    break;
                case ']':
                    squareDepth = Math.Max(0, squareDepth - 1);
                    break;
                case '{':
                    curlyDepth++;
                    break;
                case '}':
                    curlyDepth = Math.Max(0, curlyDepth - 1);
                    break;
                case ',':
                    if (squareDepth == 0 && curlyDepth == 0)
                    {
                        var part = raw[start..i].Trim();
                        if (part.Length > 0)
                        {
                            parts.Add(part);
                        }

                        start = i + 1;
                    }

                    break;
            }
        }

        var finalPart = raw[start..].Trim();
        if (finalPart.Length > 0)
        {
            parts.Add(finalPart);
        }

        return parts;
    }

    private static string NormalizeArrayFieldName(string fieldName)
    {
        var bracketIndex = fieldName.IndexOf('[', StringComparison.Ordinal);
        return bracketIndex > 0 && fieldName.EndsWith(']')
            ? fieldName[..bracketIndex]
            : fieldName;
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
