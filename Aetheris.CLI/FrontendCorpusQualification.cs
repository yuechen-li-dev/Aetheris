using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.FEA.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Garment;
using Aetheris.Kernel.Firmament.Scene;
using Aetheris.Kernel.Firmament;

namespace Aetheris.CLI;

/// <summary>Developer corpus evidence. Invokes existing production owners; never
/// grants admission, rewrites source, or equates syntax success with validity.</summary>
internal static class FrontendCorpusQualification
{
    internal sealed record Entry(string Source, string Sha256, bool StructuralSuccess, string SyntaxFingerprint,
        IReadOnlyList<FirmamentV2ValidationDiagnostic> SyntaxDiagnostics, string Owner, string ProductionLevel,
        int ProductionExit, string Classification, string Reason, string ProductionOutput, string ProductionErrors);

    internal static int Run(string input, string output, TextWriter stdout)
    {
        var root = Path.GetFullPath(input); output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(p => new[] { ".firmament", ".firmfixture", ".firmasm" }.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal).ToArray();
        var entries = new List<Entry>();
        var compactJson = new JsonSerializerOptions(CliRunner.JsonOptions) { WriteIndented = false };
        // Persist each row immediately so an interrupted expensive domain compile
        // retains source evidence. Serial execution bounds geometry/cache pressure.
        using var journal = new StreamWriter(Path.Combine(output, "entries.jsonl"), false, new UTF8Encoding(false)) { AutoFlush = true };
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var syntax = FirmamentSyntaxParser.Parse(source, file);
            var declarations = FirmamentSyntaxEvidence.Declarations(syntax.Document).ToArray();
            var tokens = syntax.Document.Tokens.Where(t => t.Kind is not (SyntaxTokenKind.Whitespace or SyntaxTokenKind.Comment or SyntaxTokenKind.End)).ToArray();
            var legacy = tokens.FirstOrDefault()?.Text == "{" || tokens.Length >= 2
                && tokens[0].Text is "model" or "firmament" && tokens[1].Text == ":";
            var directory = Path.Combine(output, "products", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relative)))[..16]);
            var captured = new StringWriter(); var errors = new StringWriter();
            string owner; string level; string[] args;
            if (SceneAuthoring.HasRoot(source)) { owner = "Scene"; level = "SceneCompiled"; args = ["scene", "inspect", file, "--json"]; }
            else if (GarmentAuthoring.HasRoot(source)) { owner = "Garment"; level = "GarmentCompiled"; args = ["garment", "inspect", file, "--json"]; }
            else if (Path.GetExtension(file).Equals(".firmasm", StringComparison.OrdinalIgnoreCase) || FirmamentLanguageAnalysisService.HasAssemblyRoot(source))
            { owner = "Assembly"; level = "AssemblyInspected"; args = ["asm", "inspect", file, "--json"]; }
            else if (declarations.Any(d => d.Type.Name.Text == "Drawing" && d.Modifiers.All(m => m.Name.Text != "Concept")))
            { owner = "Drawing"; level = "DrawingCompiled"; args = ["drawing", "compile", file, "--out-dir", directory, "--json"]; }
            else if (declarations.Any(d => d.Type.Name.Text == "Analysis" || d.Modifiers.Any(m => m.Name.Text == "Analysis")))
            { owner = "Analysis"; level = "AnalysisCompiledNotSolved"; args = []; }
            else if (GearAuthoring.IsGearSource(source))
            { owner = "Gear"; level = "GearInterfacesInspected"; args = ["inspect", file, "--json"]; }
            else { owner = "ValidateDomainDispatch"; level = "OwnerReported"; args = ["validate", file, "--json"]; }
            int exit;
            try
            {
                if (owner == "Analysis")
                {
                    var result = FirmamentAnalysisCompiler.Compile(source, file, Path.GetDirectoryName(file));
                    exit = result.IsSuccess ? 0 : 1;
                    captured.Write(JsonSerializer.Serialize(new { result.IsSuccess, result.Diagnostics }, CliRunner.JsonOptions));
                }
                else exit = CliRunner.Run(args, captured, errors);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            { exit = 2; errors.Write(exception.ToString()); }
            var negative = relative.Split('/').Any(p => p.Equals("Invalid", StringComparison.OrdinalIgnoreCase))
                || Path.GetFileNameWithoutExtension(relative).EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);
            var speculative = relative.StartsWith("Speculative/", StringComparison.Ordinal) || relative.StartsWith("Experiments/", StringComparison.Ordinal);
            var module = !legacy && syntax.Document.Statements.Count > 0 && syntax.Document.Statements.All(s => s is DirectiveSyntax
                || s is DeclarationSyntax d && (d.Modifiers.Any(m => m.Name.Text is "Template" or "Concept")
                    || d.Type.Name.Text is "Concept" or "Record" or "Enum" or "Static" or "Template" or "Subassembly"));
            var (classification, reason) = exit == 2 ? ("OwnerException", "Production owner threw; not classified as an ordinary rejection.")
                : legacy ? ("LegacyGrammarBoundary", "Serialized/legacy leading-token grammar; structural result recorded separately from its production owner.")
                : negative ? (exit == 0 ? "NegativeFixtureAccepted" : "NegativeFixtureRejected", "Negative fixture intent is explicit in its path; acceptance requires review rather than being silently counted as success.")
                : speculative ? ("ExperimentalSource", "Experimental/speculative authoring is measured but is not a canonical compatibility promise.")
                : !syntax.IsSuccess && exit == 0 ? ("StructuralGap", "Production owner accepts syntax the shared structural parser rejects.")
                : !syntax.IsSuccess ? ("BothRejected", "Both owners reject; retain both diagnostics for malformed-source or unsupported-syntax review.")
                : module && exit != 0 ? ("DeclarationModule", "Structurally valid declarations require a consuming document; standalone owner outcome is retained.")
                : exit == 0 ? ("Admitted", "Structural parse and domain production owner both succeed at the recorded production level.")
                : ("SemanticOrCapabilityRejection", "Structural syntax succeeds; production rejects binding, capabilities, geometry or domain admission. Inspect retained diagnostics.");
            var entry = new Entry(relative, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))), syntax.IsSuccess,
                FirmamentSyntaxEvidence.Fingerprint(syntax.Document), syntax.Diagnostics, owner, level, exit, classification, reason, captured.ToString(), errors.ToString());
            entries.Add(entry); journal.WriteLine(JsonSerializer.Serialize(entry, compactJson));
            stdout.WriteLine($"{entries.Count}/{files.Length} {classification} {relative}"); stdout.Flush();
            if (entries.Count % 32 == 0) GC.Collect();
        }
        var summary = new { schemaVersion = 1, sourceCount = entries.Count, structuralParsed = entries.Count(e => e.StructuralSuccess),
            structuralRejected = entries.Count(e => !e.StructuralSuccess), productionAccepted = entries.Count(e => e.ProductionExit == 0),
            universalSemanticParityQualified = false,
            classifications = entries.GroupBy(e => e.Classification).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()), entries };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(summary, CliRunner.JsonOptions));
        return 0; // completed collection, never an engineering-admission claim
    }
}
