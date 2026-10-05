using System.Globalization;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Downstream preview appearance, never engineering material or density authority.</summary>
public sealed record AssemblyUsdMaterial(double Red = .48, double Green = .55, double Blue = .63,
    double Metallic = .8, double Roughness = .28, double Opacity = 1,
    double EmissiveRed = 0, double EmissiveGreen = 0, double EmissiveBlue = 0);
public sealed record AssemblyUsdSample(double Time, IReadOnlyDictionary<string, double> State);
public sealed record AssemblyUsdOptions(IReadOnlyDictionary<string, double>? State = null,
    IReadOnlyList<AssemblyUsdSample>? Samples = null,
    IReadOnlyDictionary<string, AssemblyUsdMaterial>? DefinitionMaterials = null);

public sealed record DisplayCamera(string Name, double[] Transform, double FovDegrees);
public sealed record SpatialUsdMetadata(IReadOnlyDictionary<string,string> DefinitionIdentities,
    IReadOnlyDictionary<string,AssemblyAppearanceBinding> Appearances,
    IReadOnlyDictionary<string,IReadOnlyDictionary<string,string>> Properties,
    IReadOnlyList<DisplayCamera> Cameras);

/// <summary>Bounded USDA lowering of the accepted assembly and production display mesh.
/// STEP/BRep and the Interface compiler remain authoritative. No USD dependency or solver.</summary>
public static class AssemblyUsdExporter
{
    public static string Export(AssemblyM1CompilationResult compilation, AssemblyUsdOptions? options = null)
    {
        options ??= new();
        if (!compilation.IsSuccess || compilation.Ir is null) throw new InvalidOperationException("assembly-usd-invalid-compilation");
        var pose = AssemblyKinematics.Evaluate(compilation.Ir, options.State ?? new Dictionary<string, double>());
        var mesh = AssemblyDisplayMeshExporter.Export(compilation, pose: pose);
        return Serialize(compilation.Ir, mesh, options, pose.State);
    }

    /// <summary>Serialize already prepared geometry; useful for repeated poses without tessellating again.</summary>
    public static string Serialize(AssemblyIr ir, AssemblyDisplayMeshDocument mesh, AssemblyUsdOptions? options = null,
        IReadOnlyDictionary<string, double>? evaluatedState = null)
    {
        options ??= new();
        if (mesh.Units != "mm" || mesh.Occurrences.Count != ir.Instances.Count
            || !mesh.Occurrences.Select(o => o.Id).ToHashSet(StringComparer.Ordinal).SetEquals(ir.Instances.Select(i => i.StableId)))
            throw new InvalidOperationException("assembly-usd-document-mismatch");
        return SerializeCore(ir, mesh, options, evaluatedState, null);
    }

    /// <summary>Spatial display projection. No assembly IR, mechanical solving or inferred joints.</summary>
    public static string SerializeSpatial(AssemblyDisplayMeshDocument mesh, SpatialUsdMetadata metadata,
        AssemblyUsdOptions? options = null)
    {
        if (mesh.Units != "mm" || mesh.Occurrences.Count(o => o.ParentId is null) != 1
            || mesh.Occurrences.Any(o => o.Transform.Length != 16 || o.Transform.Any(v => !double.IsFinite(v))))
            throw new InvalidOperationException("spatial-usd-invalid-document");
        return SerializeCore(null, mesh, options ?? new(), null, metadata);
    }

