using System.Globalization;
using System.Linq;
using System.Text;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Formatting;

internal static class FirmamentCanonicalFormatter
{
    private const string IndentUnit = "  ";

    public static string Format(FirmamentParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        AppendHeaderSection(builder, document.Firmament);
        builder.AppendLine();
        AppendModelSection(builder, document.Model);

        if (document.Schema is not null)
        {
            builder.AppendLine();
            AppendSchemaSection(builder, document.Schema);
        }

        builder.AppendLine();
        AppendOpsSection(builder, document.Ops);

        if (document.HasPmi)
        {
            builder.AppendLine();
            builder.Append("pmi[0]:\n");
        }

        return builder.ToString();
    }

    private static void AppendHeaderSection(StringBuilder builder, FirmamentParsedHeader header)
    {
        builder.Append("firmament:\n");
        AppendScalarField(builder, 1, "version", header.Version);
    }

    private static void AppendModelSection(StringBuilder builder, FirmamentParsedModelHeader model)
    {
        builder.Append("model:\n");
        AppendScalarField(builder, 1, "name", model.Name);
        AppendScalarField(builder, 1, "units", model.Units);
    }

    private static void AppendSchemaSection(StringBuilder builder, FirmamentParsedSchema schema)
    {
        builder.Append("schema:\n");

        if (schema.ProcessRaw is not null)
        {
            AppendScalarField(builder, 1, "process", schema.ProcessRaw);
        }

        if (schema.MinimumToolRadiusRaw is not null)
        {
            AppendScalarField(builder, 1, "minimum_tool_radius", schema.MinimumToolRadiusRaw);
        }
        if (schema.MinimumWallThicknessRaw is not null)
        {
            AppendScalarField(builder, 1, "minimum_wall_thickness", schema.MinimumWallThicknessRaw);
        }

        if (schema.PartingPlane is not null)
        {
            AppendScalarField(builder, 1, "parting_plane", schema.PartingPlane);
        }

        if (schema.HasGateLocation)
        {
            builder.Append(Indent(1)).Append("gate_location:\n");
            AppendScalarField(builder, 2, "x", schema.GateLocation?.XRaw ?? string.Empty);
            AppendScalarField(builder, 2, "y", schema.GateLocation?.YRaw ?? string.Empty);
            AppendScalarField(builder, 2, "z", schema.GateLocation?.ZRaw ?? string.Empty);
        }

        if (schema.DraftAngleRaw is not null)
        {
            AppendScalarField(builder, 1, "draft_angle", schema.DraftAngleRaw);
        }

        if (schema.PrinterResolutionRaw is not null)
        {
            AppendScalarField(builder, 1, "printer_resolution", schema.PrinterResolutionRaw);
        }
    }

    private static void AppendOpsSection(StringBuilder builder, FirmamentParsedOpsSection ops)
    {
        builder.Append("ops[").Append(ops.Entries.Count.ToString(CultureInfo.InvariantCulture)).Append("]:\n");

        for (var index = 0; index < ops.Entries.Count; index++)
        {
            if (index > 0)
            {
                builder.AppendLine();
            }

            builder.Append(Indent(1)).Append("-\n");
            AppendOpEntry(builder, ops.Entries[index], 2);
        }
    }

