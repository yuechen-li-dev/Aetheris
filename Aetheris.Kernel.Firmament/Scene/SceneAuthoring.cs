using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Scene;

[FirmamentConstruct("Scene", "Scene", Context = "Document", Entry = "Scene Factory { Units: m; }", Description = "Spatial composition above independently engineered Parts and Assemblies. Placements imply no mechanical relationship.")]
[FirmamentField("Units", "Units", FirmamentSchemaValueKind.Choice, Required = true, Choices = ["m", "mm"])]
public static class SceneDeclaration { }

[FirmamentConstruct("Room", "Room", Context = "Scene", Entry = "Room warehouse { Size: [30m, 18m, 7m]; }", Description = "A rectangular spatial enclosure with stable named floor, ceiling and wall boundaries.")]
[FirmamentField("Size", "Size", FirmamentSchemaValueKind.Vector, Required = true, Unit = FirmamentUnitKind.Length)]
[FirmamentField("At", "At", FirmamentSchemaValueKind.Vector, Unit = FirmamentUnitKind.Length)]
[FirmamentField("Thickness", "Thickness", FirmamentSchemaValueKind.Length, Unit = FirmamentUnitKind.Length, Default = "100mm")]
[FirmamentField("Appearance", "Appearance", FirmamentSchemaValueKind.ConstructReference)]
[FirmamentField("FloorAppearance", "FloorAppearance", FirmamentSchemaValueKind.ConstructReference)]
[FirmamentField("CeilingAppearance", "CeilingAppearance", FirmamentSchemaValueKind.ConstructReference)]
[FirmamentOutput("floor", "floor", "SceneBoundary", SourceAddressable = true)]
[FirmamentOutput("ceiling", "ceiling", "SceneBoundary", SourceAddressable = true)]
[FirmamentOutput("northWall", "northWall", "SceneBoundary", SourceAddressable = true)]
[FirmamentOutput("southWall", "southWall", "SceneBoundary", SourceAddressable = true)]
[FirmamentOutput("eastWall", "eastWall", "SceneBoundary", SourceAddressable = true)]
[FirmamentOutput("westWall", "westWall", "SceneBoundary", SourceAddressable = true)]
public static class RoomDeclaration { }

