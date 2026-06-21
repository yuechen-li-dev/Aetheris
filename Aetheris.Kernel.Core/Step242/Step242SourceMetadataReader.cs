using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Core.Step242;

public static class Step242SourceMetadataReader
{
    public static Step242SourceMetadata Read(string stepText)
    {
        if (string.IsNullOrWhiteSpace(stepText)) return Step242SourceMetadata.Empty;

        var fileName = MatchFileName(stepText);
        var description = MatchFileDescription(stepText);
        var product = MatchProduct(stepText);

        return new Step242SourceMetadata(
            fileName?.FileName,
            description,
            fileName?.Author,
            fileName?.Organization,
            fileName?.CreationTimestamp,
            fileName?.OriginatingSystem,
            fileName?.Authorization,
            product?.Name,
            product?.Description);
    }

    private static FileNameMetadata? MatchFileName(string text)
    {
        var match = Regex.Match(text, @"FILE_NAME\s*\(\s*'(?<name>(?:''|[^'])*)'\s*,\s*'(?<time>(?:''|[^'])*)'\s*,\s*\((?<authors>[^)]*)\)\s*,\s*\((?<orgs>[^)]*)\)\s*,\s*'(?<preprocessor>(?:''|[^'])*)'\s*,\s*'(?<originating>(?:''|[^'])*)'\s*,\s*'(?<authorization>(?:''|[^'])*)'", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success) return null;
        return new FileNameMetadata(
            Unescape(match.Groups["name"].Value),
            FirstString(match.Groups["authors"].Value),
            FirstString(match.Groups["orgs"].Value),
            Unescape(match.Groups["time"].Value),
            Unescape(match.Groups["originating"].Value),
            Unescape(match.Groups["authorization"].Value));
    }

    private static string? MatchFileDescription(string text)
    {
        var match = Regex.Match(text, @"FILE_DESCRIPTION\s*\(\s*\((?<items>[^)]*)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? FirstString(match.Groups["items"].Value) : null;
    }

    private static ProductMetadata? MatchProduct(string text)
    {
        var match = Regex.Match(text, @"PRODUCT\s*\(\s*'(?<id>(?:''|[^'])*)'\s*,\s*'(?<name>(?:''|[^'])*)'\s*,\s*'(?<description>(?:''|[^'])*)'", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success) return null;
        return new ProductMetadata(Unescape(match.Groups["name"].Value), Unescape(match.Groups["description"].Value));
    }

    private static string? FirstString(string listText)
    {
        var match = Regex.Match(listText, @"'(?<value>(?:''|[^'])*)'", RegexOptions.Singleline);
        return match.Success ? Unescape(match.Groups["value"].Value) : null;
    }

    private static string Unescape(string value) => value.Replace("''", "'", StringComparison.Ordinal);

    private sealed record FileNameMetadata(string? FileName, string? Author, string? Organization, string? CreationTimestamp, string? OriginatingSystem, string? Authorization);
    private sealed record ProductMetadata(string? Name, string? Description);
}
