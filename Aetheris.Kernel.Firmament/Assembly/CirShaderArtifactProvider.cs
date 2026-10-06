using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Copeland.TS.Gpu;
using Copeland.TS.Gpu.Wgsl;
using Copeland.TS.Gpu.VdMir;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed record DisplayShaderBinding(int Group, int Binding, string Kind, int ByteSize);
public sealed record DisplayShaderArtifact(string ShaderId, string Wgsl, string VertexEntryPoint,
    string FragmentEntryPoint, IReadOnlyList<DisplayShaderBinding> Bindings, IReadOnlyList<string> Capabilities,
    string SourceIdentity, string CompilerVersion, string Schema = "aetheris/display-shader/1");
public sealed record CirShaderBinding(DisplayShaderArtifact? Artifact, string Status, string? Reason,
    bool CacheHit, double GenerationMilliseconds);

/// <summary>Bounded display-only compilation over compiler-retained CIR. No GPU or product state.</summary>
public static class CirShaderArtifactProvider
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, CirShaderBinding> Cache = new(StringComparer.Ordinal);
    private static readonly Queue<string> Order = new();
    private static readonly string Template = ReadTemplate();
    private static readonly string Compiler = WgslGraphicsBackend.CompatibilityVersion + ":" +
        typeof(WgslGraphicsBackend).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    private static readonly string Contract = Compiler + ":" + Hash(Template);
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static string ReadTemplate()
    {
        using var stream = typeof(CirShaderArtifactProvider).Assembly.GetManifestResourceStream("Aetheris.Display.TelosField.v.ts")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
    public static CirShaderBinding Resolve(FirmamentCirRetention? cir)
    {
        if (cir?.Qualification != "cir-qualified")
            return new(null, "mesh-fallback", cir?.FallbackReason ?? "no-field-representation", false, 0);
        if (cir.FieldSource is null || cir.StructuralIdentity is null ||
            Hash(Aetheris.Continuum.Backends.Sdf.CirVisualTsLowerer.CompatibilityVersion + "\n" + cir.FieldSource) != cir.StructuralIdentity)
            return new(null, "shader-artifact-generation-failed", "Retained field source/structural identity mismatch.", false, 0);
        var key = Contract + ":" + cir.StructuralIdentity;
        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached with { CacheHit = true, GenerationMilliseconds = 0 };
            var timer = Stopwatch.StartNew();
            var result = WgslGraphicsBackend.Compile(new GpuCompilationRequest([new GpuSourceFile("TelosField.v.ts", cir.FieldSource + Template)]));
            CirShaderBinding binding;
            if (!result.Success)
                binding = new(null, "shader-artifact-generation-failed", string.Join("; ", result.Diagnostics.Select(d => d.Code + ": " + d.Message)), false, timer.Elapsed.TotalMilliseconds);
            else
            {
                var program = result.Program!;
                var artifact = new DisplayShaderArtifact(Hash(Contract + ":" + program.SemanticHash), program.Code,
                    program.VertexEntryPoint, program.FragmentEntryPoint,
                    [new(0, 0, "uniform", program.Semantics.Material!.Size)],
                    ["fragment-depth", "telos-field-rays/2", "rigid-occurrence"], cir.StructuralIdentity, Compiler);
                binding = new(artifact, "shader-artifact-bound", null, false, timer.Elapsed.TotalMilliseconds);
            }
            if (Cache.Count == 256) Cache.Remove(Order.Dequeue());
            Cache.Add(key, binding); Order.Enqueue(key);
            return binding;
        }
    }
}