[FirmamentConstruct("Door", "Door", Context = "Scene", Entry = "Door loadingBay { On: warehouse.southWall; Width: 4m; Height: 4.5m; Along: 6m; }", Description = "Room-owned rectangular aperture measured from the wall's minimum X or Y; bottom lies on the floor.")]
[FirmamentField("On", "On", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("Width", "Width", FirmamentSchemaValueKind.Length, Required = true, Unit = FirmamentUnitKind.Length)]
[FirmamentField("Height", "Height", FirmamentSchemaValueKind.Length, Required = true, Unit = FirmamentUnitKind.Length)]
[FirmamentField("Along", "Along", FirmamentSchemaValueKind.Length, Required = true, Unit = FirmamentUnitKind.Length)]
public static class DoorDeclaration { }

[FirmamentConstruct("Window", "Window", Context = "Scene", Entry = "Window officeGlass { On: warehouse.eastWall; Width: 6m; Height: 2.5m; Sill: 1m; Along: 4m; }", Description = "Room-owned rectangular aperture with optional inset frame and glazing; all geometry follows the opening dimensions.")]
[FirmamentField("On", "On", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("Width", "Width", FirmamentSchemaValueKind.Length, Required = true, Unit = FirmamentUnitKind.Length)]
[FirmamentField("Height", "Height", FirmamentSchemaValueKind.Length, Required = true, Unit = FirmamentUnitKind.Length)]
[FirmamentField("Along", "Along", FirmamentSchemaValueKind.Length, Required = true, Unit = FirmamentUnitKind.Length)]
[FirmamentField("Sill", "Sill", FirmamentSchemaValueKind.Length, Required = true, Unit = FirmamentUnitKind.Length)]
[FirmamentField("GlazingAppearance", "GlazingAppearance", FirmamentSchemaValueKind.ConstructReference)]
[FirmamentField("FrameAppearance", "FrameAppearance", FirmamentSchemaValueKind.ConstructReference)]
[FirmamentField("FrameWidth", "FrameWidth", FirmamentSchemaValueKind.Length, Unit = FirmamentUnitKind.Length, Default = "40mm")]
[FirmamentField("GlassThickness", "GlassThickness", FirmamentSchemaValueKind.Length, Unit = FirmamentUnitKind.Length, Default = "6mm")]
public static class WindowDeclaration { }

[FirmamentConstruct("SceneCamera", "Camera", Context = "Scene", Entry = "Camera hero { Position: [35m, -15m, 20m]; LookAt: [15m, 9m, 1m]; Fov: 45deg; }", Description = "Presentation state in a Z-up world; independent of engineering geometry.")]
[FirmamentField("Position", "Position", FirmamentSchemaValueKind.Vector, Required = true, Unit = FirmamentUnitKind.Length)]
[FirmamentField("LookAt", "LookAt", FirmamentSchemaValueKind.Vector, Required = true, Unit = FirmamentUnitKind.Length)]
[FirmamentField("Fov", "Fov", FirmamentSchemaValueKind.Angle, Required = true, Unit = FirmamentUnitKind.Angle)]
public static class SceneCameraDeclaration { }

public sealed record SceneRoom(string Name, double[] SizeMm, double[] AtMm, double ThicknessMm, string? Appearance, SemanticSourceSpan Span,
    string? FloorAppearance = null, string? CeilingAppearance = null);
public sealed record SceneWindowFinish(string? GlazingAppearance, string? FrameAppearance, double FrameWidthMm, double GlassThicknessMm);
public sealed record SceneOpening(string Name, string Kind, string Boundary, double WidthMm, double HeightMm, double AlongMm, double SillMm, SemanticSourceSpan Span,
    SceneWindowFinish? Finish = null);
public sealed record SceneCamera(string Name, double[] PositionMm, double[] LookAtMm, double FovDegrees, SemanticSourceSpan Span)
{
    public double[] WorldTransform()
    {
        var z = new Vector3D(PositionMm[0]-LookAtMm[0], PositionMm[1]-LookAtMm[1], PositionMm[2]-LookAtMm[2]);
        if (!z.TryNormalize(out z)) throw new InvalidOperationException("scene-camera-degenerate");
        var up = Math.Abs(z.Z) > .999 ? new Vector3D(0,1,0) : new Vector3D(0,0,1);
        var x = up.Cross(z); x.TryNormalize(out x); var y = z.Cross(x);
        return [x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,PositionMm[0],PositionMm[1],PositionMm[2],1];
    }
}
public sealed record SceneOccurrenceSource(string Path, string Kind, string Definition, AssemblyFrameTransformSource Placement,
    string From, string? Appearance, SemanticSourceSpan Span, string? PatternKey = null);
public sealed record SceneLayoutFrame(string Identity, AssemblyFrameTransformSource Transform);
public sealed record SceneSource(string Name, string Units, string DefinitionSource, string SourceIdentity,
    IReadOnlyList<SceneRoom> Rooms, IReadOnlyList<SceneOpening> Openings, IReadOnlyList<SceneCamera> Cameras,
    IReadOnlyList<SceneOccurrenceSource> Occurrences, AssemblyAppearanceCatalog Looks,
    IReadOnlyList<FirmamentV2CanonicalPatternDecl> Patterns, IReadOnlyList<SceneLayoutFrame> LayoutFrames);
public sealed record SceneParseResult(SceneSource? Source, IReadOnlyList<AssemblyDiagnostic> Diagnostics)
{ public bool IsSuccess => Source is not null && Diagnostics.All(d => d.Severity != AssemblyDiagnosticSeverity.Error); }

/// <summary>Scene owns spatial syntax. Engineering definitions and frame math stay with their existing producers.</summary>
public static class SceneAuthoring
{
    public static bool HasRoot(string source)
    {
        var tokens=FirmamentLanguageAnalysisService.Lex(source).Where(t => t.Shape != "comment").ToArray();
        var depth=0;
        for(var i=0;i+1<tokens.Length;i++)
        {
            if (depth == 0 && tokens[i].Shape == "identifier" && tokens[i].Text == "Scene" && tokens[i+1].Shape == "identifier") return true;
            if (tokens[i].Text == "{") depth++; else if (tokens[i].Text == "}") depth--;
        }
        return false;
    }

    public static SceneParseResult Parse(string input, string sourceIdentity = "<memory>")
    {
        var diagnostics = new List<AssemblyDiagnostic>();
        try { return ParseCore(input, sourceIdentity, diagnostics); }
        catch (SceneSyntaxException ex) { diagnostics.Add(new(ex.Code, ex.Message)); return new(null, diagnostics); }
    }

    private static SceneParseResult ParseCore(string input, string identity, List<AssemblyDiagnostic> diagnostics)
    {
        var normalized = FirmamentSourceSpelling.Normalize(input);
        // Comments are erased at their lexical spans, preserving strings and positions.
        var chars = normalized.ToCharArray();
        foreach (var t in FirmamentLanguageAnalysisService.Lex(normalized).Where(t => t.Shape == "comment"))
            Array.Fill(chars, ' ', t.Start, t.Length);
        var text = new string(chars);
        var authoredText = text;
        if (Regex.IsMatch(text,@"<\s*/?\s*Group\b")) Fail("scene-group-reserved","Pattern groups are generated by the shared expander; author Pattern over a keyed Set.");
        var original = new Reader(text, identity);
        var definitionEnd = 0;
        var geometryDeclarations = new List<string>();
        var conceptNames = new List<string>();
        while (!original.End && original.Peek != "Scene")
        {
            var kind = original.Peek;
            if (kind is not ("Template" or "Record" or "Static" or "Function" or "Appearance" or "Material" or "Concept" or "Linear" or "Mirrored"))
                Fail("scene-declaration-unsupported", $"Unsupported Scene module declaration '{kind}'. Use AssemblyFile for independently compiled assemblies.");
            var declarationStart = original.Position;
            original.SkipDeclaration(); definitionEnd = original.Position;
            var declaration = text[declarationStart..definitionEnd];
            if (kind is "Linear" or "Mirrored" && !Regex.IsMatch(declaration,@"^(?:Linear|Mirrored)\s+Sites\b"))
                Fail("scene-declaration-unsupported", "Scene admits only the shared Linear Sites and Mirrored Sites recipes.");
            if (kind == "Concept")
            {
                var concept = Regex.Match(declaration,@"^Concept\s+Struct\s+(?<name>\w+)\s*\{");
                if (!concept.Success) Fail("scene-concept-layout-unsupported", "Scene admits existing Concept Struct Plane/Axis/DatumFrame layout guides.");
                conceptNames.Add(concept.Groups["name"].Value);
            }
            // Scene keyed Sets and preview looks are composition inputs, not geometry inputs.
            if (kind is not ("Appearance" or "Material" or "Concept" or "Linear" or "Mirrored") && !Regex.IsMatch(declaration,@"^Static\s+\w+\s*:\s*Set\s*<"))
                geometryDeclarations.Add(declaration);
        }
        if (original.End) Fail("scene-root-missing", "Expected one Scene root.");
        original.Expect("Scene"); var name = original.Identifier();
        original.Block(); original.Optional(";");
        if (!original.End) Fail("scene-multiple-roots", "A Scene document requires exactly one Scene root and no trailing declarations.");
        var definitionSource = string.Join("\n",geometryDeclarations);
        var layout = AssemblyDatumAuthoring.ParseLayouts(text,identity,diagnostics,out _);
        // Concept layouts are erased design scaffolding. The existing datum parser owns
        // their members, units, bases, derivation and cycle diagnostics.
        if (diagnostics.Any(d => d.Severity == AssemblyDiagnosticSeverity.Error)) return new(null,diagnostics);
        if (conceptNames.Any(n => !layout.Any(d => d.Identity.StartsWith(n+".",StringComparison.Ordinal))))
            Fail("scene-concept-layout-unsupported", "Each Scene Concept Struct requires an admitted Plane or Axis and optional DatumFrame members.");
        var errors = new List<string>();
        var expanded = CanonicalStaticAuthoring.ExpandAssemblyPatterns(text, errors, spatial: true);
        foreach (var error in errors) diagnostics.Add(new("scene-pattern-invalid", error));
        if (expanded is null || errors.Count != 0) return new(null, diagnostics);
        text = expanded.Source;
        var looks = AssemblyAppearanceAuthoring.Parse(ref text, diagnostics, out _);
        var reader = new Reader(text, identity);
        while (!reader.End && reader.Peek != "Scene") reader.SkipDeclaration();
        reader.Expect("Scene"); reader.Identifier(); var sceneBody = reader.Block();
        var body = new Reader(sceneBody.Text, identity, sceneBody.Start);
        var units = ""; var rooms = new List<SceneRoom>(); var openings = new List<SceneOpening>();
        var cameras = new List<SceneCamera>(); var occurrences = new List<SceneOccurrenceSource>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var patterns = expanded.Document?.Patterns ?? [];
        while (!body.End)
        {
            if (body.Optional(";")) continue;
            if (body.Peek == "Units")
            {
                body.Take(); body.Expect(":"); var value = body.Take(); body.Optional(";");
                if (units.Length != 0 || value is not ("m" or "mm")) Fail("scene-units-invalid", "Declare units: m or units: mm exactly once; coordinates still require suffixes.");
                units = value; continue;
            }
            if (body.Peek == "<") { ReadOccurrence(body, name); continue; }
            var start = body.Position; var kind = body.Take(); var id = body.Identifier(); var block = body.Block();
            if (!names.Add(id)) Fail("scene-name-collision", $"Scene name '{id}' is duplicated.");
            var span = AuthoredSpan(kind,id);
            var f = Fields(block.Text, identity);
            switch (kind)
            {
                case "Room":
                    Check(f, "Size At Thickness Appearance FloorAppearance CeilingAppearance", "Size");
                    var size = Vector(f["Size"]); var at = Vector(f.GetValueOrDefault("At", "[0mm,0mm,0mm]"));
                    var thickness = Length(f.GetValueOrDefault("Thickness", "100mm"));
                    if (size.Any(v => v <= 0) || thickness <= 0) Fail("scene-room-size-invalid", "Room dimensions and thickness must be positive.");
                    rooms.Add(new(id,size,at,thickness,f.GetValueOrDefault("Appearance"),span,
                        f.GetValueOrDefault("FloorAppearance"),f.GetValueOrDefault("CeilingAppearance"))); break;
                case "Door": case "Window":
                    Check(f, kind == "Door" ? "On Width Height Along" : "On Width Height Along Sill GlazingAppearance FrameAppearance FrameWidth GlassThickness", kind == "Door" ? "On Width Height Along" : "On Width Height Along Sill");
                    SceneWindowFinish? finish = null;
                    if (kind == "Window" && f.Keys.Any(k => k is "GlazingAppearance" or "FrameAppearance" or "FrameWidth" or "GlassThickness"))
                    {
                        var glazing = f.GetValueOrDefault("GlazingAppearance"); var frame = f.GetValueOrDefault("FrameAppearance");
                        if ((glazing is null && frame is null) || (f.ContainsKey("FrameWidth") && frame is null) || (f.ContainsKey("GlassThickness") && glazing is null))
                            Fail("scene-window-finish-invalid", "Window finish dimensions require their corresponding glazingAppearance or frameAppearance.");
                        var frameWidth = frame is null ? 0 : Length(f.GetValueOrDefault("FrameWidth", "40mm"));
                        var glassThickness = Length(f.GetValueOrDefault("GlassThickness", "6mm"));
                        if ((frame is not null && frameWidth <= 0) || glassThickness <= 0 || frameWidth * 2 >= Math.Min(Length(f["Width"]),Length(f["Height"])))
                            Fail("scene-window-finish-invalid", "Window frame must leave a positive clear pane; frame width and glass thickness must be positive.");
                        finish = new(glazing,frame,frameWidth,glassThickness);
                    }
                    openings.Add(new(id,kind,f["On"],Length(f["Width"]),Length(f["Height"]),Length(f["Along"]),kind == "Window" ? Length(f["Sill"]) : 0,span,finish)); break;
                case "Camera":
                    Check(f,"Position LookAt Fov","Position LookAt Fov");
                    var position = Vector(f["Position"]); var target = Vector(f["LookAt"]); var fov = Angle(f["Fov"]);
                    if (fov <= 0 || fov >= 180 || position.SequenceEqual(target)) Fail("scene-camera-invalid", "Camera requires distinct position/lookAt and 0 < fov < 180deg.");
                    cameras.Add(new(id,position,target,fov,span)); break;
                default: Fail("scene-construct-unsupported", $"'{kind}' is not an X0 Scene construct; mechanical relationships belong to an Assembly."); break;
            }
        }
        if (units.Length == 0) Fail("scene-units-required", "Scene requires units: m or units: mm.");
        foreach (var opening in openings)
        {
            var path = opening.Boundary.Split('.');
            var room = path.Length == 2 ? rooms.SingleOrDefault(r => r.Name == path[0]) : null;
            if (room is null || path[1] is not ("northWall" or "southWall" or "eastWall" or "westWall"))
                Fail("scene-opening-boundary-invalid", $"Opening '{opening.Name}' requires a declared Room wall, e.g. warehouse.southWall.");
            var extent = path[1] is "northWall" or "southWall" ? room.SizeMm[0] : room.SizeMm[1];
            if (opening.WidthMm <= 0 || opening.HeightMm <= 0 || opening.AlongMm < 0 || opening.SillMm < 0
                || opening.AlongMm+opening.WidthMm > extent || opening.SillMm+opening.HeightMm > room.SizeMm[2])
                Fail("scene-opening-outside-wall", $"Opening '{opening.Name}' must lie inside its wall bounds.");
            if (opening.Finish is { GlazingAppearance: not null } windowFinish && windowFinish.GlassThicknessMm > room.ThicknessMm)
                Fail("scene-window-finish-invalid", "Window glass thickness must fit inside its owning wall thickness.");
            foreach (var other in openings.Where(o => o != opening && o.Boundary == opening.Boundary))
                if (opening.AlongMm < other.AlongMm+other.WidthMm && other.AlongMm < opening.AlongMm+opening.WidthMm
                    && opening.SillMm < other.SillMm+other.HeightMm && other.SillMm < opening.SillMm+opening.HeightMm)
                    Fail("scene-openings-overlap", $"Openings '{opening.Name}' and '{other.Name}' overlap.");
        }
        foreach (var look in rooms.SelectMany(r => new[] {r.Appearance,r.FloorAppearance,r.CeilingAppearance})
            .Concat(openings.SelectMany(o => new[] {o.Finish?.GlazingAppearance,o.Finish?.FrameAppearance}))
            .Concat(occurrences.Select(o => o.Appearance)).OfType<string>())
            if (!looks.Appearances.ContainsKey(look)) Fail("scene-appearance-unknown", $"Unknown Appearance '{look}'.");
        var frameNames = rooms.SelectMany(r => new[] {"floor","ceiling","northWall","southWall","eastWall","westWall"}.Select(b => r.Name+"."+b))
            .Concat(layout.Select(d => d.Identity)).Append("World").ToHashSet(StringComparer.Ordinal);
        foreach (var reference in layout.Select(d => d.Transform.From).Concat(occurrences.Select(o => o.Placement.From)))
            if (!frameNames.Contains(reference)) Fail("scene-frame-unresolved", $"Unknown Scene frame '{reference}'. Use World, a Room boundary or a Concept datum.");
        return new(new(name,units,definitionSource,identity,rooms,openings,cameras,occurrences,looks,patterns,
            layout.Select(d => new SceneLayoutFrame(d.Identity,d.Transform)).ToArray()),diagnostics);

        void ReadOccurrence(Reader r, string parent)
        {
            var start = r.Position; r.Expect("<"); var kind = r.Take(); var id = r.Identifier();
            if (kind == "Group")
            {
                r.Expect(">"); var group = parent+"."+id;
                while (!(r.Peek == "<" && r.Next == "/")) ReadOccurrence(r,group);
                r.Expect("<"); r.Expect("/"); r.Expect("Group"); r.Expect(">"); return;
            }
            if (kind is not ("Part" or "Assembly")) Fail("scene-occurrence-kind-invalid", "Scene occurrences are Part or Assembly.");
            r.Expect("="); var definition = r.UntilTagEnd(); var content = r.UntilClosing(kind);
            var prefix=$"<{kind} {id}>"; var suffix=$"</{kind}>";
            var occurrenceBody=FirmamentSourceSpelling.Normalize(prefix+content.Text+suffix)[prefix.Length..^suffix.Length];
            var placementReader = new Reader(occurrenceBody,identity,content.Start); string? appearance = null; string? placement = null;
            while (!placementReader.End)
            {
                if (placementReader.Optional(";")) continue;
                if (placementReader.Optional("Appearance"))
                { if (appearance is not null) Fail("scene-duplicate-field","Repeated appearance."); placementReader.Expect(":"); appearance=placementReader.Identifier(); placementReader.Optional(";"); }
                else if (placementReader.Optional("Placement"))
                { if (placement is not null) Fail("scene-placement-duplicate","Repeated Placement."); placement=placementReader.Block().Text; }
                else Fail("scene-occurrence-field-invalid", $"Occurrence '{id}' has unknown field '{placementReader.Peek}' at {placementReader.Position}. Expected Placement and optional appearance; mechanical semantics remain in its definition.");
            }
            if (placement is null) Fail("scene-placement-required", $"Occurrence '{id}' requires Placement.");
            var fields = Fields(placement,identity); var from = fields.GetValueOrDefault("From", "Origin");
            fields.Remove("From");
            if (fields.TryGetValue("TranslateLocal",out var translation)) fields["TranslateLocal"]="["+string.Join(",",Vector(translation).Select(v => v.ToString("R",CultureInfo.InvariantCulture)+"mm"))+"]";
            var target = AssemblyFrameAuthoring.Parse(id,string.Join(";",fields.Select(f => f.Key+":"+f.Value))+";","To",identity,diagnostics);
            if (target is null) Fail("scene-placement-invalid", $"Invalid Placement for '{id}'.");
            var path = parent+"."+id;
            if (occurrences.Any(o => o.Path == path)) Fail("scene-occurrence-collision", $"Duplicate occurrence '{path}'.");
            var pattern = patterns.FirstOrDefault(p => path.StartsWith(name+"."+p.Name+".",StringComparison.Ordinal));
            var key = pattern is null ? null : path.Split('.')[2];
            var span = pattern is null ? AuthoredSpan(kind,id,tag:true) : AuthoredSpan("Pattern",pattern.Name);
            occurrences.Add(new(path,kind,definition,target,from,appearance,span,key));
        }
        SemanticSourceSpan AuthoredSpan(string kind,string id,bool tag=false)
        {
            var t=FirmamentLanguageAnalysisService.Lex(authoredText);
            for(var i=0;i+1<t.Count;i++)
            {
                if(t[i].Text != kind || t[i+1].Text != id || t[i].Shape != "identifier") continue;
                if(tag && (i == 0 || t[i-1].Text != "<")) continue;
                var start=tag ? t[i-1].Start : t[i].Start;
                if(tag)
                {
                    for(var j=i+2;j+3<t.Count;j++)
                        if(t[j].Text == "<" && t[j+1].Text == "/" && t[j+2].Text == kind && t[j+3].Text == ">")
                            return new(identity,start,t[j+3].Start+t[j+3].Length-start);
                }
                else
                {
                    var depth=0; var seen=false;
                    for(var j=i+2;j<t.Count;j++)
                    { if(t[j].Text == "{") { depth++; seen=true; } else if(t[j].Text == "}" && --depth == 0 && seen) return new(identity,start,t[j].Start+1-start); }
                }
            }
            Fail("scene-source-span-unresolved",$"Cannot locate authored declaration '{kind} {id}'."); return default!;
        }
    }

    internal static Dictionary<string,string> Fields(string source, string identity)
    {
        var r = new Reader(source,identity); var fields = new Dictionary<string,string>(StringComparer.Ordinal);
        while (!r.End)
        {
            if (r.Optional(";")) continue;
            var name = r.Identifier(); r.Expect(":"); var value = r.FieldValue();
            if (!fields.TryAdd(name,value)) Fail("scene-duplicate-field", $"Repeated field '{name}'.");
        }
        return fields;
    }
    private static void Check(Dictionary<string,string> f,string allowed,string required)
    {
        if (f.Keys.Except(allowed.Split(' ')).Any() || required.Split(' ').Any(k => !f.ContainsKey(k)))
            Fail("scene-fields-invalid", $"Required fields: {required}; allowed fields: {allowed}.");
    }
    internal static double Length(string value)
    {
        // Normalize metre literals at the Scene boundary, then use the existing
        // dimension-checked scalar evaluator. No separate Scene arithmetic engine.
        var expression = Regex.Replace(value, @"(?<![\w.])(?<n>(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)m\b", m =>
            (double.Parse(m.Groups["n"].Value,CultureInfo.InvariantCulture)*1000).ToString("R",CultureInfo.InvariantCulture)+"mm");
        if (!FirmamentV2FeatureExpansion.TryEvaluateScalar(expression,out var n,out var unit) || unit != "mm" || !double.IsFinite(n))
            Fail("scene-length-unit-required", $"Expected a finite length expression with m or mm suffix, got '{value}'.");
        return n;
    }
    private static double Angle(string value)
    {
        double n = 0;
        if (!value.EndsWith("deg",StringComparison.Ordinal) || !double.TryParse(value[..^3],NumberStyles.Float,CultureInfo.InvariantCulture,out n) || !double.IsFinite(n))
            Fail("scene-angle-invalid","Scene camera fov requires a finite deg literal.");
        return n;
    }
    internal static double[] Vector(string value)
    {
        if (!value.StartsWith('[') || !value.EndsWith(']')) Fail("scene-vector-invalid","Expected a three-length vector.");
        var v = value[1..^1].Split(',',StringSplitOptions.TrimEntries);
        if (v.Length != 3) Fail("scene-vector-invalid","Expected a three-length vector.");
        return v.Select(Length).ToArray();
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    internal static void Fail(string code,string message) => throw new SceneSyntaxException(code,message);
    private sealed class SceneSyntaxException(string code,string message) : Exception(message) { internal string Code { get; } = code; }

    private sealed class Reader
    {
        private readonly string source; private readonly string identity; private readonly int offset;
        private readonly List<FirmamentLanguageAnalysisService.Lexeme> tokens; private int index;
        internal Reader(string text,string identity,int offset=0) { source=text; this.identity=identity; this.offset=offset; tokens=FirmamentLanguageAnalysisService.Lex(text).Where(t => t.Shape != "comment").ToList(); }
        internal bool End => index >= tokens.Count;
        internal string Peek => End ? "" : tokens[index].Text;
        internal string Next => index+1 >= tokens.Count ? "" : tokens[index+1].Text;
        internal int Position => offset+(End ? source.Length : tokens[index].Start);
        internal string Take() { if (End) Fail("scene-syntax-invalid",$"Unexpected end of {identity}."); return tokens[index++].Text; }
        internal bool Optional(string s) { if (Peek != s) return false; index++; return true; }
        internal void Expect(string s) { if (!Optional(s)) Fail("scene-syntax-invalid",$"Expected '{s}', got '{Peek}' at {Position}."); }
        internal string Identifier() { if (End || tokens[index].Shape != "identifier") Fail("scene-syntax-invalid",$"Expected name at {Position}."); return Take(); }
        internal (string Text,int Start) Block()
        {
            Expect("{"); var start=index == 0 ? 0 : tokens[index-1].Start+1; var depth=1;
            while (!End) { var t=tokens[index++]; if (t.Text == "{") depth++; if (t.Text == "}") depth--;
                if (t.Text == "}" && depth == 0) return (source[start..t.Start],offset+start); }
            Fail("scene-syntax-invalid","Unclosed declaration."); return default;
        }
        internal void SkipDeclaration() { while (!End && Peek != "{") Take(); Block(); Optional(";"); }
        internal string FieldValue()
        {
            var start=End ? source.Length : tokens[index].Start; var end=start; var depth=0;
            while (!End)
            {
                if (depth == 0 && (Peek == ";" || (end > start && tokens[index].Shape == "identifier" && Next == ":"))) break;
                var t=tokens[index++]; end=t.Start+t.Length;
                if (t.Text is "[" or "{" or "(") depth++; else if (t.Text is "]" or "}" or ")") depth--;
                if (depth < 0) Fail("scene-fields-invalid","Unbalanced field value.");
            }
            if (depth != 0 || start == end) Fail("scene-fields-invalid","Empty or unbalanced field value.");
            Optional(";"); return source[start..end].Trim();
        }
        internal string UntilTagEnd()
        {
            if (End || Peek == ">") Fail("scene-syntax-invalid","Expected an occurrence definition before the closing tag.");
            var start=tokens[index].Start; var depth=0;
            while (!End) { var t=tokens[index++]; if (t.Text == "<") depth++; else if (t.Text == ">") { if (depth == 0) return source[start..t.Start].Trim(); depth--; } }
            Fail("scene-syntax-invalid","Unclosed occurrence tag."); return "";
        }
        internal (string Text,int Start) UntilClosing(string kind)
        {
            var start=End ? source.Length : tokens[index].Start;
            while (!End) { if (Peek == "<" && Next == "/") { var end=tokens[index].Start; Expect("<"); Expect("/"); Expect(kind); Expect(">"); return (source[start..end],offset+start); } Take(); }
            Fail("scene-syntax-invalid","Unclosed occurrence."); return default;
        }
    }
}
