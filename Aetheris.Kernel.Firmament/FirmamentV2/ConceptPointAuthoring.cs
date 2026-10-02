using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Finite Concept point recipes. Checked geometry projects to existing keyed Sets;
/// the ordinary Pattern expander remains the only occurrence expansion authority.</summary>
internal static class ConceptPointAuthoring
{
    internal const string Prefix = "firmament-concept-points-";
    internal sealed record Result(string Source, IReadOnlyList<ConceptIrKeyedPointSetValue> Points, MathCatalog Math);
    private sealed record Function(string Name, IReadOnlyList<(string Name, string Type)> Parameters, string ReturnType, string Body);

    internal sealed class MathCatalog
    {
        private int _remainingCalls;
        private readonly Dictionary<string, Function> _functions = new(StringComparer.Ordinal);
        internal string Parse(string source, List<string> diagnostics)
        {
            var changes = new List<(int Start, int Length)>();
            if (Regex.IsMatch(source, @"\bComptime\s+Function\b")) diagnostics.Add(Prefix+"function-syntax-invalid");
            foreach (Match header in Regex.Matches(source, @"\bFunction\s+(?<name>\w+)\s*\((?<args>[^()]*)\)\s*->\s*(?<type>\w+)\s*=\s*(?<body>[^;{}]+);"))
            {
                var args = header.Groups["args"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var parameters = new List<(string Name,string Type)>();
                foreach (var argument in args)
                {
                    var match = Regex.Match(argument,@"^(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>Int|Float|Length|Angle)$");
                    if (!match.Success) diagnostics.Add(Prefix+"function-parameter-invalid");
                    else parameters.Add((match.Groups["name"].Value,match.Groups["type"].Value));
                }
                var name = header.Groups["name"].Value;
                var type = header.Groups["type"].Value;
                if (!Regex.IsMatch(name, @"^[A-Za-z_]\w*$") || name == "Pow" || !Allowed(type) || parameters.Count > 16 || parameters.Select(p=>p.Name).Distinct().Count()!=parameters.Count
                    || !_functions.TryAdd(name,new(name,parameters,type,header.Groups["body"].Value)))
                    diagnostics.Add(Prefix+"function-definition-invalid:"+name);
                changes.Add((header.Index,header.Length));
            }
            if (_functions.Count > 128) { diagnostics.Add(Prefix+"function-limit"); return source; }
            var checkedFunctions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var function in _functions.Values)
            {
                var variables=function.Parameters.ToDictionary(p=>p.Name,p=>(1d,Unit(p.Type)),StringComparer.Ordinal);
                var dependencies = Regex.Matches(function.Body,@"\b(?<name>[A-Za-z_]\w*)\s*\(").Select(m=>m.Groups["name"].Value).ToArray();
                if (!FirmamentV2FeatureExpansion.TryEvaluateScalar(function.Body,variables,(name,args)=>Call(name,args,true,[]),true,out _,out var unit)
                    || unit!=Unit(function.ReturnType)) diagnostics.Add(Prefix+"function-body-invalid:"+function.Name);
                bool Cycle(string name,HashSet<string> visiting)
                {
                    if (!_functions.TryGetValue(name,out var f) || checkedFunctions.Contains(name)) return false;
                    if (!visiting.Add(name)) return true;
                    var cycle=Regex.Matches(f.Body,@"\b(?<name>[A-Za-z_]\w*)\s*\(").Any(m=>Cycle(m.Groups["name"].Value,visiting));
                    visiting.Remove(name);
                    if (!cycle) checkedFunctions.Add(name);
                    return cycle;
                }
                if (dependencies.Any(d=>Cycle(d,new HashSet<string>(StringComparer.Ordinal)))) diagnostics.Add(Prefix+"function-cycle:"+function.Name);
            }
            foreach (var change in changes.OrderByDescending(c=>c.Start)) source=source.Remove(change.Start,change.Length);
            if (Regex.IsMatch(source,@"\b(?:Comptime\s+)?Function\b")) diagnostics.Add(Prefix+"function-syntax-invalid");
            return source;
        }

