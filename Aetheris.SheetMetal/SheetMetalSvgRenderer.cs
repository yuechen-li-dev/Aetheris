using System.Globalization;
using System.Text;

namespace Aetheris.SheetMetal;

public static class SheetMetalSvgRenderer
{
    public static string Render(SheetMetalFlatPatternIr flat)
    {
        ArgumentNullException.ThrowIfNull(flat);var b=flat.Bounds??new(0,0,100,100);const double margin=12;var width=Math.Max(1,b.Width)+2*margin;var height=Math.Max(1,b.Height)+2*margin;
        double x(double value)=>value-b.MinX+margin;double y(double value)=>height-(value-b.MinY+margin);
        string points(IEnumerable<SheetPoint2> p)=>string.Join(" ",p.Select(q=>$"{F(x(q.X))},{F(y(q.Y))}"));
        var sb=new StringBuilder();sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}mm\" height=\"{F(height)}mm\" viewBox=\"0 0 {F(width)} {F(height)}\">");
        sb.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");
        sb.AppendLine("<g id=\"sheet-regions\" fill=\"#dbeafe\" fill-opacity=\"0.55\" stroke=\"#0f172a\" stroke-width=\"0.45\">");
        foreach(var r in flat.Regions2D.OrderBy(r=>r.StableId,StringComparer.Ordinal))if(r.Boundary.Count>=3)sb.AppendLine($"<polygon id=\"{Escape(r.StableId)}\" points=\"{points(r.Boundary)}\"/>");sb.AppendLine("</g>");
        sb.AppendLine("<g id=\"cut-contours\" fill=\"white\" stroke=\"#dc2626\" stroke-width=\"0.5\">");foreach(var c in flat.CutLoops.OrderBy(c=>c.FeatureId,StringComparer.Ordinal))if(c.Boundary.Count>=3)sb.AppendLine($"<polygon id=\"{Escape(c.FeatureId)}\" points=\"{points(c.Boundary)}\"/>");sb.AppendLine("</g>");
        var labels=new List<(double X,double Y,double Width,string Text)>();
        sb.AppendLine("<g id=\"bend-lines\" fill=\"none\" stroke=\"#2563eb\" stroke-width=\"0.45\" stroke-dasharray=\"3 2\">");foreach(var bend in flat.BendLines.OrderBy(b=>b.BendId,StringComparer.Ordinal)){var x1=x(bend.Start.X);var y1=y(bend.Start.Y);var x2=x(bend.End.X);var y2=y(bend.End.Y);sb.AppendLine($"<line x1=\"{F(x1)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\"/>");var label=$"{bend.Direction} {F(bend.BendAngleRadians*180/Math.PI)}° R{F(bend.InsideRadius)}";var dx=x2-x1;var dy=y2-y1;var length=Math.Sqrt(dx*dx+dy*dy);var nx=length<=1e-9?0d:-dy/length;var ny=length<=1e-9?-1d:dx/length;var lx=(x1+x2)/2+nx*4;var ly=(y1+y2)/2+ny*4;var labelWidth=Math.Max(18,label.Length*1.65);for(var attempt=0;attempt<8&&labels.Any(other=>Math.Abs(other.X-lx)<(other.Width+labelWidth)/2&&Math.Abs(other.Y-ly)<4.2);attempt++){lx+=nx*5;ly+=ny*5;}lx=Math.Clamp(lx,labelWidth/2+1,width-labelWidth/2-1);ly=Math.Clamp(ly,8,height-3);labels.Add((lx,ly,labelWidth,label));}sb.AppendLine("</g>");
        sb.AppendLine("<g id=\"bend-labels\" stroke=\"none\" font-family=\"Inter, 'Segoe UI', Arial, sans-serif\" font-size=\"3\" text-anchor=\"middle\" dominant-baseline=\"central\">");foreach(var label in labels){sb.AppendLine($"<rect x=\"{F(label.X-label.Width/2-.7)}\" y=\"{F(label.Y-2.2)}\" width=\"{F(label.Width+1.4)}\" height=\"4.4\" rx=\"0.8\" fill=\"white\" fill-opacity=\"0.88\"/>");sb.AppendLine($"<text x=\"{F(label.X)}\" y=\"{F(label.Y)}\" fill=\"#1d4ed8\">{Escape(label.Text)}</text>");}sb.AppendLine("</g>");
        sb.AppendLine($"<text x=\"4\" y=\"6\" font-family=\"sans-serif\" font-size=\"3\" fill=\"#334155\">{Escape(flat.Status.ToString())} · K={F(flat.Policy.KFactor)} · SHA-256 {flat.DeterministicHash[..12]}</text>");sb.AppendLine("</svg>");return sb.ToString();
    }
    public static void Write(string path,SheetMetalFlatPatternIr flat){ArgumentException.ThrowIfNullOrWhiteSpace(path);var full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllText(full,Render(flat),new UTF8Encoding(false));}
    private static string F(double v)=>v.ToString("0.###",CultureInfo.InvariantCulture);private static string Escape(string s)=>System.Security.SecurityElement.Escape(s)??string.Empty;
}