    private static void AppendOpEntry(StringBuilder builder, FirmamentParsedOpEntry entry, int indentLevel)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);

        EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "op");
        EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "id");

        switch (entry.KnownKind)
        {
            case FirmamentKnownOpKind.Box:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "size");
                break;
            case FirmamentKnownOpKind.Cylinder:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "radius");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "height");
                break;
            case FirmamentKnownOpKind.Cone:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "bottom_radius");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "top_radius");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "height");
                break;
            case FirmamentKnownOpKind.Torus:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "major_radius");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "minor_radius");
                break;
            case FirmamentKnownOpKind.Sphere:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "radius");
                break;
            case FirmamentKnownOpKind.TriangularPrism:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "base_width");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "base_depth");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "height");
                break;
            case FirmamentKnownOpKind.HexagonalPrism:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "across_flats");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "height");
                break;
            case FirmamentKnownOpKind.StraightSlot:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "length");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "width");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "height");
                break;
            case FirmamentKnownOpKind.Add:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "to");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "with");
                break;
            case FirmamentKnownOpKind.Subtract:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "from");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "with");
                break;
            case FirmamentKnownOpKind.Intersect:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "left");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "with");
                break;
            case FirmamentKnownOpKind.Draft:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "from");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "pull");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "angle");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "faces");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "direction");
                break;
            case FirmamentKnownOpKind.Chamfer:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "from");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "edges");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "distance");
                break;
            case FirmamentKnownOpKind.Fillet:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "from");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "edges");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "radius");
                break;
            case FirmamentKnownOpKind.ExpectExists:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "target");
                break;
            case FirmamentKnownOpKind.ExpectSelectable:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "target");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "count");
                break;
            case FirmamentKnownOpKind.ExpectManifold:
                break;
            case FirmamentKnownOpKind.PatternLinear:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "source");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "count");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "step");
                break;
            case FirmamentKnownOpKind.PatternCircular:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "source");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "count");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "axis");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "angle_degrees");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "angle_step_degrees");
                break;
            case FirmamentKnownOpKind.PatternMirror:
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "source");
                EmitIfPresent(builder, entry.RawFields, emitted, indentLevel, "plane");
                break;
        }

        foreach (var pair in entry.RawFields)
        {
            if (emitted.Contains(pair.Key) || string.Equals(pair.Key, "place", StringComparison.Ordinal))
            {
                continue;
            }

            AppendField(builder, indentLevel, pair.Key, pair.Value);
            emitted.Add(pair.Key);
        }

        if (entry.Placement is not null)
        {
            AppendPlacement(builder, indentLevel, entry.Placement);
            emitted.Add("place");
        }
    }

    private static void EmitIfPresent(StringBuilder builder, IReadOnlyDictionary<string, string> fields, ISet<string> emitted, int indentLevel, string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var value))
        {
            return;
        }

        AppendField(builder, indentLevel, fieldName, value);
        emitted.Add(fieldName);
    }

    private static void AppendPlacement(StringBuilder builder, int indentLevel, FirmamentParsedPlacement placement)
    {
        builder.Append(Indent(indentLevel)).Append("place:\n");
        var onValue = placement.On switch
        {
            FirmamentParsedPlacementOriginAnchor => "origin",
            FirmamentParsedPlacementSelectorAnchor selector => selector.Selector,
            _ => throw new InvalidOperationException("Unknown placement anchor.")
        };

        AppendScalarField(builder, indentLevel + 1, "on", onValue);
        builder.Append(Indent(indentLevel + 1))
            .Append("offset[")
            .Append(placement.Offset.Count.ToString(CultureInfo.InvariantCulture))
            .Append("]:\n");
        foreach (var component in placement.Offset)
        {
            builder.Append(Indent(indentLevel + 2)).Append(FormatNumber(component)).Append('\n');
        }
    }

    private static void AppendField(StringBuilder builder, int indentLevel, string fieldName, string rawValue)
    {
        var node = FirmamentFormatValueParser.Parse(rawValue);
        switch (node)
        {
            case FirmamentScalarValue scalar:
                AppendScalarField(builder, indentLevel, fieldName, scalar.Value);
                break;
            case FirmamentArrayValue array:
                builder.Append(Indent(indentLevel))
                    .Append(fieldName)
                    .Append('[')
                    .Append(array.Items.Count.ToString(CultureInfo.InvariantCulture))
                    .Append("]:\n");
                foreach (var item in array.Items)
                {
                    AppendArrayItem(builder, indentLevel + 1, item);
                }

                break;
            case FirmamentObjectValue obj:
                builder.Append(Indent(indentLevel)).Append(fieldName).Append(":\n");
                foreach (var member in obj.Members)
                {
                    AppendField(builder, indentLevel + 1, member.Name, member.Value.RawText);
                }

                break;
            default:
                throw new InvalidOperationException("Unknown formatter value node.");
        }
    }

    private static void AppendArrayItem(StringBuilder builder, int indentLevel, FirmamentFormatValue item)
    {
        switch (item)
        {
            case FirmamentScalarValue scalar:
                builder.Append(Indent(indentLevel)).Append(scalar.Value).Append('\n');
                break;
            case FirmamentObjectValue obj:
                builder.Append(Indent(indentLevel)).Append("-\n");
                foreach (var member in obj.Members)
                {
                    AppendField(builder, indentLevel + 1, member.Name, member.Value.RawText);
                }

                break;
            default:
                throw new InvalidOperationException("Canonical formatter arrays only support scalar and object entries.");
        }
    }

    private static void AppendScalarField(StringBuilder builder, int indentLevel, string fieldName, string value)
    {
        builder.Append(Indent(indentLevel)).Append(fieldName).Append(": ").Append(value).Append('\n');
    }

    private static string Indent(int level) => string.Concat(Enumerable.Repeat(IndentUnit, level));

    private static string FormatNumber(double value) => value.ToString("G", CultureInfo.InvariantCulture);
}