    private static string SerializeCore(AssemblyIr? ir, AssemblyDisplayMeshDocument mesh, AssemblyUsdOptions options,
        IReadOnlyDictionary<string, double>? evaluatedState, SpatialUsdMetadata? spatial)
    {
        var rootId = ir?.RootInstanceStableId ?? mesh.Occurrences.Single(o => o.ParentId is null).Id;
        var rootPrim = spatial is null ? "Assembly" : "Scene";
        var occurrences = mesh.Occurrences.ToDictionary(o => o.Id, StringComparer.Ordinal);
        var definitions = mesh.Definitions.OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();
        var authoredLooks = (ir is null ? spatial!.Appearances.Values : ir.Instances.Where(i => i.Appearance is not null).Select(i => i.Appearance!))
            .DistinctBy(a => a.Appearance).OrderBy(a => a.Appearance, StringComparer.Ordinal).ToArray();
        var lookPaths = authoredLooks.Select((look, index) => (look.Appearance, Path: "/Looks/Authored_" + index))
            .ToDictionary(p => p.Appearance, p => p.Path, StringComparer.Ordinal);
        var declaredPose = ir is null ? null : AssemblyKinematics.Evaluate(ir, evaluatedState ?? options.State ?? new Dictionary<string, double>());
        if (declaredPose is not null && (!declaredPose.IsSuccess || declaredPose.Instances.Any(instance => instance.ResolvedTransform is null
            || occurrences[instance.StableId].Transform.Length != 16
            || occurrences[instance.StableId].Transform.Any(value => !double.IsFinite(value))
            || occurrences[instance.StableId].Transform.Zip(instance.ResolvedTransform.Matrix, (a,b) => Math.Abs(a-b)).Any(delta => delta > 1e-9))))
            throw new InvalidOperationException("assembly-usd-mesh-state-mismatch");
        if (options.DefinitionMaterials is not null && options.DefinitionMaterials.Keys.Any(identity => !definitions.Any(d => d.Identity == identity)))
            throw new InvalidOperationException("assembly-usd-material-definition-unknown");
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        // Sibling ordinal suffixes preserve every identity even when USD sanitization collides.
        void Assign(string id, string path)
        {
            paths.Add(id, path);
            var children = mesh.Occurrences.Where(o => o.ParentId == id).OrderBy(o => o.Path, StringComparer.Ordinal).ToArray();
            for (var i = 0; i < children.Length; i++) Assign(children[i].Id, path + "/" + Identifier(children[i].Path.Split('.').Last()) + "_" + i);
        }
        Assign(rootId, "/" + rootPrim);
        if (paths.Count != occurrences.Count) throw new InvalidOperationException("assembly-usd-invalid-hierarchy");
        var definitionPaths = definitions.Select((d, i) => (d.Id, Path: "/Definitions/D_" + i)).ToDictionary(p => p.Id, p => p.Path);
        var samples = (options.Samples ?? []).OrderBy(s => s.Time).ToArray();
        if (samples.Any(s => !double.IsFinite(s.Time)) || samples.Select(s => s.Time).Distinct().Count() != samples.Length)
            throw new InvalidOperationException("assembly-usd-invalid-sample-time");
        var poses = ir is null ? [] : samples.Select(s => (s.Time, Pose: AssemblyKinematics.Evaluate(ir, s.State))).ToArray();
        if (poses.Any(p => !p.Pose.IsSuccess)) throw new InvalidOperationException("assembly-usd-invalid-sample-state");
        var text = new StringBuilder();
        void Line(string value = "") => text.Append(value).Append('\n');
        Line("#usda 1.0");
        Line("("); Line($"    defaultPrim = {Q(rootPrim)}"); Line("    metersPerUnit = 0.001"); Line("    upAxis = \"Z\"");
        if (samples.Length > 0)
        { Line($"    startTimeCode = {F(samples[0].Time)}"); Line($"    endTimeCode = {F(samples[^1].Time)}"); Line("    timeCodesPerSecond = 24"); }
        Line(")");
        Line("class Scope \"Definitions\" {");
        for (var i = 0; i < definitions.Length; i++)
        {
            var d = definitions[i];
            if (d.Positions.Length == 0 || d.Positions.Length % 3 != 0 || d.Normals.Length != d.Positions.Length
                || d.Indices.Length == 0 || d.Indices.Length % 3 != 0 || d.Indices.Any(n => n < 0 || n >= d.Positions.Length / 3))
                throw new InvalidOperationException("assembly-usd-invalid-mesh:" + d.Identity);
            Line($"    def Xform \"D_{i}\" {{");
            Line($"        custom string aetheris:definitionIdentity = {Q(d.Identity)}");
            Line($"        def Mesh \"Mesh\" (prepend apiSchemas = [\"MaterialBindingAPI\"]) {{");
            Line("            uniform token subdivisionScheme = \"none\""); Line("            uniform token orientation = \"rightHanded\"");
            Line($"            point3f[] points = {Vectors(d.Positions)}");
            Line($"            normal3f[] normals = {Vectors(d.Normals)} (interpolation = \"vertex\")");
            Line($"            int[] faceVertexCounts = [{string.Join(", ", Enumerable.Repeat("3", d.Indices.Length / 3))}]");
            Line($"            int[] faceVertexIndices = [{string.Join(", ", d.Indices)}]");
            Line($"            rel material:binding = </Looks/M_{i}>");
            var material = options.DefinitionMaterials?.GetValueOrDefault(d.Identity) ?? new();
            ValidateMaterial(material);
            Line($"            color3f[] primvars:displayColor = [({F(material.Red)}, {F(material.Green)}, {F(material.Blue)})]");
            Line("        }"); Line("    }");
        }
        Line("}"); Line("def Scope \"Looks\" {");
        for (var i = 0; i < definitions.Length; i++)
        {
            var m = options.DefinitionMaterials?.GetValueOrDefault(definitions[i].Identity) ?? new();
            Line($"    def Material \"M_{i}\" {{");
            Line($"        token outputs:surface.connect = </Looks/M_{i}/Shader.outputs:surface>");
            Line("        def Shader \"Shader\" {"); Line("            uniform token info:id = \"UsdPreviewSurface\"");
            Line($"            color3f inputs:diffuseColor = ({F(m.Red)}, {F(m.Green)}, {F(m.Blue)})");
            Line($"            float inputs:metallic = {F(m.Metallic)}"); Line($"            float inputs:roughness = {F(m.Roughness)}");
            if (m.Opacity != 1) Line($"            float inputs:opacity = {F(m.Opacity)}");
            if (m.EmissiveRed != 0 || m.EmissiveGreen != 0 || m.EmissiveBlue != 0)
                Line($"            color3f inputs:emissiveColor = ({F(m.EmissiveRed)}, {F(m.EmissiveGreen)}, {F(m.EmissiveBlue)})");
            Line("            token outputs:surface"); Line("        }"); Line("    }");
        }
        foreach (var look in authoredLooks)
        {
            var path = lookPaths[look.Appearance]; var m = look.Preview;
            ValidateMaterial(m);
            Line($"    def Material {Q(path.Split('/').Last())} {{");
            Line($"        custom string aetheris:appearanceIdentity = {Q(look.Appearance)}");
            Line($"        token outputs:surface.connect = <{path}/Shader.outputs:surface>");
            Line("        def Shader \"Shader\" {"); Line("            uniform token info:id = \"UsdPreviewSurface\"");
            Line($"            color3f inputs:diffuseColor = ({F(m.Red)}, {F(m.Green)}, {F(m.Blue)})");
            Line($"            float inputs:metallic = {F(m.Metallic)}"); Line($"            float inputs:roughness = {F(m.Roughness)}");
            if (m.Opacity != 1) Line($"            float inputs:opacity = {F(m.Opacity)}");
            if (m.EmissiveRed != 0 || m.EmissiveGreen != 0 || m.EmissiveBlue != 0)
                Line($"            color3f inputs:emissiveColor = ({F(m.EmissiveRed)}, {F(m.EmissiveGreen)}, {F(m.EmissiveBlue)})");
            Line("            token outputs:surface"); Line("        }"); Line("    }");
        }
        Line("}");
        var joints = ir?.Joints ?? [];
        var bodies = joints.SelectMany(j => new[] { j.ParentOccurrenceId, j.ChildOccurrenceId }).ToHashSet();
        void WriteOccurrence(string id, int depth)
        {
            var o = occurrences[id]; var indent = new string(' ', depth * 4);
            var api = new List<string>();
            if (id == rootId && joints.Count > 0) api.Add("PhysicsArticulationRootAPI");
            if (bodies.Contains(id)) api.Add("PhysicsRigidBodyAPI");
            Line($"{indent}def Xform {Q(paths[id].Split('/').Last())}" + (api.Count == 0 ? "" : $" (prepend apiSchemas = [{string.Join(", ", api.Select(Q))}])") + " {");
            Line($"{indent}    custom string aetheris:occurrenceIdentity = {Q(id)}");
            Line($"{indent}    custom string aetheris:sourcePath = {Q(o.Path)}");
            Line($"{indent}    custom string aetheris:definitionIdentity = {Q(ir?.Instances.Single(i => i.StableId == id).DefinitionIdentity ?? spatial!.DefinitionIdentities.GetValueOrDefault(id, ""))}");
            foreach (var field in spatial?.Properties.GetValueOrDefault(id) ?? new Dictionary<string,string>())
                Line($"{indent}    custom string aetheris:{Identifier(field.Key)} = {Q(field.Value)}");
            if (bodies.Contains(id)) { Line($"{indent}    bool physics:rigidBodyEnabled = true"); Line($"{indent}    bool physics:kinematicEnabled = true"); }
            if (id == rootId)
            {
                Line($"{indent}    custom string aetheris:sourceName = {Q(ir?.Name ?? mesh.Name)}");
                if (ir?.Annotations?.Release is { } release)
                    foreach (var field in release.Fields.OrderBy(p => p.Key, StringComparer.Ordinal))
                        Line($"{indent}    custom string aetheris:provenance:{field.Key} = {Q(field.Value)}");
                foreach (var note in ir?.Annotations?.Notes ?? [])
                {
                    Line($"{indent}    custom string aetheris:pmi:{note.Name}:target = {Q(note.Target)}");
                    Line($"{indent}    custom string aetheris:pmi:{note.Name}:text = {Q(note.Text)}");
                }
            }
            double[] Local(double[] world, double[]? parent) => parent is null ? world :
                (Transform3D.FromRowMajor(world) * Transform3D.FromRowMajor(parent).Inverse()).ToRowMajor();
            var local = Local(o.Transform, o.ParentId is null ? null : occurrences[o.ParentId].Transform);
            Line($"{indent}    double3 xformOp:translate = {Translation(local)}");
            Line($"{indent}    quatd xformOp:orient = {Quaternion(local)}");
            Line($"{indent}    uniform token[] xformOpOrder = [\"xformOp:translate\", \"xformOp:orient\"]");
            if (poses.Length > 0)
            {
                var localSamples = new List<(double Time, double[] Matrix)>();
                foreach (var p in poses)
                {
                    var instance = p.Pose.Instances.Single(i => i.StableId == id);
                    var parent = o.ParentId is null ? null : p.Pose.Instances.Single(i => i.StableId == o.ParentId).ResolvedTransform!.Matrix;
                    localSamples.Add((p.Time, Local(instance.ResolvedTransform!.Matrix, parent)));
                }
                Line($"{indent}    double3 xformOp:translate.timeSamples = {{");
                foreach (var s in localSamples) Line($"{indent}        {F(s.Time)}: {Translation(s.Matrix)},");
                Line($"{indent}    }}");
                Line($"{indent}    quatd xformOp:orient.timeSamples = {{");
                foreach (var s in localSamples) Line($"{indent}        {F(s.Time)}: {Quaternion(s.Matrix)},");
                Line($"{indent}    }}");
            }
            if (o.DefinitionId is not null)
            {
                var look = ir?.Instances.Single(i => i.StableId == id).Appearance ?? spatial?.Appearances.GetValueOrDefault(id);
                Line($"{indent}    def Xform \"Geometry\" (prepend references = <{definitionPaths[o.DefinitionId]}>; instanceable = true" +
                    (look is null ? "" : "; prepend apiSchemas = [\"MaterialBindingAPI\"]") + ") {");
                if (look is not null)
                {
                    Line($"{indent}        custom string userProperties:aetheris_material = {Q(look.PhysicalIdentity)}");
                    Line($"{indent}        custom string userProperties:aetheris_appearance = {Q(look.Appearance)}");
                    Line($"{indent}        rel material:binding = <{lookPaths[look.Appearance]}> (bindMaterialAs = \"strongerThanDescendants\")");
                    Line($"{indent}        over \"Mesh\" {{");
                    Line($"{indent}            custom string userProperties:aetheris_material = {Q(look.PhysicalIdentity)}");
                    Line($"{indent}            custom string userProperties:aetheris_appearance = {Q(look.Appearance)}");
                    Line($"{indent}        }}");
                }
                Line($"{indent}    }}");
            }
            foreach (var child in mesh.Occurrences.Where(c => c.ParentId == id).OrderBy(c => c.Path, StringComparer.Ordinal)) WriteOccurrence(child.Id, depth + 1);
            if (id == rootId)
                foreach (var (camera, index) in (spatial?.Cameras ?? []).Select((c, i) => (c, i)))
                {
                    Line($"{indent}    def Camera {Q(Identifier(camera.Name) + "_" + index)} {{");
                    Line($"{indent}        matrix4d xformOp:transform = {Matrix(camera.Transform)}");
                    Line($"{indent}        uniform token[] xformOpOrder = [\"xformOp:transform\"]");
                    Line($"{indent}        float verticalAperture = 20.955");
                    Line($"{indent}        float horizontalAperture = 20.955");
                    Line($"{indent}        float focalLength = {F(20.955 / (2 * Math.Tan(camera.FovDegrees * Math.PI / 360)))}");
                    Line($"{indent}        float2 clippingRange = (1, 100000000)");
                    Line($"{indent}    }}");
                }
            Line(indent + "}");
        }
        WriteOccurrence(rootId, 0);
        Line("def Scope \"Joints\" {");
        foreach (var (j, i) in joints.OrderBy(j => j.Name, StringComparer.Ordinal).Select((j, i) => (j, i)))
        {
            var type = j.Family switch { MechanicalInterfaceFamily.Fixed => "PhysicsFixedJoint", MechanicalInterfaceFamily.Revolute => "PhysicsRevoluteJoint", MechanicalInterfaceFamily.Prismatic => "PhysicsPrismaticJoint", _ => throw new InvalidOperationException("assembly-usd-unsupported-joint") };
            Line($"    def {type} {Q(Identifier(j.Name) + "_" + i)} {{");
            Line($"        rel physics:body0 = <{paths[j.ParentOccurrenceId]}>"); Line($"        rel physics:body1 = <{paths[j.ChildOccurrenceId]}>");
            if (j.DegreesOfFreedom == 1) Line("        uniform token physics:axis = \"Z\"");
            void Frame(int n, AssemblyTransform frame)
            {
                var m = frame.Matrix;
                Line($"        point3f physics:localPos{n} = ({F(m[12])}, {F(m[13])}, {F(m[14])})");
                Line($"        quatf physics:localRot{n} = {Quaternion(m)}");
                Line($"        custom matrix4d aetheris:zeroFrame{n} = {Matrix(m)}");
            }
            Frame(0, j.ParentLocalFrame); Frame(1, j.ChildLocalFrame);
            Line($"        custom string aetheris:interfaceName = {Q(j.Name)}"); Line($"        custom string aetheris:interfaceFamily = {Q(j.Family.ToString())}");
            Line($"        custom string aetheris:parentOccurrence = {Q(j.ParentOccurrenceId)}"); Line($"        custom string aetheris:childOccurrence = {Q(j.ChildOccurrenceId)}");
            Line($"        custom string aetheris:parentFrameSemantic = {Q(j.ParentFrameSemanticId)}"); Line($"        custom string aetheris:childFrameSemantic = {Q(j.ChildFrameSemanticId)}");
            Line($"        custom double aetheris:zeroState = {F(j.DefaultState)}");
            Line($"        custom double aetheris:state = {F((evaluatedState ?? options.State)?.GetValueOrDefault(j.Name, j.DefaultState) ?? j.DefaultState)}");
            Line($"        custom token aetheris:stateUnit = {Q(j.Family == MechanicalInterfaceFamily.Revolute ? "degree" : j.Family == MechanicalInterfaceFamily.Prismatic ? "mm" : "fixed")}");
            if (poses.Length > 0)
            { Line("        custom double aetheris:state.timeSamples = {"); foreach (var p in poses) Line($"            {F(p.Time)}: {F(p.Pose.State.GetValueOrDefault(j.Name, j.DefaultState))},"); Line("        }"); }
            Line("    }");
        }
        Line("}");
        return text.ToString();
    }

