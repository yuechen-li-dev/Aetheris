namespace Aetheris.Kernel.Firmament.Diagnostics;

public static class FirmamentDiagnosticCodes
{
    public static readonly FirmamentDiagnosticCode ParseInvalidDocumentSyntax = new($"{FirmamentDiagnosticConventions.ParsePrefix}-0001");

    public static readonly FirmamentDiagnosticCode StructureMissingRequiredSection = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0001");
    public static readonly FirmamentDiagnosticCode StructureUnknownTopLevelSection = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0002");
    public static readonly FirmamentDiagnosticCode StructureMissingRequiredField = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0003");
    public static readonly FirmamentDiagnosticCode StructureInvalidSectionShape = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0004");
    public static readonly FirmamentDiagnosticCode StructureInvalidOpsEntryShape = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0005");
    public static readonly FirmamentDiagnosticCode StructureMissingRequiredOpField = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0006");
    public static readonly FirmamentDiagnosticCode StructureInvalidOpFieldValue = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0007");
    public static readonly FirmamentDiagnosticCode StructureUnknownOpKind = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0008");

    public static readonly FirmamentDiagnosticCode PrimitiveMissingRequiredField = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0009");
    public static readonly FirmamentDiagnosticCode PrimitiveInvalidFieldTypeOrShape = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0010");
    public static readonly FirmamentDiagnosticCode PrimitiveInvalidFieldValue = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0011");
    public static readonly FirmamentDiagnosticCode BooleanMissingRequiredField = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0012");
    public static readonly FirmamentDiagnosticCode BooleanInvalidFieldTypeOrShape = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0013");
    public static readonly FirmamentDiagnosticCode BooleanInvalidFieldValue = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0018");
    public static readonly FirmamentDiagnosticCode ValidationMissingRequiredField = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0014");
    public static readonly FirmamentDiagnosticCode ValidationInvalidFieldTypeOrShape = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0015");
    public static readonly FirmamentDiagnosticCode ValidationInvalidFieldValue = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0016");
    public static readonly FirmamentDiagnosticCode ValidationInvalidTargetShape = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0017");

    public static readonly FirmamentDiagnosticCode PlacementMissingRequiredField = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0019");
    public static readonly FirmamentDiagnosticCode PlacementInvalidAnchorShape = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0020");
    public static readonly FirmamentDiagnosticCode PlacementInvalidOffsetShape = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0021");
    public static readonly FirmamentDiagnosticCode PlacementInvalidOffsetValue = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0022");

    public static readonly FirmamentDiagnosticCode ReferenceDuplicateFeatureId = new($"{FirmamentDiagnosticConventions.ReferencePrefix}-0001");
    public static readonly FirmamentDiagnosticCode ReferenceUnknownFeatureId = new($"{FirmamentDiagnosticConventions.ReferencePrefix}-0002");
    public static readonly FirmamentDiagnosticCode ValidationTargetUnknownFeatureId = new($"{FirmamentDiagnosticConventions.ReferencePrefix}-0003");
    public static readonly FirmamentDiagnosticCode ValidationTargetUnknownSelectorRootFeatureId = new($"{FirmamentDiagnosticConventions.ReferencePrefix}-0004");
    public static readonly FirmamentDiagnosticCode ValidationTargetInvalidSelectorPortToken = new($"{FirmamentDiagnosticConventions.ReferencePrefix}-0005");
    public static readonly FirmamentDiagnosticCode ValidationTargetSelectorPortNotAllowedForFeatureKind = new($"{FirmamentDiagnosticConventions.ReferencePrefix}-0006");
    public static readonly FirmamentDiagnosticCode ValidationTargetSelectorResolvedEmpty = new($"{FirmamentDiagnosticConventions.ReferencePrefix}-0007");
    public static readonly FirmamentDiagnosticCode ValidationTargetSelectableCountMismatch = new($"{FirmamentDiagnosticConventions.ReferencePrefix}-0008");
    public static readonly FirmamentDiagnosticCode ValidationTargetNonManifoldBody = new($"{FirmamentDiagnosticConventions.ReferencePrefix}-0009");
    public static readonly FirmamentDiagnosticCode SelectorPlaceholder = new($"{FirmamentDiagnosticConventions.SelectorPrefix}-0001");

    public static readonly FirmamentDiagnosticCode SchemaUnknownProcess = new($"{FirmamentDiagnosticConventions.SchemaPrefix}-0001");
    public static readonly FirmamentDiagnosticCode SchemaMissingRequiredField = new($"{FirmamentDiagnosticConventions.SchemaPrefix}-0002");
    public static readonly FirmamentDiagnosticCode SchemaInvalidFieldTypeOrShape = new($"{FirmamentDiagnosticConventions.SchemaPrefix}-0003");
    public static readonly FirmamentDiagnosticCode SchemaInvalidFieldValue = new($"{FirmamentDiagnosticConventions.SchemaPrefix}-0004");

    public static readonly FirmamentDiagnosticCode LoweringPlaceholder = new($"{FirmamentDiagnosticConventions.LoweringPrefix}-0001");
}
