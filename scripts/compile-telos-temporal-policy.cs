using Copeland.TS.Gpu;
using Copeland.TS.Gpu.VdMir;
using Copeland.TS.Gpu.Wgsl;

string source = File.ReadAllText(args[0]);
var request = new GpuCompilationRequest([new GpuSourceFile("temporal-policy.v.ts", source)]);
var result = WgslGraphicsBackend.Compile(request);
if (!result.Success)
{
    throw new InvalidOperationException(string.Join("\n",
        result.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
}

var program = result.Program!;
string policyName = program.FunctionNames["TemporalPolicy"];
string shim = $$"""


// Source: fixtures/three-telos/temporal-policy.v.ts
// Semantic hash: {{program.SemanticHash}}
// Static linkage from compiler-owned FunctionNames.
fn telosPolicy(depth: f32, motion: f32, color: f32, edge: f32) -> vec3<f32> {
    return {{policyName}}(depth, motion, color, edge);
}

""";
File.WriteAllText(args[1], program.Code + shim);
Console.WriteLine("Temporal policy -> ordinary VD-MIR -> direct WGSL: " + program.SemanticHash);
