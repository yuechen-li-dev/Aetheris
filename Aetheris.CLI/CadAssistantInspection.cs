using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Aetheris.CLI;

public enum CadAssistantInspectionStatus
{
    Unavailable,
    LaunchFailed,
    ImportFailed,
    TimedOut,
    Displayed,
    DisplayedWithWarnings,
    InspectionCompleted,
}

public sealed record CadAssistantInspectionOptions(string? ExecutablePath, TimeSpan Timeout, string EvidenceDirectory);

public sealed record CadAssistantInspectionResult(
    CadAssistantInspectionStatus Status,
    string? ResolvedExecutablePath,
    string ArtifactPath,
    string ArtifactSha256,
    IReadOnlyList<string> LaunchArguments,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int? ImportProgress,
    string? DisplayStatus,
    bool WindowResponsive,
    long StableForMilliseconds,
    IReadOnlyList<string> Screenshots,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Bounded external-display harness. CAD Assistant has no stable public import
/// completion API, so the harness records the raw process/window observations
/// and refuses to infer visual geometry correctness from them.
/// </summary>
public static class CadAssistantInspection
{
    public static CadAssistantInspectionResult Inspect(string stepPath, CadAssistantInspectionOptions options)
    {
        var artifactPath = Path.GetFullPath(stepPath);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(artifactPath))).ToLowerInvariant();
        var started = DateTimeOffset.UtcNow;
        var executable = ResolveExecutable(options.ExecutablePath);
        if (executable is null)
            return Result(CadAssistantInspectionStatus.Unavailable, null, artifactPath, hash, started, null, false, 0, [], ["CAD Assistant was not found. Set --cad-assistant-path or AETHERIS_CAD_ASSISTANT_PATH."]);

        Directory.CreateDirectory(options.EvidenceDirectory);
        using var process = new Process { StartInfo = new ProcessStartInfo(executable, Quote(artifactPath)) { UseShellExecute = true } };
        try
        {
            if (!process.Start())
                return Result(CadAssistantInspectionStatus.LaunchFailed, executable, artifactPath, hash, started, null, false, 0, [], ["Process.Start returned false."]);
        }
        catch (Exception ex)
        {
            return Result(CadAssistantInspectionStatus.LaunchFailed, executable, artifactPath, hash, started, null, false, 0, [], [ex.Message]);
        }

        var deadline = DateTimeOffset.UtcNow + options.Timeout;
        var stableSince = DateTimeOffset.UtcNow;
        IntPtr lastWindow = IntPtr.Zero;
        while (DateTimeOffset.UtcNow < deadline)
        {
            process.Refresh();
            if (process.HasExited)
                return Result(CadAssistantInspectionStatus.ImportFailed, executable, artifactPath, hash, started, process, false, 0, [], [$"CAD Assistant exited with code {process.ExitCode} before a display-ready window was observed."]);
            var handle = process.MainWindowHandle;
            if (handle != IntPtr.Zero && process.Responding)
            {
                if (handle != lastWindow) { lastWindow = handle; stableSince = DateTimeOffset.UtcNow; }
                var stable = DateTimeOffset.UtcNow - stableSince;
                if (stable >= TimeSpan.FromSeconds(2))
                {
                    var screenshots = CaptureEvidence(handle, options.EvidenceDirectory);
                    var notes = screenshots.Count == 2
                        ? new[] { "A responsive CAD Assistant window remained stable for two seconds. Import-progress and display-status controls are not exposed through a stable automation API, so this is a display observation with warnings, not a visual geometry pass." }
                        : new[] { "A responsive CAD Assistant window was observed, but native screenshot capture failed. This is not a clean display admission." };
                    TryCloseOwnedProcess(process);
                    return Result(screenshots.Count == 2 ? CadAssistantInspectionStatus.DisplayedWithWarnings : CadAssistantInspectionStatus.DisplayedWithWarnings, executable, artifactPath, hash, started, process, true, (long)stable.TotalMilliseconds, screenshots, notes);
                }
            }
            Thread.Sleep(100);
        }
        TryCloseOwnedProcess(process);
        return Result(CadAssistantInspectionStatus.TimedOut, executable, artifactPath, hash, started, process, false, 0, [], $"No stable responsive CAD Assistant window was observed within {options.Timeout}.");
    }

    public static string? ResolveExecutable(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return File.Exists(explicitPath) ? explicitPath : null;
        }
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("AETHERIS_CAD_ASSISTANT_PATH"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CAD Assistant", "CAD Assistant.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "CAD Assistant", "CAD Assistant.exe"),
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private static CadAssistantInspectionResult Result(CadAssistantInspectionStatus status, string? executable, string artifact, string hash, DateTimeOffset started, Process? process, bool responsive, long stable, IReadOnlyList<string> screenshots, params string[] diagnostics)
        => new(status, executable, artifact, hash, executable is null ? [] : [artifact], started, DateTimeOffset.UtcNow, null, null, responsive, stable, screenshots, diagnostics);

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static void TryCloseOwnedProcess(Process process)
    {
        try { if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero) process.CloseMainWindow(); }
        catch { /* best-effort only; never touch a process the harness did not start. */ }
    }

    private static IReadOnlyList<string> CaptureEvidence(IntPtr window, string evidenceDirectory)
    {
        // Native capture avoids taking a dependency on System.Drawing in the cross-platform CLI.
        // A second capture is a bounded secondary observation; no input is synthesized because
        // CAD Assistant does not offer a stable public camera automation contract.
        var first = Path.Combine(evidenceDirectory, "cad-assistant-isometric.png");
        var second = Path.Combine(evidenceDirectory, "cad-assistant-secondary.png");
        return CaptureWindowPng(window, first) && CaptureWindowPng(window, second) ? [first, second] : [];
    }

    private static bool CaptureWindowPng(IntPtr window, string destination)
    {
        if (!OperatingSystem.IsWindows() || !GetWindowRect(window, out var rect)) return false;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) return false;
        var source = GetWindowDC(window);
        var memory = CreateCompatibleDC(source);
        var bitmap = CreateCompatibleBitmap(source, width, height);
        var previous = IntPtr.Zero;
        try
        {
            if (source == IntPtr.Zero || memory == IntPtr.Zero || bitmap == IntPtr.Zero) return false;
            previous = SelectObject(memory, bitmap);
            if (!BitBlt(memory, 0, 0, width, height, source, 0, 0, 0x00CC0020)) return false;
            var info = new BitmapInfo { Header = new BitmapInfoHeader { Size = Marshal.SizeOf<BitmapInfoHeader>(), Width = width, Height = -height, Planes = 1, BitCount = 32, Compression = 0 } };
            var stride = width * 4;
            var pixels = new byte[stride * height];
            if (GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref info, 0) == 0) return false;
            WritePng(destination, width, height, pixels, stride);
            return true;
        }
        catch { return false; }
        finally
        {
            if (previous != IntPtr.Zero) SelectObject(memory, previous);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (memory != IntPtr.Zero) DeleteDC(memory);
            if (source != IntPtr.Zero) ReleaseDC(window, source);
        }
    }

    private static void WritePng(string path, int width, int height, byte[] bgra, int stride)
    {
        using var file = File.Create(path);
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(file, "IHDR", BuildHeader(width, height));
        var scanlines = new byte[height * ((width * 4) + 1)];
        for (var y = 0; y < height; y++)
        {
            var target = y * ((width * 4) + 1) + 1;
            for (var x = 0; x < width; x++)
            {
                var source = y * stride + x * 4;
                scanlines[target + x * 4] = bgra[source + 2];
                scanlines[target + x * 4 + 1] = bgra[source + 1];
                scanlines[target + x * 4 + 2] = bgra[source];
                scanlines[target + x * 4 + 3] = bgra[source + 3];
            }
        }
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) zlib.Write(scanlines);
        WriteChunk(file, "IDAT", compressed.ToArray());
        WriteChunk(file, "IEND", []);
    }

    private static byte[] BuildHeader(int width, int height)
    {
        var data = new byte[13];
        WriteBigEndian(data, 0, width); WriteBigEndian(data, 4, height);
        data[8] = 8; data[9] = 6;
        return data;
    }

    private static void WriteChunk(Stream stream, string name, byte[] data)
    {
        var type = System.Text.Encoding.ASCII.GetBytes(name);
        var length = new byte[4]; WriteBigEndian(length, 0, data.Length); stream.Write(length); stream.Write(type); stream.Write(data);
        var checksumInput = type.Concat(data).ToArray();
        var crc = new byte[4]; WriteBigEndian(crc, 0, unchecked((int)Crc32(checksumInput))); stream.Write(crc);
    }

    private static void WriteBigEndian(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24); target[offset + 1] = (byte)(value >> 16); target[offset + 2] = (byte)(value >> 8); target[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xffffffffu;
        foreach (var b in bytes) { crc ^= b; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u); }
        return ~crc;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfoHeader { public int Size, Width, Height; public short Planes, BitCount; public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ClrUsed, ClrImportant; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public BitmapInfoHeader Header; public uint Colors; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hDc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hDc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint rasterOperation);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hDc, IntPtr bitmap, uint startScan, uint scanLines, byte[] bits, ref BitmapInfo info, uint usage);
}
