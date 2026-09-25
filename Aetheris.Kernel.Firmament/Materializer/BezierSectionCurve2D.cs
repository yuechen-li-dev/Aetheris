namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Exact polynomial operations needed when a validated cubic profile becomes a section boundary.</summary>
internal static class BezierSectionCurve2D
{
    internal readonly record struct Hit((double X,double Y) Point,double First,double Second,bool Tangent);
    private static (double X, double Y) Mix((double X, double Y) a, (double X, double Y) b, double t) =>
        (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    internal static (double X, double Y) At(LineArcCubicBezier2D c, double t)
    {
        var u = 1d - t;
        return (u*u*u*c.Start.X + 3d*u*u*t*c.Control1.X + 3d*u*t*t*c.Control2.X + t*t*t*c.End.X,
            u*u*u*c.Start.Y + 3d*u*u*t*c.Control1.Y + 3d*u*t*t*c.Control2.Y + t*t*t*c.End.Y);
    }

    internal static (double X, double Y) Tangent(LineArcCubicBezier2D c, double t)
    {
        var u = 1d - t;
        return (3d*(u*u*(c.Control1.X-c.Start.X)+2d*u*t*(c.Control2.X-c.Control1.X)+t*t*(c.End.X-c.Control2.X)),
            3d*(u*u*(c.Control1.Y-c.Start.Y)+2d*u*t*(c.Control2.Y-c.Control1.Y)+t*t*(c.End.Y-c.Control2.Y)));
    }

    private static (LineArcCubicBezier2D Left, LineArcCubicBezier2D Right) Divide(LineArcCubicBezier2D c, double t)
    {
        var a = Mix(c.Start,c.Control1,t); var b = Mix(c.Control1,c.Control2,t); var d = Mix(c.Control2,c.End,t);
        var e = Mix(a,b,t); var f = Mix(b,d,t); var p = Mix(e,f,t);
        return (new(c.Start,a,e,p),new(p,f,d,c.End));
    }

    internal static LineArcCubicBezier2D Trim(LineArcCubicBezier2D c, double from, double to)
    {
        if (from > 0d) c = Divide(c,from).Right;
        if (to < 1d) c = Divide(c,(to-from)/(1d-from)).Left;
        return c;
    }

    /// <summary>Green's theorem integral of x dy - y dx, evaluated on polynomial coefficients.</summary>
    internal static double SignedDoubleArea(LineArcCubicBezier2D c)
    {
        var x = new[] { c.Start.X, 3d*(c.Control1.X-c.Start.X), 3d*(c.Start.X-2d*c.Control1.X+c.Control2.X), -c.Start.X+3d*c.Control1.X-3d*c.Control2.X+c.End.X };
        var y = new[] { c.Start.Y, 3d*(c.Control1.Y-c.Start.Y), 3d*(c.Start.Y-2d*c.Control1.Y+c.Control2.Y), -c.Start.Y+3d*c.Control1.Y-3d*c.Control2.Y+c.End.Y };
        var area = 0d;
        for (var i=0;i<4;i++) for (var j=1;j<4;j++) area += j*(x[i]*y[j]-y[i]*x[j])/(i+j);
        return area;
    }

    private static double[] YStationary(LineArcCubicBezier2D c)
    {
        var a = -c.Start.Y+3d*c.Control1.Y-3d*c.Control2.Y+c.End.Y;
        var b = 3d*c.Start.Y-6d*c.Control1.Y+3d*c.Control2.Y;
        var d = -3d*c.Start.Y+3d*c.Control1.Y;
        var roots = new List<double> { 0d, 1d };
        if (Math.Abs(a)<1e-15) { if (Math.Abs(b)>1e-15) roots.Add(-d/(2d*b)); }
        else { var discriminant=b*b-3d*a*d; if (discriminant>=0d) { var root=Math.Sqrt(discriminant); roots.Add((-b-root)/(3d*a)); roots.Add((-b+root)/(3d*a)); } }
        return roots.Where(t=>t>=0d && t<=1d).Distinct().Order().ToArray();
    }

    internal static int RayCrossings(LineArcCubicBezier2D c, (double X,double Y) point, double tol)
    {
        var roots=YStationary(c); var count=0;
        for (var i=0;i<roots.Length-1;i++)
        {
            var lo=roots[i]; var hi=roots[i+1]; var y0=At(c,lo).Y; var y1=At(c,hi).Y;
            if (Math.Abs(y1-y0)<1e-14 || point.Y<Math.Min(y0,y1) || point.Y>=Math.Max(y0,y1)) continue;
            for (var n=0;n<52;n++) { var mid=(lo+hi)/2d; if ((At(c,mid).Y<point.Y)==(y1>y0)) lo=mid; else hi=mid; }
            if (At(c,(lo+hi)/2d).X>point.X+tol) count++;
        }
        return count;
    }

    internal static bool OnCurve(LineArcCubicBezier2D c, (double X,double Y) point, double tol, out double parameter)
    {
        parameter=0d; var best=double.PositiveInfinity;
        // A closest-point seed is refined on the exact polynomial. Sampling is used only to locate a parameter,
        // never to replace curve authority or to declare a distant point on the boundary.
        for (var i=0;i<=32;i++)
        {
            var t=i/32d; var p=At(c,t); var d=(p.X-point.X)*(p.X-point.X)+(p.Y-point.Y)*(p.Y-point.Y);
            if (d<best) { best=d; parameter=t; }
        }
        for (var i=0;i<16;i++)
        {
            var p=At(c,parameter); var tangent=Tangent(c,parameter); var denominator=tangent.X*tangent.X+tangent.Y*tangent.Y;
            if (denominator<1e-25) break;
            var next=Math.Clamp(parameter+((point.X-p.X)*tangent.X+(point.Y-p.Y)*tangent.Y)/denominator,0d,1d);
            if (Math.Abs(next-parameter)<1e-15) break;
            parameter=next;
        }
        var final=At(c,parameter);
        return Math.Sqrt((final.X-point.X)*(final.X-point.X)+(final.Y-point.Y)*(final.Y-point.Y))<=tol;
    }

    private static (double X,double Y) At(LineArcProfileCurve2D c,double t) => c switch
    {
        LineArcLineSegment2D l => Mix(l.Start,l.End,t),
        LineArcCircularArc2D a => (a.Center.X+a.Radius*Math.Cos(a.StartAngleRadians+a.SweepAngleRadians*t),a.Center.Y+a.Radius*Math.Sin(a.StartAngleRadians+a.SweepAngleRadians*t)),
        LineArcCubicBezier2D b => At(b,t),
        _ => throw new NotSupportedException($"Unsupported section boundary: {c.GetType().Name}")
    };

    private static (double X,double Y) Tangent(LineArcProfileCurve2D c,double t) => c switch
    {
        LineArcLineSegment2D l => (l.End.X-l.Start.X,l.End.Y-l.Start.Y),
        LineArcCircularArc2D a => (-a.Radius*a.SweepAngleRadians*Math.Sin(a.StartAngleRadians+a.SweepAngleRadians*t),a.Radius*a.SweepAngleRadians*Math.Cos(a.StartAngleRadians+a.SweepAngleRadians*t)),
        LineArcCubicBezier2D b => Tangent(b,t),
        _ => throw new NotSupportedException($"Unsupported section boundary: {c.GetType().Name}")
    };

    private static (double MinX,double MaxX,double MinY,double MaxY) Bounds(LineArcProfileCurve2D c,double from,double to)
    {
        (double X,double Y)[] points;
        if (c is LineArcCubicBezier2D b)
        {
            var part=Trim(b,from,to); points=[part.Start,part.Control1,part.Control2,part.End];
        }
        else if (c is LineArcCircularArc2D a)
        {
            var start=a.StartAngleRadians+a.SweepAngleRadians*from;
            var end=a.StartAngleRadians+a.SweepAngleRadians*to;
            var low=Math.Min(start,end); var high=Math.Max(start,end);
            var candidates=new List<double> { start,end };
            for (var k=(int)Math.Ceiling(low/(Math.PI/2d));k<=Math.Floor(high/(Math.PI/2d));k++) candidates.Add(k*Math.PI/2d);
            points=candidates.Select(angle=>(a.Center.X+a.Radius*Math.Cos(angle),a.Center.Y+a.Radius*Math.Sin(angle))).ToArray();
        }
        else points=[At(c,from),At(c,to)];
        return (points.Min(p=>p.X),points.Max(p=>p.X),points.Min(p=>p.Y),points.Max(p=>p.Y));
    }

    /// <summary>Bounded candidate search by exact convex-hull/arc boxes, then polynomial/analytic parameter refinement.</summary>
    internal static IReadOnlyList<Hit> Intersections(LineArcProfileCurve2D first,LineArcProfileCurve2D second,double tol)
    {
        var hits=new List<Hit>();
        void Add(double t,double u)
        {
            for (var i=0;i<18;i++)
            {
                var a=At(first,t); var b=At(second,u); var da=Tangent(first,t); var db=Tangent(second,u);
                var determinant=da.X*db.Y-da.Y*db.X;
                if (Math.Abs(determinant)<1e-17) break;
                var dx=b.X-a.X; var dy=b.Y-a.Y;
                var dt=(dx*db.Y-dy*db.X)/determinant;
                var du=(dx*da.Y-dy*da.X)/determinant;
                t=Math.Clamp(t+dt,0d,1d);u=Math.Clamp(u+du,0d,1d);
                if (Math.Abs(dt)+Math.Abs(du)<1e-14) break;
            }
            var pa=At(first,t);var pb=At(second,u);
            if (Math.Sqrt((pa.X-pb.X)*(pa.X-pb.X)+(pa.Y-pb.Y)*(pa.Y-pb.Y))>tol) return;
            if (hits.Any(h=>Math.Abs(h.First-t)<1e-6 && Math.Abs(h.Second-u)<1e-6)) return;
            var ta=Tangent(first,t);var tb=Tangent(second,u);
            hits.Add(new(((pa.X+pb.X)/2d,(pa.Y+pb.Y)/2d),t,u,Math.Abs(ta.X*tb.Y-ta.Y*tb.X)<1e-10));
        }
        foreach (var t in new[] {0d,1d}) foreach(var u in new[] {0d,1d})
        {
            var a=At(first,t);var b=At(second,u);
            if (Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y))<=tol) Add(t,u);
        }
        void Search(double a0,double a1,double b0,double b1,int depth)
        {
            var a=Bounds(first,a0,a1);var b=Bounds(second,b0,b1);
            if (a.MinX>b.MaxX+tol || b.MinX>a.MaxX+tol || a.MinY>b.MaxY+tol || b.MinY>a.MaxY+tol) return;
            var aw=Math.Max(a.MaxX-a.MinX,a.MaxY-a.MinY);var bw=Math.Max(b.MaxX-b.MinX,b.MaxY-b.MinY);
            if (Math.Max(aw,bw)<=tol/2d || depth>=48) { Add((a0+a1)/2d,(b0+b1)/2d);return; }
            if (aw>=bw) { var mid=(a0+a1)/2d;Search(a0,mid,b0,b1,depth+1);Search(mid,a1,b0,b1,depth+1); }
            else { var mid=(b0+b1)/2d;Search(a0,a1,b0,mid,depth+1);Search(a0,a1,mid,b1,depth+1); }
        }
        Search(0d,1d,0d,1d,0);
        return hits.OrderBy(h=>h.First).ThenBy(h=>h.Second).ToArray();
    }
}
