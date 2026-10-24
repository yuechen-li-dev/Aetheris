using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;

namespace Aetheris.Drawing;

public interface IDrawingPdfRasterizer
{
    string Version { get; }
    void Render(string pdfPath, DrawingPage page, string outputPng, int dpi, DrawingBounds? bounds);
}

/// <summary>An opt-in external executable, never installed or bundled by Aetheris.</summary>
public sealed class PopplerPdfRasterizer : IDrawingPdfRasterizer
{
    private readonly string executable;
    public string Version { get; }

    public PopplerPdfRasterizer(string executablePath)
    {
        executable = Path.GetFullPath(executablePath);
        if (!File.Exists(executable)) throw new DrawingNotesException("drawing-renderer-missing", $"pdftoppm executable was not found: {executable}");
        var version = Run(["-v"]).Trim().Split('\n')[0].Trim();
        if (!version.StartsWith("pdftoppm version ", StringComparison.Ordinal))
            throw new DrawingNotesException("drawing-renderer-invalid", "The selected executable must be Poppler pdftoppm.");
        Version = version;
    }

    public void Render(string pdfPath, DrawingPage page, string outputPng, int dpi, DrawingBounds? bounds)
    {
        // Explicit pixel dimensions preserve physical UserUnit and rotated CropBox
        // semantics even on Poppler versions with different default page sizing.
        var width = checked((int)Math.Ceiling(page.WidthPoints * dpi / 72));
        var height = checked((int)Math.Ceiling(page.HeightPoints * dpi / 72));
        var temporary = Path.Combine(Path.GetTempPath(), $"aetheris-pdf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            var prefix = Path.Combine(temporary, "page");
            // pdftoppm scales before rotating. Crop coordinates apply afterwards.
            var swapped = page.RotationDegrees is 90 or 270;
            var args = new List<string> { "-png", "-singlefile", "-f", N(page.Number), "-l", N(page.Number), "-cropbox", "-scale-to-x", N(swapped ? height : width), "-scale-to-y", N(swapped ? width : height) };
            if (bounds is not null)
            {
                var x = (int)Math.Floor(bounds.X * dpi / 72);
                var y = (int)Math.Floor(bounds.Y * dpi / 72);
                width = (int)Math.Ceiling(bounds.Right * dpi / 72) - x;
                height = (int)Math.Ceiling(bounds.Bottom * dpi / 72) - y;
                args.AddRange(["-x", N(x), "-y", N(y), "-W", N(width), "-H", N(height)]);
            }
            args.Add(Path.GetFullPath(pdfPath));
            args.Add(prefix);
            Run(args);
            var bytes = File.ReadAllBytes(prefix + ".png");
            if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) != width || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) != height)
                throw new DrawingNotesException("drawing-render-output-invalid", "Renderer did not return a PNG with the requested page/crop dimensions.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPng))!);
            File.WriteAllBytes(outputPng, bytes);
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    private string Run(IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new DrawingNotesException("drawing-renderer-start-failed", "Could not start pdftoppm.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new DrawingNotesException("drawing-renderer-timeout", "pdftoppm exceeded the 60 second render limit.");
        }
        var output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new DrawingNotesException("drawing-renderer-failed", $"pdftoppm exited {process.ExitCode}: {output.Trim()}");
        return output;
    }
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
