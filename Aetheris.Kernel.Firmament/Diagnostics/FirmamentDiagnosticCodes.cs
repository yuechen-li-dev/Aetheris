namespace Aetheris.Kernel.Firmament.Diagnostics;

public static class FirmamentDiagnosticCodes
{
    public static readonly FirmamentDiagnosticCode ParsePlaceholder = new($"{FirmamentDiagnosticConventions.ParsePrefix}-0001");
    public static readonly FirmamentDiagnosticCode StructurePlaceholder = new($"{FirmamentDiagnosticConventions.StructurePrefix}-0001");
    public static readonly FirmamentDiagnosticCode ReferencePlaceholder = new($"{FirmamentDiagnosticConventions.ReferencePrefix}-0001");
    public static readonly FirmamentDiagnosticCode SelectorPlaceholder = new($"{FirmamentDiagnosticConventions.SelectorPrefix}-0001");
    public static readonly FirmamentDiagnosticCode SchemaPlaceholder = new($"{FirmamentDiagnosticConventions.SchemaPrefix}-0001");
    public static readonly FirmamentDiagnosticCode LoweringPlaceholder = new($"{FirmamentDiagnosticConventions.LoweringPrefix}-0001");
}
