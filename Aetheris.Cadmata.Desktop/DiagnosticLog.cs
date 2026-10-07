using Microsoft.Extensions.Logging;

namespace Aetheris.Cadmata.Desktop;

internal sealed class DiagnosticLog : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();
    internal string DataRoot { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aetheris", "Cadmata");
    internal string Path { get; }

    internal DiagnosticLog()
    {
        var logs = System.IO.Path.Combine(DataRoot, "logs");
        Directory.CreateDirectory(logs);
        Path = System.IO.Path.Combine(logs, $"cadmata-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
        _writer = new StreamWriter(Path) { AutoFlush = true };
        // Keep diagnostic history bounded without touching active instances.
        foreach (var old in new DirectoryInfo(logs).GetFiles("*.log").OrderByDescending(f => f.LastWriteTimeUtc).Skip(20))
            try { old.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        var sessions = System.IO.Path.Combine(DataRoot, "sessions");
        if (Directory.Exists(sessions))
            foreach (var old in new DirectoryInfo(sessions).GetDirectories().Where(d => d.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-1)))
                try { old.Delete(true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    internal void Write(string message)
    {
        lock (_gate) _writer.WriteLine($"{DateTime.UtcNow:O} {message}");
    }
    internal ILoggerProvider Provider => new ProviderAdapter(this);
    public void Dispose() { lock (_gate) _writer.Dispose(); }

    private sealed class ProviderAdapter(DiagnosticLog owner) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new FileLogger(owner, categoryName);
        public void Dispose() { } // The desktop lifetime owns the writer.
    }

    private sealed class FileLogger(DiagnosticLog owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level)) owner.Write($"{level} {category} {formatter(state, exception)} {exception}");
        }
    }
}
