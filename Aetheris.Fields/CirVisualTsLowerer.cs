using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Backends.Sdf;

public sealed record CirVisualTsProgram(string StructuralHash, string FieldSource, SdfBounds Bounds)
{
    // The admitted rigid primitive/Boolean family is 1-Lipschitz in source units.
    // This is an exterior stepping bound, not a promise of exact Euclidean distance.
    public bool ConservativeExteriorStep { get; internal init; }
    public string SourceLicense => "AGPL-3.0-only";
}

public sealed record CirVisualTsLoweringResult(CirVisualTsProgram? Program, string? FallbackReason)
{
    public bool Success => Program is not null;
}

/// <summary>Specializes the existing CIR tape into Visual TypeScript source. No shader-side tape interpreter.</summary>
public static class CirVisualTsLowerer
{
    public const string CompatibilityVersion = "cir-visual-ts/2";

    public static CirVisualTsLoweringResult Lower(SdfNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        string? rejection = Validate(root);
        if (rejection is not null)
        {
            return new(null, rejection);
        }
        SdfTape tape = SdfTapeLowerer.Lower(root);
        var source = new StringBuilder();
        source.AppendLine("export function Field(x: f32, y: f32, z: f32): f32 {");
        foreach (SdfTapeInstruction instruction in tape.Instructions)
        {
            string value = instruction.OpCode switch
            {
                SdfTapeOpCode.Min => $"Min(v{instruction.InputA}, v{instruction.InputB})",
                SdfTapeOpCode.Max => $"Max(v{instruction.InputA}, v{instruction.InputB})",
                SdfTapeOpCode.Neg => $"-v{instruction.InputA}",
                _ => EmitPrimitive(source, tape, instruction),
            };
            source.AppendLine($"    const v{instruction.DestSlot}: f32 = {value};");
        }
        source.AppendLine($"    return v{tape.OutputSlot};");
        source.AppendLine("}");
        string field = source.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CompatibilityVersion + "\n" + field))).ToLowerInvariant();
        return new(new CirVisualTsProgram(hash, field, root.Bounds)
        {
            ConservativeExteriorStep = true,
        }, null);
    }

    private static string? Validate(SdfNode node)
    {
        // The X0 numerical range is explicit; broader CIR occupancy capability alone is not admission.
        bool Positive(double value) => double.IsFinite(value) && value >= 0.00001 && value <= 1_000_000;
        switch (node)
        {
            case SdfSphereNode sphere when Positive(sphere.Radius):
            case SdfCylinderNode cylinder when Positive(cylinder.Radius) && Positive(cylinder.Height):
            case SdfBoxNode box when Positive(box.Width) && Positive(box.Height) && Positive(box.Depth):
            case SdfTorusNode torus when Positive(torus.MinorRadius) && Positive(torus.MajorRadius) && torus.MajorRadius >= torus.MinorRadius:
            case SdfConeNode cone when Positive(cone.Height) && cone.BottomRadius >= 0 && cone.TopRadius >= 0
                && Positive(Math.Max(cone.BottomRadius, cone.TopRadius)):
                return null;
            case SdfTransformNode transformed:
                if (!transformed.Transform.IsRigid())
                {
                    return "cir-display-transform-not-rigid: affine fields do not preserve the X0 distance-step bound.";
                }
                if (transformed.Transform.ToRowMajor().Any(value => !double.IsFinite(value) || Math.Abs(value) > 1_000_000))
                {
                    return "cir-display-transform-out-of-range";
                }
                return Validate(transformed.Child);
            case SdfUnionNode union:
                return Validate(union.Left) ?? Validate(union.Right);
            case SdfIntersectNode intersection:
                return Validate(intersection.Left) ?? Validate(intersection.Right);
            case SdfSubtractNode subtraction:
                return Validate(subtraction.Left) ?? Validate(subtraction.Right);
            default:
                return "cir-display-field-unqualified: unsupported node or invalid X0 primitive dimensions.";
        }
    }

    private static string EmitPrimitive(StringBuilder source, SdfTape tape, SdfTapeInstruction instruction)
    {
        int index = instruction.PayloadIndex;
        Transform3D inverse = instruction.OpCode switch
        {
            SdfTapeOpCode.EvalSphere => tape.SpherePayloads[index].InverseTransform,
            SdfTapeOpCode.EvalCylinder => tape.CylinderPayloads[index].InverseTransform,
            SdfTapeOpCode.EvalCone => tape.ConePayloads[index].InverseTransform,
            SdfTapeOpCode.EvalTorus => tape.TorusPayloads[index].InverseTransform,
            SdfTapeOpCode.EvalBox => tape.BoxPayloads[index].InverseTransform,
            _ => throw new InvalidOperationException("CIR tape operation has no qualified display lowering."),
        };
        double[] matrix = inverse.ToRowMajor();
        string prefix = $"p{instruction.DestSlot}";
        for (int axis = 0; axis < 3; axis++)
        {
            source.AppendLine($"    const {prefix}{axis}: f32 = x * {Literal(matrix[axis])} + y * {Literal(matrix[4 + axis])} + z * {Literal(matrix[8 + axis])} + {Literal(matrix[12 + axis])};");
        }
        string x = prefix + "0";
        string y = prefix + "1";
        string z = prefix + "2";
        string radial = $"Sqrt({x} * {x} + {y} * {y})";
        switch (instruction.OpCode)
        {
            case SdfTapeOpCode.EvalSphere:
                return $"Sqrt({x} * {x} + {y} * {y} + {z} * {z}) - {Literal(tape.SpherePayloads[index].Radius)}";
            case SdfTapeOpCode.EvalTorus:
                var torus = tape.TorusPayloads[index];
                source.AppendLine($"    const {prefix}r: f32 = {radial} - {Literal(torus.MajorRadius)};");
                return $"Sqrt({prefix}r * {prefix}r + {z} * {z}) - {Literal(torus.MinorRadius)}";
            case SdfTapeOpCode.EvalCylinder:
                var cylinder = tape.CylinderPayloads[index];
                source.AppendLine($"    const {prefix}r: f32 = {radial} - {Literal(cylinder.Radius)};");
                source.AppendLine($"    const {prefix}h: f32 = Abs({z}) - {Literal(cylinder.Height * 0.5)};");
                source.AppendLine($"    const {prefix}a: f32 = Max({prefix}r, 0.0);");
                source.AppendLine($"    const {prefix}b: f32 = Max({prefix}h, 0.0);");
                return $"Sqrt({prefix}a * {prefix}a + {prefix}b * {prefix}b) + Min(Max({prefix}r, {prefix}h), 0.0)";
            case SdfTapeOpCode.EvalBox:
                var box = tape.BoxPayloads[index];
                source.AppendLine($"    const {prefix}a: f32 = Abs({x}) - {Literal(box.Width * 0.5)};");
                source.AppendLine($"    const {prefix}b: f32 = Abs({y}) - {Literal(box.Height * 0.5)};");
                source.AppendLine($"    const {prefix}c: f32 = Abs({z}) - {Literal(box.Depth * 0.5)};");
                source.AppendLine($"    const {prefix}u: f32 = Max({prefix}a, 0.0);");
                source.AppendLine($"    const {prefix}v: f32 = Max({prefix}b, 0.0);");
                source.AppendLine($"    const {prefix}w: f32 = Max({prefix}c, 0.0);");
                return $"Sqrt({prefix}u * {prefix}u + {prefix}v * {prefix}v + {prefix}w * {prefix}w) + Min(Max({prefix}a, Max({prefix}b, {prefix}c)), 0.0)";
            case SdfTapeOpCode.EvalCone:
                // Same capped-frustum formula as SdfConeNode/SdfTape, expressed as straight-line typed locals.
                var cone = tape.ConePayloads[index];
                source.AppendLine($"    const {prefix}r: f32 = {radial};");
                source.AppendLine($"    var {prefix}cap: f32 = {Literal(cone.TopRadius)};");
                source.AppendLine($"    if ({z} < 0.0) {{ {prefix}cap = {Literal(cone.BottomRadius)}; }}");
                double delta = cone.TopRadius - cone.BottomRadius;
                source.AppendLine($"    const {prefix}a: f32 = {prefix}r - Min({prefix}r, {prefix}cap);");
                source.AppendLine($"    const {prefix}b: f32 = Abs({z}) - {Literal(cone.Height * 0.5)};");
                source.AppendLine($"    const {prefix}h: f32 = Clamp((({Literal(cone.TopRadius)} - {prefix}r) * {Literal(delta)} + ({Literal(cone.Height * 0.5)} - {z}) * {Literal(cone.Height)}) / {Literal(delta * delta + cone.Height * cone.Height)}, 0.0, 1.0);");
                source.AppendLine($"    const {prefix}c: f32 = {prefix}r - {Literal(cone.TopRadius)} + {Literal(delta)} * {prefix}h;");
                source.AppendLine($"    const {prefix}d: f32 = {z} - {Literal(cone.Height * 0.5)} + {Literal(cone.Height)} * {prefix}h;");
                source.AppendLine($"    var {prefix}sign: f32 = 1.0;");
                source.AppendLine($"    if ({prefix}c < 0.0 && {prefix}b < 0.0) {{ {prefix}sign = -1.0; }}");
                return $"{prefix}sign * Sqrt(Min({prefix}a * {prefix}a + {prefix}b * {prefix}b, {prefix}c * {prefix}c + {prefix}d * {prefix}d))";
            default:
                throw new InvalidOperationException("CIR tape primitive has no qualified display lowering.");
        }
    }

    private static string Literal(double value)
        => value.ToString("0.0###############################", CultureInfo.InvariantCulture);
}