        private (double Value,string Unit) Call(string name,IReadOnlyList<(double Value,string Unit)> args,bool typeOnly,HashSet<string> active)
        {
            if (!typeOnly && --_remainingCalls < 0) throw new FormatException();
            if (!_functions.TryGetValue(name,out var function) || args.Count!=function.Parameters.Count || active.Count>=32 || !active.Add(name)) throw new FormatException();
            try
            {
                for(var i=0;i<args.Count;i++)
                    if(args[i].Unit!=Unit(function.Parameters[i].Type) || (!typeOnly && function.Parameters[i].Type=="Int" && !ValidInt(args[i].Value))) throw new FormatException();
                if(typeOnly) return (1,Unit(function.ReturnType));
                var variables=function.Parameters.Select((p,i)=>(p.Name,args[i])).ToDictionary(p=>p.Name,p=>p.Item2,StringComparer.Ordinal);
                if(!FirmamentV2FeatureExpansion.TryEvaluateScalar(function.Body,variables,(n,a)=>Call(n,a,false,active),false,out var value,out var unit)
                    || unit!=Unit(function.ReturnType) || (function.ReturnType=="Int" && !ValidInt(value))) throw new FormatException();
                return (value,unit);
            }
            finally { active.Remove(name); }
        }

        internal bool Evaluate(string expression,IReadOnlyDictionary<string,(double Value,string Unit)> variables,out double value,out string unit)
        {
            _remainingCalls = 10000;
            return FirmamentV2FeatureExpansion.TryEvaluateScalar(expression,variables,(n,a)=>Call(n,a,false,[]),false,out value,out unit);
        }

        // Only scalar consumer fields are normalized. No source code is executed.
        internal string Normalize(string source,List<string> diagnostics)
        {
            foreach(var name in _functions.Keys)
            {
                var offset=0;
                while(offset<source.Length)
                {
                    var match=Regex.Match(source[offset..],$@"\b{Regex.Escape(name)}\s*\(");
                    if(!match.Success) break;
                    var start=offset+match.Index; var open=source.IndexOf('(',start); var close=Matching(source,open,'(',')');
                    if(close<0) { diagnostics.Add(Prefix+"function-call-invalid:"+name); break; }
                    var expression=source[start..(close+1)];
                    if(!Evaluate(expression,new Dictionary<string,(double,string)>(),out var value,out var unit))
                    { diagnostics.Add(Prefix+"function-call-invalid:"+expression); offset=close+1; continue; }
                    var literal=Literal(value,unit);
                    source=source.Remove(start,close-start+1).Insert(start,literal); offset=start+literal.Length;
                }
            }
            return source;
        }
        private static bool ValidInt(double value) => Math.Abs(value) <= 9007199254740991d && value == Math.Truncate(value);
        private static bool Allowed(string type)=>type is "Int" or "Float" or "Length" or "Angle";
        private static string Unit(string type)=>type switch { "Length"=>"mm","Angle"=>"deg",_=>"" };
    }

