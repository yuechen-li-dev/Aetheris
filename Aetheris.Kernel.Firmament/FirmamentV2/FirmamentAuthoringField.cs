namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Compiler-owned, JSON-safe field help for a bounded authoring construct.</summary>
public sealed record FirmamentAuthoringField(
    string Name, string Type, bool Required, string Meaning,
    string? Default = null, IReadOnlyList<string>? Choices = null);
