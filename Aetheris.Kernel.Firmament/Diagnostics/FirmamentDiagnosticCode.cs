namespace Aetheris.Kernel.Firmament.Diagnostics;

public readonly record struct FirmamentDiagnosticCode(string Value)
{
    public override string ToString() => Value;
}
