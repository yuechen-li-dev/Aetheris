namespace Aetheris.CLI;

internal static class CommandHelp
{
    internal static void Write(TextWriter output, string usage, string description, string example)
    {
        output.WriteLine(description);
        output.WriteLine();
        output.WriteLine(usage.StartsWith("Usage:", StringComparison.Ordinal) ? usage : "Usage: " + usage);
        output.WriteLine();
        output.WriteLine("Options are listed in the usage above. -h, --help shows this help.");
        output.WriteLine("Example: " + example);
    }
}
