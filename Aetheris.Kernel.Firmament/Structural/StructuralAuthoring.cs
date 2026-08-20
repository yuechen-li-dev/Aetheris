using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.StandardLibrary.Materials;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Structural;

public enum StructuralSectionKind { SquareTube, RectangularTube, RoundTube, Angle, FlatBar, RoundBar }
public enum StructuralJointKind { Butt, Miter }
public enum StructuralEnd { Start, End }

public sealed record StructuralNodeAir(string StableId, string Name, Point3D Point, string SourceProvenance);
public sealed record StructuralPathAir(string StableId, string Name, string StartNode, string EndNode, Vector3D Direction, double Length, string SourceProvenance);
public sealed record StructuralSectionAir(string StableId, string Name, StructuralSectionKind Kind, double Width, double Height, double Thickness, double Radius, double Area);
public sealed record StructuralMemberAir(string StableId, string Name, string Path, string Section, string Material, Vector3D Orientation, string StartInterface, string EndInterface);
public sealed record StructuralJointAir(string StableId, string Name, StructuralJointKind Kind, string FirstMember, StructuralEnd FirstEnd, string SecondMember, StructuralEnd SecondEnd, string PrimaryMember);
public sealed record StructuralWeldAir(string StableId, string Name, string Joint, string Type, double Size, IReadOnlyList<string> Members);
public sealed record StructuralJointInterfaceIr(
    string StableId, string Joint, string Relation,
    string FirstMember, string FirstMemberInterface, string SecondMember, string SecondMemberInterface,
    IReadOnlyList<double>? SharedPlaneOrigin, IReadOnlyList<double>? SharedPlaneNormal, bool? MatingSurfacesCoincident,
    string? SelectedStrategy, double? UtilityScore, bool? RetainedHalfSpacesOpposed,
    double? VolumetricOverlapMm3, IReadOnlyList<string> RejectedCandidates);
public sealed record StructuralAssemblyIr(
    string StableId, string Name, IReadOnlyList<string> MemberDefinitions,
    IReadOnlyList<string> MemberOccurrences, IReadOnlyList<string> JointInterfaces);
public sealed record StructuralEndTreatmentIr(string Kind, double? AngleDegrees = null, string? RelatedMember = null);
public sealed record StructuralMemberIr(string StableId, string PathStableId, string SectionStableId, string Material, double RawLength, double FinishedLength, StructuralEndTreatmentIr StartTreatment, StructuralEndTreatmentIr EndTreatment, double VolumeMm3, double MassKilograms, BrepBody Body, StructuralEndPlane StartPlane, StructuralEndPlane EndPlane);
public sealed record StructuralCutListEntry(string GroupId, string Section, string SectionKind, string Material, int Quantity, double RawLengthMm, double FinishedLengthMm, string StartTreatment, string EndTreatment, double MassKilograms, IReadOnlyList<string> Members);
public sealed record StructuralTimingReport(double ParseBindMilliseconds, double JointResolutionMilliseconds, double MemberGeometryMilliseconds, double AssemblyAndStepMilliseconds, double CutListMilliseconds);
public sealed record StructuralReport(
    string Schema, string Structure, StructuralAssemblyIr Assembly, IReadOnlyList<StructuralNodeAir> Nodes, IReadOnlyList<StructuralPathAir> Paths,
    IReadOnlyList<StructuralSectionAir> Sections, IReadOnlyList<StructuralMemberSummary> Members,
    IReadOnlyList<StructuralJointAir> Joints, IReadOnlyList<StructuralJointInterfaceIr> Interfaces,
    IReadOnlyList<StructuralWeldAir> Welds, IReadOnlyList<StructuralCutListEntry> CutList,
    string MaterializationRoute, string StepSha256, int BodyCount, int FaceCount, int PlaneCount, int CylinderCount,
    IReadOnlyList<double> Bounds, double AssemblyMassKilograms, bool StepReimportSucceeded, bool MembersEnclosed,
    string? CutListArtifactPath, StructuralTimingReport Timings);
public sealed record StructuralMemberSummary(string StableId, string Path, string Section, string Material, double RawLengthMm, double FinishedLengthMm, string StartTreatment, string EndTreatment, double MassKilograms);

public sealed record StructuralCompilationResult(bool IsSuccess, string? StepText, StructuralReport? Report, IReadOnlyList<string> Diagnostics,
    IReadOnlyList<StructuralMemberIr>? RealizedMembers = null);
public readonly record struct StructuralEndPlane(double CenterDepth, double XCoefficient, double YCoefficient, StructuralEndTreatmentIr Treatment);

