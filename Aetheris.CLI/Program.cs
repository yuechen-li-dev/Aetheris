namespace Aetheris.CLI;

public static class Program
{
    public static int Main(string[] args) => CliRunner.Run(args, Console.Out, Console.Error);
}