    private static void ValidateMaterial(AssemblyUsdMaterial m)
    {
        if (new[] { m.Red, m.Green, m.Blue, m.Metallic, m.Roughness, m.Opacity, m.EmissiveRed, m.EmissiveGreen, m.EmissiveBlue }.Any(v => !double.IsFinite(v) || v < 0 || v > 1))
            throw new InvalidOperationException("assembly-usd-invalid-material");
    }
    private static string Identifier(string value) => "N_" + new string(value.Select(c => char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
    private static string Q(string value) => JsonSerializer.Serialize(value);
    private static string F(double value) => double.IsFinite(value) ? value.ToString("G17", CultureInfo.InvariantCulture) : throw new InvalidOperationException("assembly-usd-nonfinite");
    private static string Vectors(double[] values) => "[" + string.Join(", ", Enumerable.Range(0, values.Length / 3).Select(i => $"({F(values[3*i])}, {F(values[3*i+1])}, {F(values[3*i+2])})")) + "]";
    private static string Translation(double[] m) => $"({F(m[12])}, {F(m[13])}, {F(m[14])})";
    private static string Matrix(double[] m)
    {
        var t = Transform3D.FromRowMajor(m);
        if (t.Apply(new Vector3D(1,0,0)).Cross(t.Apply(new Vector3D(0,1,0))).Dot(t.Apply(new Vector3D(0,0,1))) < 0)
            throw new InvalidOperationException("assembly-usd-left-handed-transform");
        return "(" + string.Join(", ", Enumerable.Range(0, 4).Select(r => "(" + string.Join(", ", m.Skip(r * 4).Take(4).Select(F)) + ")")) + ")";
    }
    // Gf and Aetheris both use row vectors. Quaternion extraction uses the transpose of the usual column convention.
    private static string Quaternion(double[] m)
    {
        Matrix(m);
        double w, x, y, z;
        var trace = m[0] + m[5] + m[10];
        if (trace > 0) { var s = Math.Sqrt(trace + 1) * 2; w = s / 4; x = (m[6]-m[9])/s; y = (m[8]-m[2])/s; z = (m[1]-m[4])/s; }
        else if (m[0] > m[5] && m[0] > m[10]) { var s = Math.Sqrt(1+m[0]-m[5]-m[10])*2; w=(m[6]-m[9])/s; x=s/4; y=(m[4]+m[1])/s; z=(m[8]+m[2])/s; }
        else if (m[5] > m[10]) { var s = Math.Sqrt(1+m[5]-m[0]-m[10])*2; w=(m[8]-m[2])/s; x=(m[4]+m[1])/s; y=s/4; z=(m[9]+m[6])/s; }
        else { var s = Math.Sqrt(1+m[10]-m[0]-m[5])*2; w=(m[1]-m[4])/s; x=(m[8]+m[2])/s; y=(m[9]+m[6])/s; z=s/4; }
        return $"({F(w)}, {F(x)}, {F(y)}, {F(z)})";
    }
}