/// <summary>
/// Bounded X2 structural authoring front end. It deliberately recognizes only explicit Nodes,
/// straight Paths, constant Sections, Members, two-member Butt/Miter Joints, and Fillet Weld requirements.
/// The resulting immutable records are fabrication AIR; no joint is inferred from finished solids.
/// </summary>
public static class StructuralAuthoring
{
    private const double Tol = 1e-7;
    public static bool IsStructuralSource(string source) => Regex.IsMatch(source, @"\bStructure\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant);

    public static StructuralCompilationResult Compile(string source, string sourceIdentity = "memory")
    {
        var total = System.Diagnostics.Stopwatch.StartNew();
        var diagnostics = new List<string>();
        var parseClock = System.Diagnostics.Stopwatch.StartNew();
        var parsed = Parse(source, sourceIdentity, diagnostics);
        parseClock.Stop();
        if (parsed is null) return new(false, null, null, diagnostics);

        var jointClock = System.Diagnostics.Stopwatch.StartNew();
        var resolved = ResolveJoints(parsed, diagnostics);
        jointClock.Stop();
        if (diagnostics.Count > 0) return new(false, null, null, diagnostics);

        var geometryClock = System.Diagnostics.Stopwatch.StartNew();
        var realized = new List<StructuralMemberIr>();
        foreach (var member in parsed.Members.OrderBy(x => x.StableId, StringComparer.Ordinal))
        {
            var path = parsed.Paths.Single(x => x.Name == member.Path);
            var section = parsed.Sections.Single(x => x.Name == member.Section);
            var start = parsed.Nodes.Single(x => x.Name == path.StartNode).Point;
            var direction = Direction3D.Create(path.Direction);
            var frame = MemberFrame(direction, member.Orientation, diagnostics, member.Name);
            if (frame is null) continue;
            var endPlan = resolved[member.Name];
            var body = StructuralMemberBrepEmitter.Emit(member.Name, section, start, frame.Value.X, frame.Value.Y, direction, endPlan.Start, endPlan.End);
            if (!body.IsSuccess || body.Value is null)
            {
                diagnostics.AddRange(body.Diagnostics.Select(x => $"structural-member-geometry:{member.Name}: {x.Message}"));
                continue;
            }
            var finished = endPlan.End.CenterDepth - endPlan.Start.CenterDepth;
            var volume = section.Area * finished;
            var material = parsed.Materials[member.Material];
            realized.Add(new(member.StableId, path.StableId, section.StableId, material.Identity.FirmamentPath, path.Length, finished,
                endPlan.Start.Treatment, endPlan.End.Treatment, volume, volume * 1e-9 * material.Structural!.Density.SiValue, body.Value, endPlan.Start, endPlan.End));
        }
        geometryClock.Stop();
        if (diagnostics.Count > 0) return new(false, null, null, diagnostics);

        var interfaces = BuildInterfaces(parsed, realized, diagnostics);
        if (diagnostics.Count > 0) return new(false, null, null, diagnostics);

        var assemblyClock = System.Diagnostics.Stopwatch.StartNew();
        var definitions = realized.Select(x => new Step242AssemblyDefinition("def:" + x.StableId, x.StableId, x.Body)).ToArray();
        var identity = new double[] { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 };
        var occurrences = new[] { new Step242AssemblyOccurrence("structure:" + parsed.Name, parsed.Name, null, null, identity) }
            .Concat(realized.Select(x => new Step242AssemblyOccurrence("occ:" + x.StableId, x.StableId, "structure:" + parsed.Name, "def:" + x.StableId, identity))).ToArray();
        var assembly = new StructuralAssemblyIr("structure:" + parsed.Name, parsed.Name,
            definitions.Select(x => x.StableId).Order(StringComparer.Ordinal).ToArray(),
            occurrences.Where(x => x.DefinitionStableId is not null).Select(x => x.StableId).Order(StringComparer.Ordinal).ToArray(),
            interfaces.Select(x => x.StableId).Order(StringComparer.Ordinal).ToArray());
        var step = Step242AssemblyExporter.Export(new(parsed.Name, "structure:" + parsed.Name, definitions, occurrences));
        if (!step.IsSuccess || step.Value is null)
        {
            diagnostics.AddRange(step.Diagnostics.Select(x => "structural-step-export: " + x.Message));
            return new(false, null, null, diagnostics);
        }
        var imported = Step242AssemblyImporter.Import(step.Value);
        assemblyClock.Stop();
        if (!imported.IsSuccess)
        {
            diagnostics.AddRange(imported.Diagnostics.Select(x => "structural-step-reimport: " + x.Message));
            return new(false, null, null, diagnostics);
        }

        var cutClock = System.Diagnostics.Stopwatch.StartNew();
        var cutList = BuildCutList(parsed, realized);
        cutClock.Stop();
        var allPoints = realized.SelectMany(x => x.Body.Topology.Vertices.Select(v => x.Body.TryGetVertexPoint(v.Id, out var p) ? p : default)).ToArray();
        var surfaces = realized.SelectMany(x => x.Body.Topology.Faces.Select(f => x.Body.GetFaceSurface(f.Id).Kind)).ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))).ToLowerInvariant();
        total.Stop();
        var summaries = realized.Select(x => new StructuralMemberSummary(x.StableId, x.PathStableId, x.SectionStableId, x.Material, x.RawLength, x.FinishedLength,
            Treatment(x.StartTreatment), Treatment(x.EndTreatment), x.MassKilograms)).ToArray();
        var report = new StructuralReport("aetheris:structural:x2", parsed.Name, assembly, parsed.Nodes, parsed.Paths, parsed.Sections, summaries, parsed.Joints, interfaces, parsed.Welds, cutList,
            "Structure->MemberAIR->JointResolution->AssemblyInterfaces->MemberFinalization->AP242Assembly+CutList", hash, realized.Count,
            realized.Sum(x => x.Body.Topology.Faces.Count()), surfaces.Count(x => x == SurfaceGeometryKind.Plane), surfaces.Count(x => x == SurfaceGeometryKind.Cylinder),
            [allPoints.Min(x => x.X), allPoints.Min(x => x.Y), allPoints.Min(x => x.Z), allPoints.Max(x => x.X), allPoints.Max(x => x.Y), allPoints.Max(x => x.Z)],
            realized.Sum(x => x.MassKilograms), true, realized.All(IsEnclosed), null,
            new(parseClock.Elapsed.TotalMilliseconds, jointClock.Elapsed.TotalMilliseconds, geometryClock.Elapsed.TotalMilliseconds, assemblyClock.Elapsed.TotalMilliseconds, cutClock.Elapsed.TotalMilliseconds));
        return new(true, step.Value, report, [], realized);
    }

    public static string CutListJson(StructuralReport report) => JsonSerializer.Serialize(new
    {
        schema = "aetheris:structural-cut-list:x2", structure = report.Structure, units = new { length = "mm", mass = "kg" }, entries = report.CutList
    }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private sealed record Parsed(string Name, IReadOnlyList<StructuralNodeAir> Nodes, IReadOnlyList<StructuralPathAir> Paths, IReadOnlyList<StructuralSectionAir> Sections, IReadOnlyList<StructuralMemberAir> Members, IReadOnlyList<StructuralJointAir> Joints, IReadOnlyList<StructuralWeldAir> Welds, IReadOnlyDictionary<string, ResolvedMaterial> Materials);
    private sealed record MemberEnds(StructuralEndPlane Start, StructuralEndPlane End);

    private static Parsed? Parse(string source, string sourceIdentity, List<string> d)
    {
        var structure = Blocks(source, "Structure").SingleOrDefault();
        if (structure == default) { d.Add("structural-missing-structure: exactly one Structure block is required"); return null; }
        if (Blocks(source, "Structure").Count != 1) { d.Add("structural-multiple-structures-unsupported: X2 admits one Structure per model"); return null; }
        var nodes = Regex.Matches(structure.Body, @"\bNode\s+(?<n>[A-Za-z_]\w*)\s*=\s*\[(?<v>[^\]]+)\]\s*;", RegexOptions.CultureInvariant)
            .Select(m => new StructuralNodeAir($"structure:{structure.Name}:node:{m.Groups["n"].Value}", m.Groups["n"].Value, Point(Vector(m.Groups["v"].Value, d, "node:" + m.Groups["n"].Value)), sourceIdentity)).ToArray();
        var paths = Regex.Matches(structure.Body, @"\bPath\s+(?<n>[A-Za-z_]\w*)\s*:\s*(?<a>[A-Za-z_]\w*)\s*->\s*(?<b>[A-Za-z_]\w*)\s*;", RegexOptions.CultureInvariant)
            .Select(m => (Name:m.Groups["n"].Value, A:m.Groups["a"].Value, B:m.Groups["b"].Value)).ToArray();
        Duplicates(nodes.Select(x => x.Name), "node", d); Duplicates(paths.Select(x => x.Name), "path", d);
        var nodeByName = nodes.ToDictionary(x => x.Name, StringComparer.Ordinal);
        var pathAir = new List<StructuralPathAir>();
        foreach (var p in paths)
        {
            if (!nodeByName.TryGetValue(p.A, out var a) || !nodeByName.TryGetValue(p.B, out var b)) { d.Add($"structural-path-unknown-node:{p.Name}: '{p.A}' -> '{p.B}'"); continue; }
            var delta = b.Point - a.Point; if (delta.Length <= Tol) { d.Add($"structural-zero-length-member-path:{p.Name}"); continue; }
            pathAir.Add(new($"structure:{structure.Name}:path:{p.Name}", p.Name, p.A, p.B, delta / delta.Length, delta.Length, sourceIdentity));
        }

        var sections = new List<StructuralSectionAir>();
        foreach (var block in Blocks(source, "Section"))
        {
            var kindText = Field(block.Body, "Kind");
            if (kindText.StartsWith("Standard.Structural.", StringComparison.Ordinal)) kindText = kindText["Standard.Structural.".Length..];
            if (!Enum.TryParse<StructuralSectionKind>(kindText, out var kind)) { d.Add($"structural-section-kind-unsupported:{block.Name}: '{kindText}'"); continue; }
            var width = LengthField(block.Body, kind == StructuralSectionKind.RoundTube || kind == StructuralSectionKind.RoundBar ? "Diameter" : "Width", d, block.Name);
            var height = kind is StructuralSectionKind.SquareTube ? width : kind is StructuralSectionKind.RoundTube or StructuralSectionKind.RoundBar ? width : LengthField(block.Body, "Height", d, block.Name);
            var thickness = kind is StructuralSectionKind.FlatBar or StructuralSectionKind.RoundBar ? 0 : LengthField(block.Body, "Thickness", d, block.Name);
            if (width <= 0 || height <= 0) d.Add($"structural-section-invalid-dimensions:{block.Name}: dimensions must be positive");
            if (kind is StructuralSectionKind.SquareTube or StructuralSectionKind.RectangularTube or StructuralSectionKind.RoundTube && (thickness <= 0 || thickness * 2 >= Math.Min(width, height))) d.Add($"structural-section-wall-too-large:{block.Name}");
            var area = kind switch { StructuralSectionKind.SquareTube or StructuralSectionKind.RectangularTube => width*height-(width-2*thickness)*(height-2*thickness), StructuralSectionKind.RoundTube => Math.PI/4*(width*width-(width-2*thickness)*(width-2*thickness)), StructuralSectionKind.Angle => thickness*(width+height-thickness), StructuralSectionKind.FlatBar => width*height, StructuralSectionKind.RoundBar => Math.PI/4*width*width, _ => 0 };
            sections.Add(new($"standard:structural:{kind}:{Q(width)}:{Q(height)}:{Q(thickness)}", block.Name, kind, width, height, thickness, width/2, area));
        }
        Duplicates(sections.Select(x => x.Name), "section", d);

        var resolver = new MaterialResolver(); var materials = new Dictionary<string, ResolvedMaterial>(StringComparer.Ordinal);
        var members = new List<StructuralMemberAir>();
        foreach (var block in Blocks(source, "Member"))
        {
            AddMember(block.Name, Field(block.Body, "Path"), Field(block.Body, "Section"), Field(block.Body, "Material"), Field(block.Body, "Orientation", required:false));
        }
        var defaultMaterial = Blocks(source,"StructuralDefaults").Select(x=>Field(x.Body,"Material",required:false)).FirstOrDefault(x=>!string.IsNullOrWhiteSpace(x)) ?? "";
        foreach (var row in TableRows(source, "StructuralMemberRow", d))
            AddMember(Cell(row,"Name"),Cell(row,"Path"),Cell(row,"Section"),Cell(row,"Material",false) is var tableMaterial && tableMaterial.Length>0?tableMaterial:defaultMaterial,Cell(row,"Orientation",false));
        void AddMember(string name,string path,string section,string materialValue,string orientationText)
        {
            var materialRef=materialValue.Trim().Trim('"');
            if (!pathAir.Any(x => x.Name == path)) d.Add($"structural-member-unknown-path:{name}: '{path}'");
            if (!sections.Any(x => x.Name == section)) d.Add($"structural-member-unknown-section:{name}: '{section}'");
            var asymmetric = sections.FirstOrDefault(x => x.Name == section)?.Kind is StructuralSectionKind.Angle;
            if (asymmetric && string.IsNullOrWhiteSpace(orientationText)) d.Add($"structural-member-orientation-required:{name}: asymmetric section '{section}' requires Orientation");
            var orientation = Orientation(orientationText,d,name);
            if (!materials.ContainsKey(materialRef)) { var resolvedMaterial = resolver.Resolve(materialRef); if (!resolvedMaterial.IsSuccess) d.Add($"structural-material-unresolved:{name}: {resolvedMaterial.Message}"); else materials[materialRef] = resolvedMaterial.Material!; }
            members.Add(new($"member:{name}", name, path, section, materialRef, orientation, "interface:"+name+":start", "interface:"+name+":end"));
        }
        Duplicates(members.Select(x => x.Name), "member", d);
        if (pathAir.Select(x => x.Name).Except(members.Select(x => x.Path), StringComparer.Ordinal).Any()) d.Add("structural-unassigned-path: every structural Path must have one Member assignment");
        if (members.GroupBy(x => x.Path).Any(g => g.Count() != 1)) d.Add("structural-path-member-cardinality: each Path must have exactly one Member");

        var joints = new List<StructuralJointAir>();
        foreach (var block in Blocks(source, "Joint"))
        {
            var endpoints = Field(block.Body, "Members").Trim('[',']').Split(',', StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
            if (endpoints.Length != 2 || !TryEndpoint(endpoints[0], out var a, out var ae) || !TryEndpoint(endpoints[1], out var b, out var be)) { d.Add($"structural-joint-endpoints-invalid:{block.Name}: exactly two Member.Start/End references are required"); continue; }
            AddJoint(block.Name,Field(block.Body,"Type"),a,ae,b,be,Field(block.Body,"Primary",required:false));
        }
        foreach(var row in TableRows(source,"StructuralJointRow",d))
        {
            if(!Enum.TryParse<StructuralEnd>(Cell(row,"FirstEnd"),out var ae)||!Enum.TryParse<StructuralEnd>(Cell(row,"SecondEnd"),out var be)){d.Add($"structural-joint-endpoints-invalid:{Cell(row,"Name")}");continue;}
            AddJoint(Cell(row,"Name"),Cell(row,"Type"),Cell(row,"FirstMember"),ae,Cell(row,"SecondMember"),be,Cell(row,"Primary",false));
        }
        void AddJoint(string name,string type,string a,StructuralEnd ae,string b,StructuralEnd be,string primary)
        {
            if (!Enum.TryParse<StructuralJointKind>(type, out var kind)) { d.Add($"structural-joint-kind-unsupported:{name}: '{type}'"); return; }
            if (!members.Any(x => x.Name == a) || !members.Any(x => x.Name == b)) { d.Add($"structural-joint-unknown-member:{name}"); return; }
            if (string.IsNullOrEmpty(primary)) primary = a;
            if (kind == StructuralJointKind.Butt && primary != a && primary != b) d.Add($"structural-butt-primary-invalid:{name}: Primary must name a participant");
            joints.Add(new($"joint:{name}", name, kind, a, ae, b, be, primary));
        }
        Duplicates(joints.Select(x => x.Name), "joint", d);
        // A through member can host several terminating butt joints without changing its own end.
        // Only geometry-owning endpoints (both miter participants and the butt secondary) are exclusive.
        var occupied = joints.SelectMany(x => x.Kind == StructuralJointKind.Miter
                ? new[] { x.FirstMember+"."+x.FirstEnd, x.SecondMember+"."+x.SecondEnd }
                : new[] { (x.PrimaryMember == x.FirstMember ? x.SecondMember+"."+x.SecondEnd : x.FirstMember+"."+x.FirstEnd) })
            .GroupBy(x => x).Where(g => g.Count()>1).Select(g=>g.Key).ToArray();
        if (occupied.Length > 0) d.Add("structural-multiple-joints-at-end-unsupported: " + string.Join(", ", occupied));

        var welds = new List<StructuralWeldAir>();
        foreach (var block in Blocks(source, "Weld"))
        {
            AddWeld(block.Name,Field(block.Body,"Joint"),Field(block.Body,"Type"),Field(block.Body,"Size"));
        }
        foreach(var row in TableRows(source,"StructuralWeldRow",d))AddWeld(Cell(row,"Name"),Cell(row,"Joint"),Cell(row,"Type"),Cell(row,"Size"));
        void AddWeld(string name,string joint,string type,string sizeText)
        {
            var size=ParseLength(sizeText,d,name+".Size");var j=joints.FirstOrDefault(x=>x.Name==joint);if(j is null)d.Add($"structural-weld-unknown-joint:{name}: '{joint}'");if(type!="Fillet")d.Add($"structural-weld-type-unsupported:{name}: X2 admits Fillet");welds.Add(new($"weld:{name}",name,"joint:"+joint,type,size,j is null?[]:[j.FirstMember,j.SecondMember]));
        }
        return d.Count == 0 ? new(structure.Name, nodes, pathAir, sections, members, joints, welds, materials) : null;
    }

    private static Dictionary<string, MemberEnds> ResolveJoints(Parsed p, List<string> d)
    {
        var result = p.Members.ToDictionary(x => x.Name, x => new MemberEnds(new(0,0,0,new("Square")), new(p.Paths.Single(y=>y.Name==x.Path).Length,0,0,new("Square"))), StringComparer.Ordinal);
        foreach (var joint in p.Joints.OrderBy(x => x.StableId, StringComparer.Ordinal))
        {
            var first = p.Members.Single(x=>x.Name==joint.FirstMember); var second=p.Members.Single(x=>x.Name==joint.SecondMember);
            var fp=p.Paths.Single(x=>x.Name==first.Path); var sp=p.Paths.Single(x=>x.Name==second.Path);
            var nodeA = joint.FirstEnd==StructuralEnd.Start ? fp.StartNode : fp.EndNode; var nodeB=joint.SecondEnd==StructuralEnd.Start ? sp.StartNode : sp.EndNode;
            if (joint.Kind == StructuralJointKind.Miter && nodeA != nodeB) { d.Add($"structural-disconnected-joint:{joint.Name}: endpoints resolve to '{nodeA}' and '{nodeB}'"); continue; }
            var dot=Math.Abs(fp.Direction.Dot(sp.Direction)); if(dot>1-1e-6){d.Add($"structural-joint-collinear-unsupported:{joint.Name}");continue;}
            if(joint.Kind==StructuralJointKind.Miter)
            {
                var awayF=joint.FirstEnd==StructuralEnd.Start?fp.Direction:-fp.Direction; var awayS=joint.SecondEnd==StructuralEnd.Start?sp.Direction:-sp.Direction;
                // The two angle bisectors are not interchangeable. The difference bisector
                // separates the two rays leaving the node, so both member caps occupy one
                // coincident weld surface. The sum bisector puts both rays on the same side
                // and creates the characteristic crossed spikes/gaps at an outside corner.
                var selection=StructuralJointPathingPolicy.SelectMiter(new(awayF,awayS));
                if(!selection.IsSuccess||selection.SelectedStrategy is null){d.Add($"structural-miter-geometry-unsupported:{joint.Name}: {string.Join(" | ",selection.RejectedCandidates)}");continue;}
                var normal=selection.PlaneNormal;
                SetPlane(first,fp,joint.FirstEnd,normal,new("Miter", Math.Acos(Math.Clamp(awayF.Dot(awayS),-1,1))*90/Math.PI, second.StableId));
                SetPlane(second,sp,joint.SecondEnd,normal,new("Miter", Math.Acos(Math.Clamp(awayF.Dot(awayS),-1,1))*90/Math.PI, first.StableId));
            }
            else
            {
                var primary = joint.PrimaryMember==first.Name?first:second; var secondary=primary==first?second:first; var primaryPath=primary==first?fp:sp; var secondaryPath=secondary==first?fp:sp; var secondaryEnd=secondary==first?joint.FirstEnd:joint.SecondEnd;
                var secondaryNode=secondaryEnd==StructuralEnd.Start?secondaryPath.StartNode:secondaryPath.EndNode;
                var secondaryPoint=p.Nodes.Single(x=>x.Name==secondaryNode).Point;var primaryStart=p.Nodes.Single(x=>x.Name==primaryPath.StartNode).Point;var along=(secondaryPoint-primaryStart).Dot(primaryPath.Direction);
                var residual=(secondaryPoint-(primaryStart+primaryPath.Direction*along)).Length;
                if(residual>Tol||along < -Tol||along > primaryPath.Length+Tol){d.Add($"structural-disconnected-joint:{joint.Name}: terminating endpoint does not lie on through member '{primary.Name}'");continue;}
                var primarySection=p.Sections.Single(x=>x.Name==primary.Section); var clearance=ProjectedHalfExtent(primarySection, primaryPath.Direction, secondaryPath.Direction);
                var center=secondaryEnd==StructuralEnd.End?secondaryPath.Length-clearance:clearance;
                Update(secondary.Name,secondaryEnd,new(center,0,0,new("TrimmedTo",null,primary.StableId)));
            }
            void SetPlane(StructuralMemberAir member, StructuralPathAir path, StructuralEnd end, Vector3D worldNormal, StructuralEndTreatmentIr treatment)
            {
                var dir=Direction3D.Create(path.Direction); var frame=MemberFrame(dir,member.Orientation,d,member.Name); if(frame is null)return;
                var denominator=worldNormal.Dot(dir.ToVector()); if(Math.Abs(denominator)<Tol){d.Add($"structural-miter-geometry-unsupported:{joint.Name}: cut plane parallel to '{member.Name}'");return;}
                var center=end==StructuralEnd.Start?0:path.Length;
                Update(member.Name,end,new(center,-worldNormal.Dot(frame.Value.X.ToVector())/denominator,-worldNormal.Dot(frame.Value.Y.ToVector())/denominator,treatment));
            }
            void Update(string member,StructuralEnd end,StructuralEndPlane plane){var current=result[member];result[member]=end==StructuralEnd.Start?current with{Start=plane}:current with{End=plane};}
        }
        foreach(var pair in result) if(pair.Value.End.CenterDepth-pair.Value.Start.CenterDepth<=Tol)d.Add($"structural-member-finished-length-invalid:{pair.Key}");
        return result;
    }

    private static IReadOnlyList<StructuralJointInterfaceIr> BuildInterfaces(Parsed p, IReadOnlyList<StructuralMemberIr> realized, List<string> d)
    {
        var bodies = realized.ToDictionary(x => x.StableId, x => x.Body, StringComparer.Ordinal);
        var result = new List<StructuralJointInterfaceIr>();
        foreach (var joint in p.Joints.OrderBy(x => x.StableId, StringComparer.Ordinal))
        {
            var first = p.Members.Single(x => x.Name == joint.FirstMember);
            var second = p.Members.Single(x => x.Name == joint.SecondMember);
            var firstInterface = joint.FirstEnd == StructuralEnd.Start ? first.StartInterface : first.EndInterface;
            var secondInterface = joint.SecondEnd == StructuralEnd.Start ? second.StartInterface : second.EndInterface;
            if (joint.Kind == StructuralJointKind.Butt)
            {
                var primary = joint.PrimaryMember == first.Name ? first : second;
                var secondary = primary == first ? second : first;
                var primaryPath = p.Paths.Single(x => x.Name == primary.Path);
                var secondaryPath = p.Paths.Single(x => x.Name == secondary.Path);
                var secondaryEnd = secondary == first ? joint.FirstEnd : joint.SecondEnd;
                var secondaryNodeName = secondaryEnd == StructuralEnd.Start ? secondaryPath.StartNode : secondaryPath.EndNode;
                var node = p.Nodes.Single(x => x.Name == secondaryNodeName).Point;
                var away = secondaryEnd == StructuralEnd.Start ? secondaryPath.Direction : -secondaryPath.Direction;
                var primarySection = p.Sections.Single(x => x.Name == primary.Section);
                var contact = node + away * ProjectedHalfExtent(primarySection, primaryPath.Direction, secondaryPath.Direction);
                var primaryContained = AllVerticesInRetainedHalfSpace(bodies[primary.StableId], contact, away, -1);
                var secondaryContained = AllVerticesInRetainedHalfSpace(bodies[secondary.StableId], contact, away, 1);
                var buttOpposed = primaryContained && secondaryContained;
                if (!buttOpposed)
                    d.Add($"structural-butt-interface-overlap:{joint.Name}: terminating and through members are not bounded to opposite contact half-spaces");
                result.Add(new("interface:" + joint.Name, joint.StableId, "EndToMemberEnvelope", first.StableId, firstInterface,
                    second.StableId, secondInterface, [contact.X, contact.Y, contact.Z], [away.X, away.Y, away.Z], null,
                    "AuthoredPrimaryButt", 100d, buttOpposed, buttOpposed ? 0d : null, []));
                continue;
            }

            var firstPath = p.Paths.Single(x => x.Name == first.Path);
            var secondPath = p.Paths.Single(x => x.Name == second.Path);
            var nodeName = joint.FirstEnd == StructuralEnd.Start ? firstPath.StartNode : firstPath.EndNode;
            var origin = p.Nodes.Single(x => x.Name == nodeName).Point;
            var awayFirst = joint.FirstEnd == StructuralEnd.Start ? firstPath.Direction : -firstPath.Direction;
            var awaySecond = joint.SecondEnd == StructuralEnd.Start ? secondPath.Direction : -secondPath.Direction;
            var selection = StructuralJointPathingPolicy.SelectMiter(new(awayFirst, awaySecond));
            if (!selection.IsSuccess || selection.SelectedStrategy is null)
            {
                d.Add($"structural-miter-interface-pathing-rejected:{joint.Name}: {string.Join(" | ", selection.RejectedCandidates)}");
                continue;
            }
            var normal = Direction3D.Create(selection.PlaneNormal).ToVector();
            var firstPoints = EndFacePoints(bodies[first.StableId], joint.FirstEnd);
            var secondPoints = EndFacePoints(bodies[second.StableId], joint.SecondEnd);
            var coincident = SamePointSet(firstPoints, secondPoints);
            if (!coincident)
                d.Add($"structural-miter-interface-noncoincident:{joint.Name}: member end loops do not share one weld surface");
            var onDeclaredPlane = firstPoints.Concat(secondPoints).All(point => Math.Abs(normal.Dot(point - origin)) <= Tol);
            if (!onDeclaredPlane)
                d.Add($"structural-miter-interface-off-plane:{joint.Name}: materialized end loops do not lie on the declared shared interface plane");
            var firstSign = Math.Sign(normal.Dot(awayFirst));
            var secondSign = Math.Sign(normal.Dot(awaySecond));
            var firstContained = AllVerticesInRetainedHalfSpace(bodies[first.StableId], origin, normal, firstSign);
            var secondContained = AllVerticesInRetainedHalfSpace(bodies[second.StableId], origin, normal, secondSign);
            var opposed = selection.RetainedHalfSpacesOpposed && firstSign == -secondSign && firstContained && secondContained;
            if (!opposed)
                d.Add($"structural-miter-interface-overlap:{joint.Name}: material is not bounded to opposite retained half-spaces");
            result.Add(new("interface:" + joint.Name, joint.StableId, "SharedCutSurface", first.StableId, firstInterface,
                second.StableId, secondInterface, [origin.X, origin.Y, origin.Z], [normal.X, normal.Y, normal.Z], coincident,
                selection.SelectedStrategy, selection.UtilityScore, opposed, opposed ? 0d : null, selection.RejectedCandidates));
        }
        return result;

        static IReadOnlyList<Point3D> EndFacePoints(BrepBody body, StructuralEnd end)
        {
            var caps = body.Topology.Faces.OrderBy(x => x.Id.Value).Take(2).ToArray();
            var face = caps[end == StructuralEnd.Start ? 0 : 1];
            return face.LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
                .Select(id => body.Topology.GetCoedge(id))
                .Select(coedge => body.Topology.GetEdge(coedge.EdgeId))
                .Select(edge => body.TryGetVertexPoint(edge.StartVertexId, out var point) ? point : default)
                .ToArray();
        }
        static bool SamePointSet(IReadOnlyList<Point3D> a, IReadOnlyList<Point3D> b) =>
            a.Count == b.Count && a.All(point => b.Any(other => (point - other).Length <= Tol));
        static bool AllVerticesInRetainedHalfSpace(BrepBody body, Point3D origin, Vector3D normal, int retainedSign) =>
            retainedSign != 0 && body.Topology.Vertices.All(vertex =>
                body.TryGetVertexPoint(vertex.Id, out var point) && retainedSign * normal.Dot(point - origin) >= -Tol);
    }

    private static double ProjectedHalfExtent(StructuralSectionAir section, Vector3D primaryDirection, Vector3D targetDirection)
    {
        // X2 butt qualification is intentionally orthogonal and conservative: the through member's
        // circumscribed half-size is the deterministic terminating offset.
        return Math.Abs(primaryDirection.Dot(targetDirection)) > 1e-6 ? 0 : Math.Max(section.Width, section.Height)/2;
    }

    private static IReadOnlyList<StructuralCutListEntry> BuildCutList(Parsed p, IReadOnlyList<StructuralMemberIr> members)
    {
        return members.GroupBy(x => $"{x.SectionStableId}|{x.Material}|{Q(x.RawLength)}|{Q(x.FinishedLength)}|{TreatmentKey(x.StartTreatment)}|{TreatmentKey(x.EndTreatment)}", StringComparer.Ordinal)
            .OrderBy(g=>g.Key,StringComparer.Ordinal).Select((g,i)=>new StructuralCutListEntry($"cut-group:{i+1:D3}",g.First().SectionStableId,p.Sections.Single(x=>x.StableId==g.First().SectionStableId).Kind.ToString(),g.First().Material,g.Count(),g.First().RawLength,g.First().FinishedLength,TreatmentKey(g.First().StartTreatment),TreatmentKey(g.First().EndTreatment),g.Sum(x=>x.MassKilograms),g.Select(x=>x.StableId).Order(StringComparer.Ordinal).ToArray())).ToArray();
    }
    private static string TreatmentKey(StructuralEndTreatmentIr x)=>x.Kind=="Miter"?$"Miter {x.AngleDegrees:G6}deg":x.Kind;
    private static string Treatment(StructuralEndTreatmentIr x)=>x.Kind=="Miter"?$"Miter {x.AngleDegrees:G6}deg":x.Kind=="TrimmedTo"?$"TrimmedTo {x.RelatedMember}":x.Kind;
    private static bool IsEnclosed(StructuralMemberIr x)=>x.Body.Topology.Edges.All(e=>x.Body.Topology.Coedges.Count(c=>c.EdgeId==e.Id)==2);
    private static (Direction3D X,Direction3D Y)? MemberFrame(Direction3D z,Vector3D authored,List<string>d,string member)
    {
        var hint=authored.Length>Tol?authored:new[]{new Vector3D(0,0,1),new Vector3D(0,1,0),new Vector3D(1,0,0)}.OrderBy(x=>Math.Abs(x.Dot(z.ToVector()))).First();
        var projected=hint-z.ToVector()*hint.Dot(z.ToVector()); if(!Direction3D.TryCreate(projected,out var y)){d.Add($"structural-member-orientation-parallel:{member}");return null;} var x=Direction3D.Create(y.ToVector().Cross(z.ToVector())); y=Direction3D.Create(z.ToVector().Cross(x.ToVector()));return(x,y);
    }
    private static bool TryEndpoint(string text,out string member,out StructuralEnd end){var parts=text.Split('.',StringSplitOptions.TrimEntries);member=parts.ElementAtOrDefault(0)??"";end=default;return parts.Length==2&&Enum.TryParse(parts[1],out end);}
    private static string Field(string body,string name,bool required=true){var m=Regex.Match(body,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>[^;\r\n]+)\s*;",RegexOptions.CultureInvariant);return m.Success?m.Groups["v"].Value.Trim():required?"":"";}
    private static double LengthField(string body,string name,List<string>d,string owner)=>ParseLength(Field(body,name),d,owner+"."+name);
    private static double ParseLength(string value,List<string>d,string owner){var m=Regex.Match(value,@"^(?<n>[+-]?(?:\d+(?:\.\d*)?|\.\d+))\s*mm$",RegexOptions.CultureInvariant);if(!m.Success||!double.TryParse(m.Groups["n"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)){d.Add($"structural-length-invalid:{owner}: '{value}'");return 0;}return n;}
    private static Vector3D Orientation(string value,List<string>d,string owner)=>value.Trim().Trim('"') switch{"" or "Auto"=>Vector3D.Zero,"UpX"=>new(1,0,0),"UpY"=>new(0,1,0),"UpZ"=>new(0,0,1),var raw=>Vector(raw.Trim('[',']'),d,"orientation:"+owner)};
    private static Vector3D Vector(string value,List<string>d,string owner){var parts=value.Split(',',StringSplitOptions.TrimEntries);if(parts.Length!=3||parts.Any(x=>!TryScalar(x,out _))){d.Add($"structural-vector-invalid:{owner}: '{value}'");return Vector3D.Zero;}return new(Scalar(parts[0]),Scalar(parts[1]),Scalar(parts[2]));}
    private static bool TryScalar(string expression,out double value)
    {
        value=0;var normalized=expression.Replace("mm",string.Empty,StringComparison.Ordinal).Trim();var tokens=Regex.Split(normalized,@"\s*([*/])\s*",RegexOptions.CultureInvariant).Where(x=>x.Length>0).ToArray();if(tokens.Length==0||!double.TryParse(tokens[0],NumberStyles.Float,CultureInfo.InvariantCulture,out value))return false;for(var i=1;i+1<tokens.Length;i+=2){if(!double.TryParse(tokens[i+1],NumberStyles.Float,CultureInfo.InvariantCulture,out var operand))return false;if(tokens[i]=="*")value*=operand;else if(tokens[i]=="/"&&Math.Abs(operand)>Tol)value/=operand;else return false;}return tokens.Length%2==1&&double.IsFinite(value);
    }
    private static double Scalar(string expression){TryScalar(expression,out var value);return value;}
    private static Point3D Point(Vector3D v)=>new(v.X,v.Y,v.Z);
    private static void Duplicates(IEnumerable<string> values,string kind,List<string>d){foreach(var g in values.GroupBy(x=>x,StringComparer.Ordinal).Where(x=>x.Count()>1))d.Add($"structural-duplicate-{kind}:{g.Key}");}
    private static long Q(double x)=>(long)Math.Round(x*1_000_000,MidpointRounding.AwayFromZero);
    private readonly record struct Block(string Name,string Body);
    private static string Cell(IReadOnlyDictionary<string,string> row,string name,bool required=true)=>row.TryGetValue(name,out var value)?value.Trim().Trim('"'):required?"":"";
    private static IReadOnlyList<IReadOnlyDictionary<string,string>> TableRows(string source,string rowType,List<string>d)
    {
        var rows=new List<IReadOnlyDictionary<string,string>>();var pattern=$@"\bStatic\s+Table\s+(?<name>[A-Za-z_]\w*)\s*:\s*{Regex.Escape(rowType)}(?:\s+Key\s*:\s*[A-Za-z_]\w*)?\s*\{{";
        foreach(Match match in Regex.Matches(source,pattern,RegexOptions.CultureInvariant)){var open=match.Index+match.Length-1;var close=Matching(source,open,'{','}');if(close<0){d.Add($"structural-table-malformed:{match.Groups["name"].Value}");continue;}var body=source[(open+1)..close];var columns=new Dictionary<string,string[]>(StringComparer.Ordinal);foreach(Match column in Regex.Matches(body,@"\b(?<name>[A-Za-z_]\w*)\s*:\s*\[",RegexOptions.CultureInvariant)){var arrayOpen=column.Index+column.Length-1;var arrayClose=Matching(body,arrayOpen,'[',']');if(arrayClose<0)continue;var values=body[(arrayOpen+1)..arrayClose].Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);columns[column.Groups["name"].Value]=values;}var lengths=columns.Values.Select(x=>x.Length).Distinct().ToArray();if(lengths.Length!=1){d.Add($"structural-table-unequal-column-length:{match.Groups["name"].Value}");continue;}for(var i=0;i<lengths[0];i++)rows.Add(columns.ToDictionary(x=>x.Key,x=>x.Value[i],StringComparer.Ordinal));}
        return rows;
    }
    private static int Matching(string source,int open,char opening,char closing){var depth=0;for(var i=open;i<source.Length;i++){if(source[i]==opening)depth++;else if(source[i]==closing&&--depth==0)return i;}return-1;}
    private static IReadOnlyList<Block> Blocks(string source,string keyword)
    {
        var result=new List<Block>();foreach(Match m in Regex.Matches(source,$@"\b{keyword}\s+(?<n>[A-Za-z_]\w*)\s*\{{",RegexOptions.CultureInvariant)){var open=m.Index+m.Length-1;var depth=0;var close=-1;for(var i=open;i<source.Length;i++){if(source[i]=='{')depth++;else if(source[i]=='}'&&--depth==0){close=i;break;}}if(close>open)result.Add(new(m.Groups["n"].Value,source[(open+1)..close]));}return result;
    }
}

internal static class StructuralMemberBrepEmitter
{
    public static KernelResult<BrepBody> Emit(string memberName, StructuralSectionAir section, Point3D origin, Direction3D x, Direction3D y, Direction3D z, StructuralEndPlane start, StructuralEndPlane end)
    {
        if(section.Kind is StructuralSectionKind.RoundTube or StructuralSectionKind.RoundBar)
        {
            if(Math.Abs(start.XCoefficient)>1e-12||Math.Abs(start.YCoefficient)>1e-12||Math.Abs(end.XCoefficient)>1e-12||Math.Abs(end.YCoefficient)>1e-12)
                return Fail("structural-miter-round-section-unsupported: X2 admits miter geometry for polygonal sections only");
            var profileLoops = new List<LineArcProfileLoop2D> { new([new LineArcFullCircle2D((0,0), section.Width/2)], false) };
            if (section.Kind == StructuralSectionKind.RoundTube) profileLoops.Add(new([new LineArcFullCircle2D((0,0), section.Width/2-section.Thickness)], true));
            var frame = new ConstructionPlane("structural-frame", "member-orientation", origin, x, y, z, "structural-air", "explicit-member-frame");
            var plan = ProfileExtrusionBRepPlanner.TryPlan(new(profileLoops, end.CenterDepth-start.CenterDepth, frame, start.CenterDepth, end.CenterDepth));
            if (!plan.Succeeded || plan.Plan is null) return Fail("structural-round-section-plan-failed: " + string.Join("; ", plan.Diagnostics));
            var materialized = ProfileExtrusionBRepMaterializer.TryMaterialize(plan.Plan);
            return materialized.Succeeded && materialized.Body is not null ? KernelResult<BrepBody>.Success(materialized.Body) : Fail("structural-round-section-materialization-failed: " + string.Join("; ", materialized.Diagnostics));
        }
        var loops=Loops(section); var b=new TopologyBuilder();var geometry=new BrepGeometryStore();var bindings=new BrepBindingModel();var points=new Dictionary<VertexId,Point3D>();
        var rings=new List<(VertexId[] S,VertexId[] E,EdgeId[] SE,EdgeId[] EE,EdgeId[] Span,bool Hole)>();
        foreach(var loop in loops)
        {
            var sv=new VertexId[loop.Points.Count];var ev=new VertexId[loop.Points.Count];var se=new EdgeId[loop.Points.Count];var ee=new EdgeId[loop.Points.Count];var span=new EdgeId[loop.Points.Count];
            for(var i=0;i<loop.Points.Count;i++){sv[i]=b.AddVertex();ev[i]=b.AddVertex();var p=loop.Points[i];points[sv[i]]=World(p,start.CenterDepth+start.XCoefficient*p.X+start.YCoefficient*p.Y);points[ev[i]]=World(p,end.CenterDepth+end.XCoefficient*p.X+end.YCoefficient*p.Y);}
            for(var i=0;i<loop.Points.Count;i++){var n=(i+1)%loop.Points.Count;se[i]=b.AddEdge(sv[i],sv[n]);ee[i]=b.AddEdge(ev[i],ev[n]);span[i]=b.AddEdge(sv[i],ev[i]);}
            rings.Add((sv,ev,se,ee,span,loop.Hole));
        }
        var faces=new List<FaceId>();
        var startLoops=rings.Select(r=>AddLoop(b,r.SE.Select(Use.F).ToArray())).ToArray();var endLoops=rings.Select(r=>AddLoop(b,r.EE.Select(Use.R).ToArray())).ToArray();
        faces.Add(b.AddFace(startLoops));faces.Add(b.AddFace(endLoops));
        foreach(var ring in rings)for(var i=0;i<ring.SE.Length;i++){var n=(i+1)%ring.SE.Length;faces.Add(AddFace(b,[Use.F(ring.SE[i]),Use.F(ring.Span[n]),Use.R(ring.EE[i]),Use.R(ring.Span[i])]));}
        var shell=b.AddShell(faces);b.AddBody([shell]);
        var curve=1;foreach(var edge in b.Model.Edges.OrderBy(e=>e.Id.Value)){var a=points[edge.StartVertexId];var e=points[edge.EndVertexId];var cid=new CurveGeometryId(curve++);geometry.AddCurve(cid,CurveGeometry.FromLine(new Line3Curve(a,Direction3D.Create(e-a))));bindings.AddEdgeBinding(new(edge.Id,cid,new ParameterInterval(0,(e-a).Length)));}
        var surface=1;BindPlane(faces[0],points[rings[0].S[0]],-z.ToVector()+x.ToVector()*start.XCoefficient+y.ToVector()*start.YCoefficient,x.ToVector());BindPlane(faces[1],points[rings[0].E[0]],z.ToVector()-x.ToVector()*end.XCoefficient-y.ToVector()*end.YCoefficient,x.ToVector());
        var fi=2;foreach(var ring in rings)for(var i=0;i<ring.SE.Length;i++){var n=(i+1)%ring.SE.Length;var a=points[ring.S[i]];var edge=points[ring.S[n]]-a;var longitudinal=points[ring.E[i]]-a;var normal=edge.Cross(longitudinal);if(ring.Hole)normal=-normal;BindPlane(faces[fi++],a,normal,edge);}
        var body=new BrepBody(b.Model,geometry,bindings,points);
        var capDiagnostics=ValidateCapSupportPlanes(memberName,body);
        if(capDiagnostics.Count>0)return KernelResult<BrepBody>.Failure(capDiagnostics.Select(message=>new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed,KernelDiagnosticSeverity.Error,message,"StructuralMemberBrepEmitter")).ToArray());
        var validation=BrepBindingValidator.Validate(body,true);return validation.IsSuccess?KernelResult<BrepBody>.Success(body):KernelResult<BrepBody>.Failure(validation.Diagnostics);
        Point3D World((double X,double Y)p,double depth)=>origin+x.ToVector()*p.X+y.ToVector()*p.Y+z.ToVector()*depth;
        void BindPlane(FaceId face,Point3D o,Vector3D normal,Vector3D u){var sid=new SurfaceGeometryId(surface++);geometry.AddSurface(sid,SurfaceGeometry.FromPlane(new PlaneSurface(o,Direction3D.Create(normal),Direction3D.Create(u))));bindings.AddFaceBinding(new(face,sid));}
    }

    internal static IReadOnlyList<string> ValidateCapSupportPlanes(string memberName, BrepBody body)
    {
        var diagnostics = new List<string>();
        var caps = body.Topology.Faces.OrderBy(face => face.Id.Value).Take(2).ToArray();
        for (var index = 0; index < caps.Length; index++)
        {
            var cap = caps[index];
            if (!body.TryGetFaceSurfaceGeometry(cap.Id, out var surface) || surface?.Plane is not PlaneSurface plane)
                continue;

            var maximumDeviation = cap.LoopIds
                .SelectMany(loopId => body.Topology.GetLoop(loopId).CoedgeIds)
                .Select(coedgeId => body.Topology.GetCoedge(coedgeId))
                .Select(coedge => body.Topology.GetEdge(coedge.EdgeId))
                .SelectMany(edge => new[] { edge.StartVertexId, edge.EndVertexId })
                .Distinct()
                .Select(vertexId => body.TryGetVertexPoint(vertexId, out var point)
                    ? Math.Abs((point - plane.Origin).Dot(plane.Normal.ToVector()))
                    : double.PositiveInfinity)
                .DefaultIfEmpty(double.PositiveInfinity)
                .Max();
            if (maximumDeviation <= 1e-7)
                continue;

            var end = index == 0 ? "Start" : "End";
            var bindingFormula = index == 0
                ? "normal = -Z + X*Start.XCoefficient + Y*Start.YCoefficient"
                : "normal = +Z - X*End.XCoefficient - Y*End.YCoefficient";
            var deviation = maximumDeviation.ToString("G9", CultureInfo.InvariantCulture);
            diagnostics.Add(
                $"structural-member-cap-support-plane-mismatch:{memberName}:{end}: cap loop is {deviation} mm off its bound PlaneSurface. " +
                "STEP resolves vertices from incident support planes; exporting this body can turn mirror-opposed picture-frame miters into a parallelogram. " +
                $"Bind the cap with `{bindingFormula}` and keep every cap-loop vertex on that plane.");
        }
        return diagnostics;
    }
    private static IReadOnlyList<(IReadOnlyList<(double X,double Y)> Points,bool Hole)> Loops(StructuralSectionAir s)
    {
        static (double,double)[] Rect(double w,double h)=>[(-w/2,-h/2),(w/2,-h/2),(w/2,h/2),(-w/2,h/2)];
        if(s.Kind is StructuralSectionKind.SquareTube or StructuralSectionKind.RectangularTube)return[(Rect(s.Width,s.Height),false),(Rect(s.Width-2*s.Thickness,s.Height-2*s.Thickness).Reverse().ToArray(),true)];
        if(s.Kind==StructuralSectionKind.Angle)return[([( -s.Width/2,-s.Height/2),(s.Width/2,-s.Height/2),(s.Width/2,-s.Height/2+s.Thickness),(-s.Width/2+s.Thickness,-s.Height/2+s.Thickness),(-s.Width/2+s.Thickness,s.Height/2),(-s.Width/2,s.Height/2)],false)];
        if(s.Kind==StructuralSectionKind.FlatBar)return[(Rect(s.Width,s.Height),false)];
        var n=32;var outer=Enumerable.Range(0,n).Select(i=>(s.Width/2*Math.Cos(2*Math.PI*i/n),s.Width/2*Math.Sin(2*Math.PI*i/n))).ToArray();if(s.Kind==StructuralSectionKind.RoundBar)return[(outer,false)];var inner=Enumerable.Range(0,n).Reverse().Select(i=>((s.Width/2-s.Thickness)*Math.Cos(2*Math.PI*i/n),(s.Width/2-s.Thickness)*Math.Sin(2*Math.PI*i/n))).ToArray();return[(outer,false),(inner,true)];
    }
    private static LoopId AddLoop(TopologyBuilder b,IReadOnlyList<Use> uses){var loop=b.AllocateLoopId();var ids=uses.Select(_=>b.AllocateCoedgeId()).ToArray();for(var i=0;i<ids.Length;i++)b.AddCoedge(new(ids[i],uses[i].Edge,loop,ids[(i+1)%ids.Length],ids[(i+ids.Length-1)%ids.Length],uses[i].Reverse));b.AddLoop(new Loop(loop,ids));return loop;}
    private static FaceId AddFace(TopologyBuilder b,IReadOnlyList<Use> uses)=>b.AddFace([AddLoop(b,uses)]);
    private readonly record struct Use(EdgeId Edge,bool Reverse){public static Use F(EdgeId e)=>new(e,false);public static Use R(EdgeId e)=>new(e,true);}
    private static KernelResult<BrepBody> Fail(string message)=>KernelResult<BrepBody>.Failure([new(KernelDiagnosticCode.ValidationFailed,KernelDiagnosticSeverity.Error,message,"StructuralMemberBrepEmitter")]);
}
