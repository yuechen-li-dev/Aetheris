using System.Globalization;
using System.Text;
using Aetheris.Kernel.Core.Mechanisms;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.EditableV8;

/// <summary>Authored Firmament modules plus finite, typed mechanism input data.
/// No geometry or product-tree source is synthesized by the demo.</summary>
public sealed record EngineProject(string EntryPath, IReadOnlyDictionary<string, string> Documents,
    IReadOnlyDictionary<string, string> OccurrencePaths)
{
    private static string N(double value)
    {
        var text = value.ToString("G17", CultureInfo.InvariantCulture);
        // Keep round-trip precision without exponent/unit ambiguity in legacy
        // Record literal consumers. This changes spelling, not numeric values.
        return text.Contains('E') ? value.ToString("F" + Math.Max(0, 16 - (int)Math.Floor(Math.Log10(Math.Abs(value)))), CultureInfo.InvariantCulture) : text;
    }
    private static string Export(string group) => string.Concat(group.Split('-').Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
    internal static string Group(EngineBinding b) => b.Cylinder > 0 ? "cylinder" + b.Cylinder
        : b.Kind == "Crank" ? "crankshaft" : b.Category == "Block" ? "crankcase"
        : b.Rest[12] > 0 ? "cam-right" : "cam-left";

    public FirmamentProjectSnapshot Snapshot => new(EntryPath, Documents);

    public string WriteTo(string directory, string label)
    {
        var root = Path.Combine(directory, label);
        foreach (var (path, source) in Documents) {
            var target = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, source);
        }
        return Path.Combine(root, EntryPath);
    }

    internal static EngineProject Create(V8Spec spec, IReadOnlyList<EngineBinding> bindings,
        IReadOnlyList<EngineCylinder> cylinders, string stage)
    {
        var assets = typeof(EngineProject).Assembly;
        var documents = assets.GetManifestResourceNames().Where(n => n.StartsWith("EditableV8/", StringComparison.Ordinal))
            .ToDictionary(n => n[11..].Replace('\\', '/'), n => {
                using var reader = new StreamReader(assets.GetManifestResourceStream(n)!);
                return reader.ReadToEnd();
            }, StringComparer.Ordinal);
        var dimensions = new Dictionary<string, double>(StringComparer.Ordinal) {
            ["crankRadius"] = spec.CrankRadius, ["cylinderSpacing"] = spec.CylinderSpacing, ["crankcaseFloor"] = spec.CrankcaseFloor, ["camHeight"] = spec.DeckHeight + 80,
            ["deckHeight"] = spec.DeckHeight, ["sleeveSeatHeight"] = spec.DeckHeight - (spec.Stroke + 29) / 2,
            ["rodLength"] = spec.RodLength, ["rodHeight"] = spec.RodLength + 48, ["rodCenter"] = spec.RodLength / 2,
            ["pistonRadius"] = spec.Bore / 2 - .3, ["skirtInnerRadius"] = spec.Bore / 2 - 4.3,
            ["sleeveOuterRadius"] = spec.Bore / 2 + 6, ["boreRadius"] = spec.Bore / 2,
            ["sleeveMin"] = -(spec.Stroke + 29) / 2, ["sleeveMax"] = (spec.Stroke + 29) / 2,
            ["webHeight"] = spec.CrankRadius + 60, ["webCenter"] = spec.CrankRadius / 2,
            ["journalMin"] = -(spec.CylinderSpacing - 56) / 2, ["journalMax"] = (spec.CylinderSpacing - 56) / 2,
            ["headWidth"] = spec.Bore + 12, ["compressedPitch"] = (36 - spec.ValveLift) / 6,
            ["supportHeight"] = 36 - spec.CrankcaseFloor, ["supportCenter"] = (36 + spec.CrankcaseFloor) / 2,
            ["caseMin"] = -(4 * spec.CylinderSpacing + 16) / 2, ["caseMax"] = (4 * spec.CylinderSpacing + 16) / 2
        };
        documents["design.firmament"] = "// Millimetre dimensions from the same typed specification as the mechanism.\nRecord EngineDimensions {\n"
            + string.Concat(dimensions.Keys.Select(k => $"  {k}: Length\n")) + "}\nStatic d: EngineDimensions = EngineDimensions {\n"
            + string.Concat(dimensions.Select(v => $"  {v.Key}: {N(v.Value)}mm\n")) + "}\n"
            + "Record SpringDimensions { pitch: Length }\nStatic freeCoil: SpringDimensions = SpringDimensions { pitch: 6mm }\n"
            + $"Static compressedCoil: SpringDimensions = SpringDimensions {{ pitch: {N((36 - spec.ValveLift) / 6)}mm }}\n";
        var stageWitness = stage is "stage-a" or "stage-b";
        var rootName = stageWitness ? "SliderCrankStageA" : "EditableV8";
        var shift = -bindings.Single(b => b.Name == "MainJournal1").Rest[14];
        if (!stageWitness) {
            var rest = new StringBuilder("// Indexed scalar rest data; no Part transforms.\n");
            var configurations = new List<string>();
            foreach (var cylinder in cylinders) {
                var bankSin = double.Sin(cylinder.BankRadians); var bankCos = double.Cos(cylinder.BankRadians);
                var rod = bindings.Single(b => b.Name == "Rod" + cylinder.Number);
                var piston = bindings.Single(b => b.Name == "Piston" + cylinder.Number);
                var intake = PrescribedMotion.ValveLift(-cylinder.IgnitionDegrees, spec.IntakeOpeningDegrees, spec.ValveDurationDegrees, spec.ValveLift);
                var exhaust = PrescribedMotion.ValveLift(-cylinder.IgnitionDegrees, spec.ExhaustOpeningDegrees, spec.ValveDurationDegrees, spec.ValveLift);
                var values = new Dictionary<string,string> {
                    ["rodX"] = N(rod.Rest[12] * bankCos - rod.Rest[13] * bankSin) + "mm",
                    ["rodY"] = N(rod.Rest[12] * bankSin + rod.Rest[13] * bankCos) + "mm",
                    ["rodSin"] = N(-rod.Rest[1] * bankCos - rod.Rest[0] * bankSin),
                    ["rodCos"] = N(rod.Rest[0] * bankCos - rod.Rest[1] * bankSin),
                    ["pistonY"] = N(piston.Rest[12] * bankSin + piston.Rest[13] * bankCos - 32.5) + "mm",
                    ["intakeLift"] = N(intake) + "mm", ["exhaustLift"] = N(exhaust) + "mm",
                    ["intakePitch"] = N((36 - intake) / 6) + "mm", ["exhaustPitch"] = N((36 - exhaust) / 6) + "mm"
                };
                rest.AppendLine($"Static restC{cylinder.Number}: CylinderRest = standardRest with {{ " + string.Join("; ", values.Select(v => v.Key + ": " + v.Value)) + " }");
                configurations.Add($"  C{cylinder.Number} => standardCylinder with {{ bankSin: {N(bankSin)}; bankCos: {N(bankCos)}; z: {N(cylinder.Z + shift)}mm; rest: restC{cylinder.Number}; journal: EditableV8.Crankshaft.Pin{(cylinder.Number + 1) / 2} }}");
            }
            rest.AppendLine("Static cylinderConfigs: Set<CylinderConfig> {\n" + string.Join("\n", configurations) + "\n}"); documents[cylinders.Count == 4 ? "bank-rest-configurations.firmament" : "rest-configurations.firmament"] = rest.ToString();
        }
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in bindings.GroupBy(b => stageWitness ? stage : Group(b))) {
            if (!stageWitness && group.First().Cylinder > 0) {
                foreach (var binding in group)
                    paths[binding.Name] = rootName + ".Cylinders.C" + binding.Cylinder + ".Cylinder."
                        + binding.Name[..^binding.Cylinder.ToString(CultureInfo.InvariantCulture).Length];
                continue;
            }
            foreach (var binding in group) paths[binding.Name] = rootName + "." + Export(group.Key) + "." + binding.Name;
        }
        var entry = stageWitness ? stage + ".firmasm" : cylinders.Count == 4 ? "bank.firmasm" : "engine.firmasm";
        return new(entry, documents, paths);
    }

}
