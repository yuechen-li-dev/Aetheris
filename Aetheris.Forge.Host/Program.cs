using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Aetheris.Forge.Host;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.In, Console.Out);

    internal static int Run(string[] args, TextReader input, TextWriter output)
    {
        var host = new ForgeProtocolHost();
        try
        {
            if (args.Length == 1 && args[0] == "info")
                return Write(output, host.GetHostInfo(), ForgeProtocolJsonContext.Default.ForgeHostInfo);
            if (args.Length == 1 && args[0] == "list")
                return Write(output, host.ListTemplates(), ForgeProtocolJsonContext.Default.ForgeTemplateListResponse);
            if (args.Length == 2 && args[0] == "describe")
            {
                var description = host.DescribeTemplate(args[1]);
                if (description is not null)
                    return Write(output, description, ForgeProtocolJsonContext.Default.ForgeTemplateDescription);
                return WriteError(output, "forge-host-template-not-found", $"Public template '{args[1]}' was not found.", 3);
            }
            if (args.Length >= 2 && args[0] == "invoke")
            {
                var requestPath = Option(args, "--request");
                var outputDirectory = Option(args, "--out");
                if (outputDirectory is null)
                    return WriteError(output, "forge-host-output-directory-required", "Invoke requires --out <directory>.", 2);
                var json = requestPath is null || requestPath == "-" ? input.ReadToEnd() : File.ReadAllText(requestPath);
                var request = JsonSerializer.Deserialize(json, ForgeProtocolJsonContext.Default.ForgeTemplateInvocationRequest);
                if (request is null)
                    return WriteError(output, "forge-host-request-invalid", "Invocation request was empty.", 2);
                var result = host.InvokeTemplate(args[1], request, outputDirectory);
                Write(output, result, ForgeProtocolJsonContext.Default.ForgeTemplateInvocationResult);
                return result.Success ? 0 : 4;
            }
            return WriteError(output, "forge-host-command-invalid",
                "Usage: forge-host info | list | describe <template-id> | invoke <template-id> [--request <json|->] --out <directory>", 2);
        }
        catch (JsonException exception)
        {
            return WriteError(output, "forge-host-request-json-invalid", exception.Message, 2);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return WriteError(output, "forge-host-request-read-failed", exception.Message, 2);
        }
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int Write<T>(TextWriter output, T value, JsonTypeInfo<T> typeInfo)
    {
        output.WriteLine(JsonSerializer.Serialize(value, typeInfo));
        return 0;
    }

    private static int WriteError(TextWriter output, string code, string message, int exitCode)
    {
        var response = new ForgeProtocolErrorResponse(ForgeHostProtocol.Version,
            [new(code, ForgeProtocolDiagnosticSeverity.Error, message)]);
        Write(output, response, ForgeProtocolJsonContext.Default.ForgeProtocolErrorResponse);
        return exitCode;
    }
}
