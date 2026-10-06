using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Continuum.Backends.Sdf;

namespace Aetheris.Kernel.Firmament;

/// <summary>Compiler-owned display eligibility. The field never owns engineering topology.</summary>
public sealed record FirmamentCirRetention(string DefinitionId, string Qualification, string? FallbackReason,
    string? StructuralIdentity, string? FieldSource, double[]? MinimumMm, double[]? MaximumMm)
{
    public const string Version = "aetheris/cir-retention/1";
    public string Schema => Version;
    [JsonIgnore] public SdfNode? RuntimeRoot { get; init; }

    internal static FirmamentCirRetention FromRoot(SdfNode? root, string step)
    {
        var definition = "definition:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step)))[..16].ToLowerInvariant();
        if (root is null) return new(definition, "mesh", "no-qualified-cir-mirror", null, null, null, null);
        var display = CirVisualTsLowerer.Lower(root);
        if (!display.Success) return new(definition, "mesh", display.FallbackReason, null, null, null, null)
            { RuntimeRoot = root };
        var program = display.Program!;
        return new(definition, "cir-qualified", null, program.StructuralHash, program.FieldSource,
            [program.Bounds.Min.X, program.Bounds.Min.Y, program.Bounds.Min.Z],
            [program.Bounds.Max.X, program.Bounds.Max.Y, program.Bounds.Max.Z]) { RuntimeRoot = root };
    }
}
