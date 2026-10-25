# Aetheris bundles

Download a bundle to use the CAD compiler and Cadmata viewer without installing
.NET, Node.js or a development environment. Your designs stay on your computer.

| System | Archive | Viewer |
| --- | --- | --- |
| Windows x64 | `Aetheris-2.0.0-win-x64.zip` | Double-click `Cadmata.exe` |
| Linux x64 (Intel/AMD, glibc) | `Aetheris-2.0.0-linux-x64.tar.gz` | Run `./Cadmata` in the extracted folder |
| macOS arm64 (Apple Silicon) | `Aetheris-2.0.0-osx-arm64.tar.gz` | Open `Open Cadmata.command`, or run `./Cadmata` in Terminal |

Get the archives from [Releases](https://github.com/yuechen-li-dev/Aetheris/releases/tag/v2.0.0).
Extract the entire archive into a fresh local folder. Preserve executable
permissions when extracting the Linux/macOS tar archive. You can use
`tar -xzf Aetheris-2.0.0-linux-x64.tar.gz` (substitute the macOS filename on a Mac).

Cadmata opens in your existing default browser. Choose a STEP file, click
**Import STEP 242**, then orbit, select or measure the part. Use **Product Gallery**
to generate an example. A blank workspace on startup is intentional.

Keep the launcher terminal open while using the viewer. Press **Ctrl+C** there
to stop the local service. Closing the browser tab alone does not stop it.
The service uses a temporary loopback port, and the normal launcher establishes
a local session cookie. No browser engine is bundled or installed.

## Compile a part

Run these commands from the extracted directory on Linux or macOS:

```sh
./aetheris build fixtures/Canonical/PMI/hole-diameter-and-datum.firmament --output first-part.step
./aetheris analyze first-part.step --json
./aetheris view first-part.step
./forge-host/Aetheris.Forge.Host list
```

On Windows PowerShell, use `.\aetheris.exe` and
`.\forge-host\Aetheris.Forge.Host.exe` instead of the Unix executable paths.
The example produces a plate with a hole, datum and toleranced hole diameter.
See [Getting started](getting-started.md) and [Learn Firmament](learn/README.md).

The bundle includes a local copy of this guide under `docs/public`, source
examples under `fixtures`, and Forge client examples under `samples`.
Verify binary downloads against the release's `SHA256SUMS.txt`.
The CLI-only NuGet tool and C# libraries are separate downloads; installing
`Aetheris.CLI` alone does not install the viewer.

## Requirements and limits

- A modern browser with hardware acceleration is recommended. Cadmata uses WebGPU
  when available and an identified WebGL fallback otherwise. Initialization or
  device failures appear as diagnostics.
- Linux targets **x86-64 glibc** distributions supported by .NET 10, such as
  Ubuntu 24.04 or newer. Alpine/musl is not supported by this archive. The normal
  browser launch needs a graphical desktop and a working `xdg-open`.
- macOS targets **Apple Silicon** with macOS 15 or newer. Intel Macs have no bundle.
  The download is not Developer ID signed or notarized. If macOS blocks it,
  verify its checksum and use the per-app **Open Anyway** option under
  **System Settings → Privacy & Security**. Do not disable Gatekeeper globally.
- Windows also supplies an optional dedicated `Cadmata.Desktop.exe` window using
  shared Evergreen WebView2. See [Windows launch options](cadmata/portable-windows.md).
- To run an explicitly addressed server without launching a browser, use
  `./Cadmata --no-browser --urls http://127.0.0.1:5090` (`.\Cadmata.exe` on Windows).
  This explicit mode does not create the normal launcher's session cookie; keep
  it on loopback and do not expose it as an authenticated multi-user service.

The compiler supports documented feature combinations, not arbitrary CAD edits.
Read [Supported features](reference/supported-features.md) and
[Known issues](reference/known-issues.md) for modeling boundaries.
