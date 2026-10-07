using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Aetheris.Server.Startup;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Aetheris.Cadmata.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var log = new DiagnosticLog();
        try
        {
            log.Write("startup " + BuildIdentity + " root=" + AppContext.BaseDirectory);
            using var window = new CadmataWindow(args, log);
            Application.ThreadException += (_, e) => window.ShowFailure(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Write("fatal " + e.ExceptionObject);
            Application.Run(window);
            return 0;
        }
        catch (Exception exception)
        {
            log.Write("fatal " + exception);
            MessageBox.Show($"Cadmata could not start.\n\n{exception.Message}\n\nDiagnostic log: {log.Path}",
                "Cadmata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
    }

    internal static string BuildIdentity => typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Preview 4";
}

internal sealed class CadmataWindow : Form
{
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Fill, Text = "Starting Cadmata…", TextAlign = ContentAlignment.MiddleCenter };
    private readonly DiagnosticLog _log;
    private readonly CancellationTokenSource _lifetime = new();
    private Microsoft.AspNetCore.Builder.WebApplication? _server;
    private readonly string _profile;
    private readonly string[] _args;
    private bool _closing;
    private bool _shutdownComplete;

    internal CadmataWindow(string[] args, DiagnosticLog log)
    {
        _args = args;
        _log = log;
        _profile = System.IO.Path.Combine(log.DataRoot, "sessions", Guid.NewGuid().ToString("N"));
        Text = "Cadmata — Aetheris Preview 4";
        Width = 1440; Height = 960; MinimumSize = new Size(900, 600);
        Controls.Add(_view); Controls.Add(_status);
        var menu = new MenuStrip();
        menu.Items.Add("About Cadmata", null, (_, _) => MessageBox.Show(
            $"Cadmata — Aetheris Preview 4\n{Program.BuildIdentity}\n\nLogs: {_log.Path}\nSource: https://github.com/yuechen-li-dev/Aetheris",
            "About Cadmata"));
        menu.Items.Add("Open diagnostic log", null, (_, _) => Process.Start(new ProcessStartInfo(_log.Path) { UseShellExecute = true }));
        Controls.Add(menu); MainMenuStrip = menu;
        Shown += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var runtime = System.IO.Path.Combine(AppContext.BaseDirectory, "webview2");
            if (!File.Exists(System.IO.Path.Combine(runtime, "msedgewebview2.exe")))
                throw new FileNotFoundException("Bundled WebView2 runtime is missing. Extract the complete Cadmata ZIP again.");
            var options = CadmataLaunchOptions.Parse(_args.Length == 0
                ? [System.IO.Path.Combine(AppContext.BaseDirectory, "samples", "mounting-block.firmament")]
                : _args);
            options.ValidateProductionAssets(AppContext.BaseDirectory);
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _server = CadmataApplication.Create([], options, portable: true, logging: _log.Provider, sessionToken: token);
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            startup.CancelAfter(TimeSpan.FromSeconds(45));
            await _server.StartAsync(startup.Token);
            var address = _server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
                .Addresses.Single();
            _log.Write("backend ready " + address);
            _server.Lifetime.ApplicationStopped.Register(() =>
            {
                if (!_lifetime.IsCancellationRequested && !IsDisposed)
                    BeginInvoke(() => ShowFailure(new InvalidOperationException("The local CAD service stopped unexpectedly. Reopen Cadmata.")));
            });
            Directory.CreateDirectory(_profile);
            var environment = await CoreWebView2Environment.CreateAsync(runtime, _profile);
            await _view.EnsureCoreWebView2Async(environment).WaitAsync(startup.Token);
            if (_lifetime.IsCancellationRequested) return;
            _view.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _view.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _view.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
            _view.CoreWebView2.WindowCloseRequested += (_, _) => Close();
            _view.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.GetLeftPart(UriPartial.Authority) != address.TrimEnd('/'))
                    e.Cancel = true;
            };
            _view.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            _view.CoreWebView2.WebResourceRequested += (_, e) =>
            {
                if (Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)
                    && uri.Scheme is "http" or "https" && uri.GetLeftPart(UriPartial.Authority) != address.TrimEnd('/'))
                    e.Response = environment.CreateWebResourceResponse(null, 403, "Offline local application", "");
            };
            _view.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (e.Source.StartsWith(address + "/", StringComparison.Ordinal))
                    _log.Write("frontend " + e.WebMessageAsJson);
            };
            _view.CoreWebView2.ProcessFailed += (_, e) => ShowFailure(new InvalidOperationException($"WebView2 process failed: {e.ProcessFailedKind}. Reopen Cadmata."));
            _view.CoreWebView2.DownloadStarting += (_, e) =>
            {
                _log.Write("export download " + e.ResultFilePath);
                e.DownloadOperation.StateChanged += (_, _) =>
                    _log.Write("export " + e.DownloadOperation.State + " " + e.DownloadOperation.InterruptReason);
            };
            _view.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                if (e.IsSuccess) { _status.Visible = false; _log.Write("UI visible"); }
                else ShowFailure(new InvalidOperationException("The local page could not load: " + e.WebErrorStatus));
            };
            _view.CoreWebView2.Navigate(address + "/__cadmata/session/" + token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { if (!IsDisposed) ShowFailure(exception); }
    }

    internal void ShowFailure(Exception exception)
    {
        _log.Write("failure " + exception);
        _status.Text = $"Cadmata could not continue.\n\n{exception.Message}\n\nLog: {_log.Path}\nClose and reopen Cadmata after correcting the problem.";
        _status.Visible = true; _status.BringToFront();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel || _shutdownComplete) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        _log.Write("closing");
        _status.Text = "Closing Cadmata…";
        _status.Visible = true;
        _status.BringToFront();
        _lifetime.Cancel();
        _view.Dispose();
        _log.Write("browser controller closed");
        if (_server is not null)
        {
            try
            {
                using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _server.StopAsync(shutdown.Token);
                await _server.DisposeAsync();
                _log.Write("backend stopped");
            }
            catch (Exception exception) { _log.Write("shutdown " + exception); }
        }
        _shutdownComplete = true;
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // The browser may release profile locks a moment after its controller.
        // Deletion is bounded; a stale session is cleaned on the next launch.
        try { Directory.Delete(_profile, true); }
        catch (IOException) { _log.Write("profile cleanup deferred " + _profile); }
        catch (UnauthorizedAccessException) { _log.Write("profile cleanup deferred " + _profile); }
        _log.Write("closed");
        _lifetime.Dispose();
        base.OnFormClosed(e);
    }
}
