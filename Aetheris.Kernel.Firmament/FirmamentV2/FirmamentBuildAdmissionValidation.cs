using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Validation uses the existing engineering compiler, including selection
/// binding, capability checks, materialization and export. No output file is written.</summary>
public static class FirmamentBuildAdmissionValidation
{
    public static FirmamentV2ValidationReport Admit(
        FirmamentV2ValidationReport report, string source, string? sourceDirectory = null)
    {
        if (report.Status == "invalid") return report;
        var compilation = FirmamentBuildAndExport.CompileSource(source, sourceDirectory);
        var diagnostics = report.Diagnostics.Concat(compilation.Diagnostics.Select(diagnostic =>
            Present(diagnostic, report.Source, source))).Distinct().ToArray();
        var fatal = diagnostics.Count(diagnostic => diagnostic.Severity is "fatal" or "error");
        return report with
        {
            Status = !compilation.IsSuccess || fatal > 0 ? "invalid" : report.Status,
            ValidationLevel = compilation.IsSuccess ? "Built" : "BuildRejected",
            Diagnostics = diagnostics,
            Summary = report.Summary with
            {
                FatalDiagnosticCount = fatal,
                WarningDiagnosticCount = diagnostics.Count(diagnostic => diagnostic.Severity == "warning")
            }
        };
    }

    public static FirmamentV2ValidationDiagnostic Present(KernelDiagnostic diagnostic, string sourceFile, string? source = null)
    {
        var separator = diagnostic.Message.IndexOf(':');
        var code = separator > 0 && !diagnostic.Message[..separator].Any(char.IsWhiteSpace)
            ? diagnostic.Message[..separator]
            : diagnostic.Message.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
                ? diagnostic.Message : diagnostic.Code.ToString();
        if (source is not null)
        {
            var parsed = FirmamentV2Parser.Parse(source);
            var original = parsed.SyntaxDiagnostics.FirstOrDefault(d => d.Code == code || d.Code.StartsWith(code + ":", StringComparison.Ordinal));
            if (original is not null) return Locate(original, source, sourceFile);
            if (diagnostic.SourceSpan is { } produced && FirmamentV2Parser.Parse(source.ReplaceLineEndings("\n")).DiagnosticSource is { } input)
            {
                var origins = new Syntax.FirmamentSyntaxOrigins(Syntax.FirmamentSyntaxParser.Parse(FirmamentSourceSpelling.Normalize(source)).Document,
                    Syntax.FirmamentSyntaxParser.Parse(input).Document);
                var anchor = origins.Find(new(produced.Start, produced.Length));
                if (anchor is not null) return Locate(new(code, diagnostic.Severity == KernelDiagnosticSeverity.Error ? "fatal" : "warning", diagnostic.Message)
                    { SourceSpan = anchor, Source = diagnostic.Source, Stage = diagnostic.Source }, source, sourceFile);
            }
            if (ProfileAuthoringParser.LocateDiagnostic(source, diagnostic.Message) is { } profileDiagnostic)
                return Locate(profileDiagnostic, source, sourceFile);
        }
        if (source is not null && code.StartsWith("firmament-syntax-", StringComparison.Ordinal))
        {
            var candidates = Syntax.FirmamentSyntaxParser.Parse(source, sourceFile).Diagnostics.Where(d => d.Code == code).ToArray();
            if (candidates.Length == 1) return candidates[0] with { Source = diagnostic.Source, Stage = "Syntax" };
        }
        return Locate(new(diagnostic.Message, diagnostic.Severity switch
        {
            KernelDiagnosticSeverity.Error => "fatal",
            KernelDiagnosticSeverity.Warning => "warning",
            _ => "info"
        }, diagnostic.Message)
        {
            SourceFile = sourceFile,
            Stage = diagnostic.Source,
            Source = diagnostic.Source,
            LocationNote = "The owning build stage did not provide an original-source span."
        }, source, sourceFile) with { Code = code, Message = FirmamentInterop.FirmamentV2DiagnosticAdapter.MessageFor(diagnostic.Message) };
    }

    public static FirmamentV2ValidationDiagnostic Locate(
        FirmamentV2ValidationDiagnostic diagnostic, string? source, string sourceFile)
    {
        if (diagnostic.SourceSpan is { } span)
        {
            if (source is null) return diagnostic with { SourceFile = sourceFile };
            var sourceLine = 1; var sourceColumn = 1;
            for (var index = 0; index < Math.Min(span.Start, source.Length); index++)
                if (source[index] == '\n') { sourceLine++; sourceColumn = 1; } else sourceColumn++;
            return diagnostic with { SourceFile = sourceFile, Line = sourceLine, Column = sourceColumn };
        }
        if (source is null) return diagnostic with { SourceFile = sourceFile, LocationNote = "Original source is unavailable." };
        var tokens = FirmamentLanguageAnalysisService.Lex(source).Where(token => token.Shape != "comment").ToArray();
        // Only these parser-owned diagnostics identify a specific original token.
        // All other legacy producers receive a disclosed enclosing declaration span.
        var parts = diagnostic.Code.Split(':');
        string? subject = parts[0] is FirmamentV2Parser.CanonicalFieldUnknown or FirmamentV2Parser.CanonicalDeclarationUnknown
            && parts.Length > 1 ? parts[^1] : null;
        var matches = subject is null ? [] : tokens.Where(token => token.Shape == "identifier" && token.Text == subject).ToArray();
        var exact = matches.Length == 1;
        var token = exact ? matches[0] : tokens.FirstOrDefault(token => token.Shape == "identifier"
            && token.Text is "Model" or "model" or "Struct" or "Concept" or "Profile" or "Template" or "Assembly" or "Scene");
        if (token is null) return diagnostic with { SourceFile = sourceFile, LocationNote = "The producer supplied no original-source location." };
        var line = 1; var column = 1;
        for (var index = 0; index < token.Start; index++)
            if (source[index] == '\n') { line++; column = 1; } else column++;
        return diagnostic with
        {
            SourceFile = sourceFile, SourceSpan = new(token.Start, token.Length), Line = line, Column = column,
            LocationNote = exact ? null : "Document declaration anchor; the producer supplied no precise original-source span."
        };
    }
}
