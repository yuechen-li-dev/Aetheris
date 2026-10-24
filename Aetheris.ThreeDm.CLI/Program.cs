using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetheris.CLI;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            Console.WriteLine("Usage: aetheris-3dm <inspect|recover> <file.3dm> [options]. Use '<command> --help' for options.");
            return args.Length == 0 ? 1 : 0;
        }
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());
        try
        {
            return args[0] switch
            {
                "inspect" or "inspect-3dm" => ThreeDmCli.Run(args[1..], Console.Out, Console.Error, options),
                "recover" or "recover-3dm" => ThreeDmCli.Recover(args[1..], Console.Out, Console.Error, options),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"3dm-operation-failed: {e.Message}");
            return 1;
        }
    }
    private static int Unknown(string command) { Console.Error.WriteLine($"Unknown 3DM command '{command}'."); return 1; }
}