    internal static Result? Expand(string source,List<string> diagnostics)
    {
        var math=new MathCatalog(); source=math.Parse(source,diagnostics);
        var collections=new List<ConceptIrKeyedPointSetValue>();
        var changes=new List<(int Start,int Length,string Text)>();
        var sets=new Dictionary<string,string>(StringComparer.Ordinal);
        var totalPoints = 0;
        foreach(Match owner in Regex.Matches(source,@"\bConcept\s+Struct\s+(?<name>[A-Za-z_]\w*)\s*\{"))
        {
            var open=source.IndexOf('{',owner.Index); var close=Matching(source,open,'{','}');
            if(close<0) { diagnostics.Add(Prefix+"unclosed"); continue; }
            var body=source[(open+1)..close];
            if(!Regex.IsMatch(body,@"\bPoints\s*<")) continue;
            var generated=new List<string>(); var remaining=body;
            foreach(Match header in Regex.Matches(body,@"\bPoints\s*<\s*(?<type>Point2|Point3)\s*>\s+(?<name>[A-Za-z_]\w*)\s*\{"))
            {
                var end=Matching(body,body.IndexOf('{',header.Index),'{','}');
                if(end<0) { diagnostics.Add(Prefix+"unclosed"); continue; }
                if (totalPoints > 4096) return null;
                var fields=body[(header.Index+header.Length)..end];
                var identity=owner.Groups["name"].Value+"."+header.Groups["name"].Value;
                var setName=SetName(identity);
                if(!sets.TryAdd(identity,setName)) { diagnostics.Add(Prefix+"duplicate:"+identity); continue; }
                var type=header.Groups["type"].Value; var count=type=="Point2"?2:3;
                var points=new List<ConceptIrKeyedPointValue>();
                void Error(string reason)=>diagnostics.Add(Prefix+reason+":"+identity);
                var recipe=Regex.Match(fields,@"\b(?<kind>Linear|Series)\s*\{");
                if(recipe.Success)
                {
                    var finish=Matching(fields,fields.IndexOf('{',recipe.Index),'{','}');
                    if(finish<0) { Error("recipe-unclosed"); continue; }
                    var recipeBody=fields[(recipe.Index+recipe.Length)..finish];
                    string? stationsBody = null;
                    var stationHeaders = Regex.Matches(recipeBody, @"\bStations\s*\{");
                    if (stationHeaders.Count > 0)
                    {
                        if (stationHeaders.Count != 1 || recipe.Groups["kind"].Value != "Linear") { Error("stations-fields-invalid"); continue; }
                        var stationHeader = stationHeaders[0];
                        var stationEnd = Matching(recipeBody, stationHeader.Index + stationHeader.Length - 1, '{', '}');
                        if (stationEnd < 0) { Error("stations-unclosed"); continue; }
                        stationsBody = recipeBody[(stationHeader.Index + stationHeader.Length)..stationEnd];
                        recipeBody = recipeBody.Remove(stationHeader.Index, stationEnd - stationHeader.Index + 1);
                    }
                    var entries=Fields(recipeBody,Error);
                    var outer=fields.Remove(recipe.Index,finish-recipe.Index+1);
                    if(recipe.Groups["kind"].Value=="Linear")
                    {
                        var outerFields=Fields(outer,Error);
                        if (stationsBody is not null)
                        {
                            if (outerFields.Count != 0 || entries.Count != 2 || !entries.ContainsKey("Start") || !entries.ContainsKey("Direction"))
                            { Error("stations-fields-invalid"); continue; }
                            var start = Point(entries["Start"], type, math, new Dictionary<string,(double,string)>(), Error);
                            var directionText = entries["Direction"].Trim();
                            if (start is null || !Regex.IsMatch(directionText, @"^\[[^\[\]]*\]$")) { Error("stations-direction-invalid"); continue; }
                            var components = Components(directionText[1..^1]);
                            var direction = new double[count];
                            if (components.Count != count) { Error("stations-direction-invalid"); continue; }
                            var validDirection = true;
                            for (var i = 0; i < count; i++)
                                if (!math.Evaluate(components[i], new Dictionary<string,(double,string)>(), out direction[i], out var unit)
                                    || unit != "" || !double.IsFinite(direction[i])) validDirection = false;
                            var scale = direction.Max(v => Math.Abs(v));
                            if (!validDirection || scale == 0) { Error("stations-direction-invalid"); continue; }
                            // Scale first so even finite very large/small vectors normalize safely.
                            var norm = Math.Sqrt(direction.Sum(v => (v / scale) * (v / scale)));
                            direction = direction.Select(v => (v / scale) / norm).ToArray();
                            var stations = Fields(stationsBody, Error);
                            if (stations.Count is < 1 or > 1024) { Error("point-limit"); continue; }
                            foreach (var station in stations)
                            {
                                if (!math.Evaluate(station.Value, new Dictionary<string,(double,string)>(), out var distance, out var unit)
                                    || unit != "mm" || !double.IsFinite(distance)) { Error("station-distance-invalid"); continue; }
                                Add(station.Key, points.Count, start.Zip(direction, (s, d) => s + d * distance).ToArray());
                            }
                            // Explicit station form is mutually exclusive with Keys/Step.
                            // The same keyed point IR and Pattern executor own both forms.
                        }
                        else
                        {
                        if(outerFields.Count!=1 || !outerFields.TryGetValue("Keys",out var keysText) || entries.Count!=2 || !entries.ContainsKey("Start") || !entries.ContainsKey("Step"))
                        { Error("linear-fields-invalid"); continue; }
                        if (!Regex.IsMatch(keysText, @"^\s*\[[^\[\]]*\]\s*$")) { Error("keys-invalid"); continue; }
                        var keys=keysText.Trim().Trim('[',']').Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
                        var start=Point(entries["Start"],type,math,new Dictionary<string,(double,string)>(),Error); var step=Point(entries["Step"],type,math,new Dictionary<string,(double,string)>(),Error);
                        if(start is null || step is null || keys.Length is <1 or >1024 || keys.Distinct().Count()!=keys.Length || keys.Any(k=>!Regex.IsMatch(k,@"^[A-Za-z_]\w*$")) || step.All(v=>v==0))
                        { Error("linear-invalid"); continue; }
                        for(var i=0;i<keys.Length;i++) Add(keys[i],i,start.Zip(step,(s,d)=>s+i*d).ToArray());
                        }
                    }
                    else
                    {
                        if(!string.IsNullOrWhiteSpace(outer.Replace(";","")) || entries.Count!=5 || new[]{"Index","First","Count","KeyPrefix","Position"}.Any(k=>!entries.ContainsKey(k)))
                        { Error("series-fields-invalid"); continue; }
                        if(!Regex.IsMatch(entries["Index"],@"^[A-Za-z_]\w*$") || !Regex.IsMatch(entries["KeyPrefix"],@"^[A-Za-z_]\w*$")
                            || !int.TryParse(entries["First"],out var first) || !int.TryParse(entries["Count"],out var amount) || first<0 || amount is <1 or >1024 || (long)first+amount>int.MaxValue)
                        { Error("series-bounds-invalid"); continue; }
                        for(var i=0;i<amount;i++)
                        {
                            var n=first+i;
                            var point=Point(entries["Position"],type,math,new Dictionary<string,(double,string)>{{entries["Index"],(n,"")}},Error);
                            if(point is not null) Add(entries["KeyPrefix"]+n.ToString(CultureInfo.InvariantCulture),i,point);
                        }
                    }
                }
                else
                {
                    var manual=Fields(fields,Error);
                    if(manual.Count is <1 or >1024) Error("point-limit");
                    foreach(var entry in manual)
                    { var point=Point(entry.Value,type,math,new Dictionary<string,(double,string)>(),Error); if(point is not null) Add(entry.Key,points.Count,point); }
                }
                void Add(string key,int ordinal,double[] point)
                {
                    if (++totalPoints > 4096) { if (totalPoints == 4097) Error("total-limit"); return; }
                    if(point.Length!=count || point.Any(v=>!double.IsFinite(v))) { Error("nonfinite"); return; }
                    points.Add(new("concept:"+identity+"."+key,key,ordinal,type,point,identity+"."+key));
                }
                collections.Add(new("concept:"+identity,identity,type,points,body[header.Index..(end+1)],identity));
                generated.Add($"Static {setName}: Set<{type}> {{\n"+string.Join("\n",points.Select(p=>$"{p.Key} => {type}({string.Join(",",p.Coordinates.Select(v=>Literal(v,"mm")))})"))+"\n}");
                remaining=remaining.Replace(body[header.Index..(end+1)],"",StringComparison.Ordinal);
            }
            if (Regex.IsMatch(remaining, @"\bPoints\s*<")) diagnostics.Add(Prefix+"collection-syntax-invalid");
            // Preserve other Concept members for their existing owner; erase an empty container.
            var retained=string.IsNullOrWhiteSpace(remaining.Replace(";",""))?"":$"Concept Struct {owner.Groups["name"].Value} {{ {remaining} }}";
            changes.Add((owner.Index,close-owner.Index+1,retained+"\n"+string.Join("\n",generated)));
        }
        if(collections.Sum(c=>c.Points.Count)>4096) diagnostics.Add(Prefix+"total-limit");
        foreach(var change in changes.OrderByDescending(c=>c.Start)) source=source.Remove(change.Start,change.Length).Insert(change.Start,change.Text);
        foreach(var collection in collections)
        {
            foreach(var point in collection.Points)
            {
                var path=collection.Name+"."+point.Key;
                if (point.ElementType == "Point2" && Regex.IsMatch(source,$@"\b{Regex.Escape(path)}(?:\.Position)?\.Z\b")) diagnostics.Add(Prefix+"point-dimension-invalid:"+path);
                for(var i=0;i<point.Coordinates.Count;i++) source=Regex.Replace(source,$@"\b{Regex.Escape(path)}(?:\.Position)?\.{"XYZ"[i]}\b",Literal(point.Coordinates[i],"mm"));
                source=Regex.Replace(source,$@"\b{Regex.Escape(path)}(?:\.Position)?\b",$"{point.ElementType}({string.Join(",",point.Coordinates.Select(v=>Literal(v,"mm")))})");
            }
            source=Regex.Replace(source,$@"\bOver\s+{Regex.Escape(collection.Name)}\b","Over "+sets[collection.Name]);
            if (Regex.IsMatch(source,$@"\b{Regex.Escape(collection.Name)}\.")) diagnostics.Add(Prefix+"point-member-unresolved:"+collection.Name);
        }
        return diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal)) ? null : new(source,collections,math);
    }

    private static Dictionary<string,string> Fields(string body,Action<string> error)
    {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var field in body.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))
        { var split=field.Split(':',2,StringSplitOptions.TrimEntries); if(split.Length!=2 || !Regex.IsMatch(split[0],@"^[A-Za-z_]\w*$") || !result.TryAdd(split[0],split[1])) error("field-invalid"); }
        return result;
    }
    private static double[]? Point(string text,string type,MathCatalog math,IReadOnlyDictionary<string,(double Value,string Unit)> variables,Action<string> error)
    {
        text=text.Trim();
        if(text.StartsWith(type+"(",StringComparison.Ordinal) && text.EndsWith(')')) text=text[(type.Length+1)..^1];
        else if(text.StartsWith('[') && text.EndsWith(']')) text=text[1..^1];
        else { error("point-type-invalid"); return null; }
        var parts=Components(text); var expected=type=="Point2"?2:3;
        if(parts.Count!=expected) { error("point-arity-invalid"); return null; }
        var result=new double[expected];
        for(var i=0;i<expected;i++) if(!math.Evaluate(parts[i],variables,out result[i],out var unit) || unit!="mm") { error("point-expression-invalid");return null; }
        return result;
    }
    internal static IReadOnlyList<string> Components(string text)
    {
        var parts=new List<string>(); var start=0; var depth=0;
        for(var i=0;i<text.Length;i++) { if(text[i]=='(') depth++; if(text[i]==')') depth--; if(text[i]==',' && depth==0) { parts.Add(text[start..i]);start=i+1; } }
        parts.Add(text[start..]); return parts;
    }
    internal static string SetName(string identity) => "__ConceptPoints_" + identity.Split('.').Aggregate("", (name, part) => name + part.Length.ToString(CultureInfo.InvariantCulture) + "_" + part);
    internal static string Literal(double value, string unit)
    {
        // Existing point grammars consume plain decimals. Expand round-trip exponent
        // spelling without rounding: a tiny Float may later be multiplied by a large Length.
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var exponentAt = text.IndexOf('E');
        if (exponentAt < 0) return text + unit;
        var mantissa = text[..exponentAt];
        var sign = mantissa.StartsWith('-') ? "-" : "";
        mantissa = mantissa.TrimStart('-');
        var decimalAt = mantissa.IndexOf('.');
        if (decimalAt < 0) decimalAt = mantissa.Length;
        decimalAt += int.Parse(text[(exponentAt + 1)..], CultureInfo.InvariantCulture);
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal);
        var plain = decimalAt <= 0 ? "0." + new string('0', -decimalAt) + digits
            : decimalAt >= digits.Length ? digits + new string('0', decimalAt - digits.Length)
            : digits.Insert(decimalAt, ".");
        return sign + plain + unit;
    }
    private static int Matching(string text,int open,char first,char last)
    { var depth=0; for(var i=open;i<text.Length;i++) { if(text[i]==first) depth++;else if(text[i]==last && --depth==0) return i; }return -1; }
}
