using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop;

public static class FirmamentV2ConceptApplicationAdapter
{
    public static FirmamentConceptApplicationView Adapt(FirmamentV2ManufacturingConceptDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new FirmamentConceptApplicationView(
            FirmamentConceptApplicationKind.Manufacturing,
            null,
            AdaptConceptId(declaration.Application),
            AdaptFields(declaration.BoundFields ?? []),
            FirmamentV2InteropValueAdapter.AdaptSourceSpan(declaration.SourceSpan));
    }

    public static FirmamentConceptApplicationView Adapt(FirmamentV2FeatureConceptDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new FirmamentConceptApplicationView(
            FirmamentConceptApplicationKind.Feature,
            declaration.Name,
            AdaptConceptId(declaration.Application),
            AdaptFields(declaration.BoundFields ?? []),
            FirmamentV2InteropValueAdapter.AdaptSourceSpan(declaration.SourceSpan));
    }

    public static ConceptId AdaptConceptId(FirmamentV2ConceptApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return new ConceptId(application.FamilyName, application.ConceptName);
    }

    public static FirmamentConceptFieldView Adapt(FirmamentV2BoundConceptField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return field.TargetSource is not null
            ? new FirmamentConceptFieldView(
                field.Name,
                FirmamentFieldKind.Target,
                null,
                field.TargetSource,
                FirmamentV2InteropValueAdapter.AdaptSourceSpan(field.Field.SourceSpan))
            : new FirmamentConceptFieldView(
                field.Name,
                FirmamentFieldKind.Value,
                field.BoundValue is null ? null : FirmamentV2InteropValueAdapter.AdaptValue(field.Name, field.BoundValue),
                null,
                FirmamentV2InteropValueAdapter.AdaptSourceSpan(field.Field.SourceSpan));
    }

    private static IReadOnlyList<FirmamentConceptFieldView> AdaptFields(IReadOnlyList<FirmamentV2BoundConceptField> fields) =>
        fields.Select(Adapt).OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
}
